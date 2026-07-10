namespace Theragraf.Functions.Services;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

/// <summary>
/// Azure Cosmos DB for NoSQL implementation of <see cref="ITenantRepository"/>.
/// Database: theragraf   Container: tenants   PartitionKey: /tenantId
/// </summary>
public class CosmosTenantRepository(
    CosmosClient cosmosClient,
    string databaseName,
    string containerName,
    ILogger<CosmosTenantRepository> logger) : ITenantRepository
{
    private readonly Container _container = cosmosClient.GetContainer(databaseName, containerName);

    public async Task<TenantDocument?> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<TenantDocument>(
                tenantId, new PartitionKey(tenantId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TenantDocument> UpsertAsync(TenantDocument tenant, CancellationToken cancellationToken = default)
    {
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        var response = await _container.UpsertItemAsync(
            tenant,
            new PartitionKey(tenant.TenantId),
            cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<TenantDocument> IncrementAiCallCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // Patch increment is atomic and avoids a read-modify-write race condition.
        var patchOps = new[]
        {
            PatchOperation.Increment("/aiCallsThisPeriod", 1),
            PatchOperation.Set("/updatedAt", DateTimeOffset.UtcNow)
        };

        try
        {
            var response = await _container.PatchItemAsync<TenantDocument>(
                tenantId,
                new PartitionKey(tenantId),
                patchOps,
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            logger.LogError(ex, "Failed to increment AI call count for tenant {TenantId}", tenantId);
            throw;
        }
    }
}
