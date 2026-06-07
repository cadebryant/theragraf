namespace Theragraf.Functions.Services;

using System.Text;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

/// <summary>
/// Azure Cosmos DB for NoSQL implementation of <see cref="ISessionRepository"/>.
/// Database: theragraf   Container: sessions   PartitionKey: /clientId
/// </summary>
public class CosmosSessionRepository : ISessionRepository
{
    private readonly Container _container;

    public CosmosSessionRepository(CosmosClient cosmosClient, string databaseName, string containerName)
    {
        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task SaveAsync(SessionRecord record, CancellationToken cancellationToken = default)
    {
        var redactionMap = string.IsNullOrEmpty(record.RedactionMapJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(record.RedactionMapJson) ?? [];

        var soapNote = string.IsNullOrEmpty(record.SoapNoteJson)
            ? new SoapNote("", "", "", "")
            : System.Text.Json.JsonSerializer.Deserialize<SoapNote>(record.SoapNoteJson) ?? new SoapNote("", "", "", "");

        var cptCodes = string.IsNullOrEmpty(record.CptCodesJson)
            ? new List<CptCode>()
            : System.Text.Json.JsonSerializer.Deserialize<List<CptCode>>(record.CptCodesJson) ?? [];

        var icdCodes = string.IsNullOrEmpty(record.IcdCodesJson)
            ? new List<IcdCode>()
            : System.Text.Json.JsonSerializer.Deserialize<List<IcdCode>>(record.IcdCodesJson) ?? [];

        var doc = new SessionDocument
        {
            Id                    = record.RowKey,
            ClientId              = record.PartitionKey,
            TherapistName         = record.TherapistName,
            Discipline            = record.Discipline,
            Setting               = record.Setting,
            Payer                 = record.Payer,
            SessionDurationMinutes = record.SessionDurationMinutes,
            RedactionMap          = redactionMap,
            SoapNote              = soapNote,
            SuggestedCptCodes     = cptCodes,
            SuggestedIcdCodes     = icdCodes,
            CreatedAt             = record.CreatedAt,
        };

        await _container.UpsertItemAsync(doc, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
    }

    // ── Read (unpaged) ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SessionResponse>> GetByClientIdAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId ORDER BY c.id DESC")
            .WithParameter("@clientId", clientId);

        return await ExecuteQueryAsync(query, clientId, cancellationToken);
    }

    public async Task<SessionResponse?> GetByClientIdAndDateAsync(
        string clientId, string rowKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<SessionDocument>(
                rowKey, new PartitionKey(clientId), cancellationToken: cancellationToken);
            return MapToResponse(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // ── Read (paged with filter/sort) ─────────────────────────────────────────

    public async Task<PagedResult<SessionResponse>> GetByClientIdPagedAsync(
        string clientId,
        int pageSize,
        string? continuationToken,
        SessionQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SessionQueryOptions();

        var (sql, parameters) = BuildQuery(clientId, options);

        var queryDef = new QueryDefinition(sql);
        foreach (var (name, value) in parameters)
            queryDef = queryDef.WithParameter(name, value);

        // Decode opaque base64 continuation token.
        string? rawToken = null;
        if (!string.IsNullOrEmpty(continuationToken))
        {
            try { rawToken = Encoding.UTF8.GetString(Convert.FromBase64String(continuationToken)); }
            catch (FormatException) { /* invalid token → first page */ }
        }

        var requestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(clientId),
            MaxItemCount = pageSize,
        };

        using var iterator = _container.GetItemQueryIterator<SessionDocument>(
            queryDef, continuationToken: rawToken, requestOptions: requestOptions);

        var items = new List<SessionResponse>();
        string? nextRawToken = null;

        if (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
                items.Add(MapToResponse(doc));
            nextRawToken = page.ContinuationToken;
        }

        string? nextToken = string.IsNullOrEmpty(nextRawToken)
            ? null
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(nextRawToken));

        return new PagedResult<SessionResponse>(
            Items:             items,
            PageSize:          pageSize,
            HasMore:           nextToken is not null,
            ContinuationToken: nextToken
        );
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<bool> DeleteAsync(string clientId, string rowKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.DeleteItemAsync<SessionDocument>(
                rowKey, new PartitionKey(clientId), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // ── Query builder ─────────────────────────────────────────────────────────

    internal static (string Sql, List<(string Name, object Value)> Parameters) BuildQuery(
        string clientId, SessionQueryOptions options)
    {
        var sb = new StringBuilder("SELECT * FROM c WHERE c.clientId = @clientId");
        var parameters = new List<(string, object)> { ("@clientId", clientId) };

        if (!string.IsNullOrWhiteSpace(options.Discipline))
        {
            sb.Append(" AND c.discipline = @discipline");
            parameters.Add(("@discipline", options.Discipline));
        }

        if (!string.IsNullOrWhiteSpace(options.Therapist))
        {
            sb.Append(" AND c.therapistName = @therapist");
            parameters.Add(("@therapist", options.Therapist));
        }

        if (!string.IsNullOrWhiteSpace(options.Payer))
        {
            sb.Append(" AND c.payer = @payer");
            parameters.Add(("@payer", options.Payer));
        }

        if (options.DateFrom.HasValue)
        {
            sb.Append(" AND c.id >= @dateFrom");
            parameters.Add(("@dateFrom", options.DateFrom.Value.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ")));
        }

        if (options.DateTo.HasValue)
        {
            sb.Append(" AND c.id <= @dateTo");
            parameters.Add(("@dateTo", options.DateTo.Value.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ")));
        }

        var sortField = options.SortBy?.ToLowerInvariant() switch
        {
            "therapistname" or "therapist" => "c.therapistName",
            "discipline"                   => "c.discipline",
            "setting"                      => "c.setting",
            "payer"                        => "c.payer",
            "duration"                     => "c.sessionDurationMinutes",
            "createdat"                    => "c.createdAt",
            _                              => "c.id",   // default: sessionDate
        };

        var sortDir = string.Equals(options.SortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC" : "DESC";

        sb.Append($" ORDER BY {sortField} {sortDir}");

        return (sb.ToString(), parameters);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static SessionResponse MapToResponse(SessionDocument doc)
    {
        var restoredNote = new SoapNote(
            Subjective: Restore(doc.SoapNote.Subjective, doc.RedactionMap),
            Objective:  Restore(doc.SoapNote.Objective,  doc.RedactionMap),
            Assessment: Restore(doc.SoapNote.Assessment, doc.RedactionMap),
            Plan:       Restore(doc.SoapNote.Plan,        doc.RedactionMap)
        );

        return new SessionResponse(
            ClientId:              doc.ClientId,
            SessionDate:           doc.Id,
            TherapistName:         doc.TherapistName,
            Discipline:            doc.Discipline,
            Setting:               doc.Setting,
            Payer:                 doc.Payer,
            SessionDurationMinutes: doc.SessionDurationMinutes,
            SoapNote:              restoredNote,
            SuggestedCptCodes:     doc.SuggestedCptCodes,
            SuggestedIcdCodes:     doc.SuggestedIcdCodes,
            CreatedAt:             doc.CreatedAt
        );
    }

    private static string Restore(string text, IReadOnlyDictionary<string, string> map)
    {
        foreach (var (placeholder, original) in map)
            text = text.Replace(placeholder, original);
        return text;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<SessionResponse>> ExecuteQueryAsync(
        QueryDefinition query, string clientId, CancellationToken cancellationToken)
    {
        var results = new List<SessionResponse>();
        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) };

        using var iterator = _container.GetItemQueryIterator<SessionDocument>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            foreach (var doc in page)
                results.Add(MapToResponse(doc));
        }

        return results;
    }
}
