namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Middleware;

/// <summary>
/// Admin-only HTTP trigger that migrates existing Cosmos documents from single-level
/// partition key containers to new hierarchical-partition-key containers.
///
/// This is required because Cosmos DB does not allow in-place partition key changes.
/// The migration reads all documents from each old container and writes them into the
/// corresponding new container with <c>tenantId</c> prepended as the first partition level.
///
/// Usage:
///   POST /api/admin/migrate-partitions?dryRun=true   — preview what would be migrated
///   POST /api/admin/migrate-partitions               — execute the migration
///
/// The function is gated by <c>Admin:MigrationKey</c> in configuration. Supply the
/// key via the <c>X-Migration-Key</c> request header.
///
/// ⚠️  Run this ONCE against an existing deployment. New deployments provisioned with
/// hierarchical partition keys do not need this function.
/// </summary>
public class TenantMigrationFunction(
    CosmosClient cosmosClient,
    IConfiguration config,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TenantMigrationFunction>();

    // Containers to migrate: (sourceName, destinationName, old PK path, new PK paths)
    private static readonly (string Source, string Destination, string[] NewPkPaths)[] ContainerMap =
    [
        ("sessions",    "sessions-v2",    ["/tenantId", "/clientId"]),
        ("goals",       "goals-v2",       ["/tenantId", "/clientId"]),
        ("clients",     "clients-v2",     ["/tenantId", "/clientId"]),
        ("rate-limits", "rate-limits-v2", ["/tenantId", "/userId"]),
    ];

    [Function("TenantMigration")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/migrate-partitions")]
        HttpRequestData req)
    {
        // Gate the migration behind a shared secret to prevent accidental execution.
        var expectedKey = config["Admin:MigrationKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            _logger.LogError("Admin:MigrationKey is not configured. Migration endpoint is disabled.");
            return await WriteResponse(req, HttpStatusCode.ServiceUnavailable,
                "Migration endpoint is not configured.");
        }

        req.Headers.TryGetValues("X-Migration-Key", out var keyValues);
        var providedKey = keyValues?.FirstOrDefault();
        if (!string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Migration attempted with invalid key.");
            return await WriteResponse(req, HttpStatusCode.Unauthorized, "Invalid migration key.");
        }

        // Resolve the tenantId that will be stamped on all migrated documents.
        // For BYOA deployments this is the Entra TenantId from config.
        var tenantId = config["AzureAd:TenantId"];
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return await WriteResponse(req, HttpStatusCode.BadRequest,
                "AzureAd:TenantId must be configured before running migration.");
        }

        var dryRun = string.Equals(
            req.Query["dryRun"], "true", StringComparison.OrdinalIgnoreCase);

        var dbName = config["CosmosDb:DatabaseName"] ?? "theragraf";
        var database = cosmosClient.GetDatabase(dbName);

        var results = new List<object>();

        foreach (var (source, destination, newPkPaths) in ContainerMap)
        {
            var result = await MigrateContainerAsync(
                database, source, destination, newPkPaths, tenantId, dryRun);
            results.Add(result);
        }

        var summary = new
        {
            dryRun,
            tenantId,
            containers = results
        };

        _logger.LogInformation("Migration {Mode} complete. Summary: {Summary}",
            dryRun ? "dry-run" : "execution",
            JsonSerializer.Serialize(summary));

        return await WriteResponse(req, HttpStatusCode.OK, JsonSerializer.Serialize(summary));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<object> MigrateContainerAsync(
        Database database,
        string sourceName,
        string destinationName,
        string[] newPkPaths,
        string tenantId,
        bool dryRun)
    {
        int read = 0, written = 0, skipped = 0;
        var errors = new List<string>();

        // Check source container exists.
        Container source;
        try
        {
            source = database.GetContainer(sourceName);
            await source.ReadContainerAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Source container '{Source}' not found — skipping.", sourceName);
            return new { container = sourceName, status = "source_not_found", read, written, skipped, errors };
        }

        // Ensure destination container exists (with hierarchical PK).
        Container destination;
        if (!dryRun)
        {
            var props = new ContainerProperties { Id = destinationName, PartitionKeyPaths = newPkPaths };
            var response = await database.CreateContainerIfNotExistsAsync(props);
            destination = response.Container;
        }
        else
        {
            destination = database.GetContainer(destinationName);
        }

        // Stream all documents from source.
        using var feedIterator = source.GetItemQueryStreamIterator("SELECT * FROM c");

        while (feedIterator.HasMoreResults)
        {
            using var responseMessage = await feedIterator.ReadNextAsync();
            responseMessage.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(responseMessage.Content);
            var documents = doc.RootElement.GetProperty("Documents").EnumerateArray();

            foreach (var item in documents)
            {
                read++;

                // Stamp tenantId onto the document if not already present.
                var mutable = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(item.GetRawText())!;
                if (mutable.ContainsKey("tenantId") && mutable["tenantId"].GetString() == tenantId)
                {
                    skipped++;
                    continue;  // Already migrated.
                }

                mutable["tenantId"] = JsonDocument.Parse($"\"{tenantId}\"").RootElement.Clone();

                if (!dryRun)
                {
                    try
                    {
                        var json = JsonSerializer.Serialize(mutable);
                        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

                        // Build hierarchical partition key.
                        var pkBuilder = new PartitionKeyBuilder().Add(tenantId);

                        // Second partition key component varies by container type.
                        var secondKey = newPkPaths[1].TrimStart('/');
                        if (mutable.TryGetValue(secondKey, out var secondVal))
                            pkBuilder.Add(secondVal.GetString() ?? string.Empty);

                        await destination.UpsertItemStreamAsync(stream, pkBuilder.Build());
                        written++;
                    }
                    catch (Exception ex)
                    {
                        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown";
                        errors.Add($"id={id}: {ex.Message}");
                        _logger.LogError(ex, "Failed to migrate document id={Id} in {Container}", id, sourceName);
                    }
                }
                else
                {
                    written++;  // Dry-run: count as "would write".
                }
            }
        }

        return new { container = sourceName, destination = destinationName, status = "ok", read, written, skipped, errors };
    }

    private static async Task<HttpResponseData> WriteResponse(
        HttpRequestData req, HttpStatusCode status, string body)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(body);
        return response;
    }
}
