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
    private readonly Container                _container;
    private readonly IRedactionMapEncryption  _encryption;

    public CosmosSessionRepository(
        CosmosClient            cosmosClient,
        string                  databaseName,
        string                  containerName,
        IRedactionMapEncryption encryption)
    {
        _container  = cosmosClient.GetContainer(databaseName, containerName);
        _encryption = encryption;
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

        // Encrypt the redaction map when Key Vault is configured; otherwise store plaintext.
        string?                      encryptedBlob    = null;
        Dictionary<string, string>?  plainRedactionMap = null;

        if (_encryption.IsEnabled)
        {
            var plainJson = System.Text.Json.JsonSerializer.Serialize(redactionMap);
            encryptedBlob = _encryption.Encrypt(plainJson);
        }
        else
        {
            plainRedactionMap = redactionMap;
        }

        var doc = new SessionDocument
        {
            Id                       = record.RowKey,
            ClientId                 = record.PartitionKey,
            TherapistName            = record.TherapistName,
            Discipline               = record.Discipline,
            Setting                  = record.Setting,
            Payer                    = record.Payer,
            SessionDurationMinutes   = record.SessionDurationMinutes,
            RedactionMap             = plainRedactionMap,
            EncryptedRedactionMap    = encryptedBlob,
            RedactionMapIsEncrypted  = _encryption.IsEnabled,
            SoapNote                 = soapNote,
            SuggestedCptCodes        = cptCodes,
            SuggestedIcdCodes        = icdCodes,
            CreatedAt                = record.CreatedAt,
        };

        await _container.UpsertItemAsync(doc, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
    }

    // ── Read (unpaged) ────────────────────────────────────────────────────────

    // GetItemLinqQueryable has its own LINQ serializer that does NOT inherit
    // CosmosClientOptions.SerializerOptions. Passing CosmosLinqSerializerOptions
    // explicitly ensures generated SQL uses camelCase field names to match the stored documents.
    private static readonly CosmosLinqSerializerOptions _linqSerializerOptions = new()
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<SessionResponse>> GetByClientIdAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) };

        var iterator = _container
            .GetItemLinqQueryable<SessionDocument>(
                requestOptions:       requestOptions,
                linqSerializerOptions: _linqSerializerOptions)
            .Where(d => d.ClientId == clientId)
            .OrderByDescending(d => d.Id)
            .ToFeedIterator();

        return await DrainIteratorAsync(iterator, cancellationToken);
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

    // ── Update ────────────────────────────────────────────────────────────────

    public async Task<SessionResponse?> UpdateAsync(
        string                              clientId,
        string                              rowKey,
        SoapNote?                           redactedNote,
        IReadOnlyDictionary<string, string> newRedactionMap,
        IReadOnlyList<CptCode>?             cptCodes,
        IReadOnlyList<IcdCode>?             icdCodes,
        CancellationToken                   cancellationToken = default)
    {
        SessionDocument doc;
        try
        {
            var response = await _container.ReadItemAsync<SessionDocument>(
                rowKey, new PartitionKey(clientId), cancellationToken: cancellationToken);
            doc = response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        // Apply SOAP note changes
        if (redactedNote is not null)
            doc.SoapNote = redactedNote;

        // Apply code changes
        if (cptCodes is not null)
            doc.SuggestedCptCodes = cptCodes.ToList();
        if (icdCodes is not null)
            doc.SuggestedIcdCodes = icdCodes.ToList();

        // Persist the new redaction map (re-encrypt if Key Vault is active)
        if (_encryption.IsEnabled)
        {
            var plainJson = System.Text.Json.JsonSerializer.Serialize(newRedactionMap);
            doc.EncryptedRedactionMap  = _encryption.Encrypt(plainJson);
            doc.RedactionMapIsEncrypted = true;
            doc.RedactionMap           = null;
        }
        else
        {
            doc.RedactionMap            = new Dictionary<string, string>(newRedactionMap);
            doc.EncryptedRedactionMap   = null;
            doc.RedactionMapIsEncrypted = false;
        }

        await _container.UpsertItemAsync(doc, new PartitionKey(clientId), cancellationToken: cancellationToken);

        return MapToResponse(doc);
    }

    // ── Query builder ─────────────────────────────────────────────────────────

    internal static (string Sql, List<(string Name, object Value)> Parameters) BuildQuery(
        string clientId, SessionQueryOptions options)
    {
        var sb         = new StringBuilder(CosmosSessionQueries.BaseSelect);
        var parameters = new List<(string, object)> { ("@clientId", clientId) };

        if (!string.IsNullOrWhiteSpace(options.Discipline))
        {
            sb.Append(CosmosSessionQueries.FilterDiscipline);
            parameters.Add(("@discipline", options.Discipline));
        }

        if (!string.IsNullOrWhiteSpace(options.Therapist))
        {
            sb.Append(CosmosSessionQueries.FilterTherapist);
            parameters.Add(("@therapist", options.Therapist));
        }

        if (!string.IsNullOrWhiteSpace(options.Payer))
        {
            sb.Append(CosmosSessionQueries.FilterPayer);
            parameters.Add(("@payer", options.Payer));
        }

        if (options.DateFrom.HasValue)
        {
            sb.Append(CosmosSessionQueries.FilterDateFrom);
            parameters.Add(("@dateFrom", options.DateFrom.Value.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ")));
        }

        if (options.DateTo.HasValue)
        {
            sb.Append(CosmosSessionQueries.FilterDateTo);
            parameters.Add(("@dateTo", options.DateTo.Value.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ")));
        }

        sb.Append(CosmosSessionQueries.OrderByClause(options));

        return (sb.ToString(), parameters);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private SessionResponse MapToResponse(SessionDocument doc)
    {
        // Resolve the redaction map: decrypt if flagged, fall back to plain dict.
        Dictionary<string, string> redactionMap;
        if (doc.RedactionMapIsEncrypted && !string.IsNullOrEmpty(doc.EncryptedRedactionMap))
        {
            var plainJson = _encryption.Decrypt(doc.EncryptedRedactionMap);
            redactionMap  = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(plainJson)
                            ?? [];
        }
        else
        {
            redactionMap = doc.RedactionMap ?? [];
        }

        var restoredNote = new SoapNote(
            Subjective: Restore(doc.SoapNote.Subjective, redactionMap),
            Objective:  Restore(doc.SoapNote.Objective,  redactionMap),
            Assessment: Restore(doc.SoapNote.Assessment, redactionMap),
            Plan:       Restore(doc.SoapNote.Plan,        redactionMap)
        );

        return new SessionResponse(
            ClientId:               doc.ClientId,
            SessionDate:            doc.Id,
            TherapistName:          doc.TherapistName,
            Discipline:             doc.Discipline,
            Setting:                doc.Setting,
            Payer:                  doc.Payer,
            SessionDurationMinutes: doc.SessionDurationMinutes,
            SoapNote:               restoredNote,
            SuggestedCptCodes:      doc.SuggestedCptCodes,
            SuggestedIcdCodes:      doc.SuggestedIcdCodes,
            CreatedAt:              doc.CreatedAt
        );
    }

    private static string Restore(string text, IReadOnlyDictionary<string, string> map)
    {
        foreach (var (placeholder, original) in map)
            text = text.Replace(placeholder, original);
        return text;
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    public async Task<TherapistStats> GetTherapistStatsAsync(
        string therapistName, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(CosmosSessionQueries.StatsProjectionByTherapist)
            .WithParameter("@therapistName", therapistName);

        var docs = await DrainStatsIteratorAsync(
            _container.GetItemQueryIterator<SessionStatsProjection>(query), cancellationToken);

        var clients       = docs.Select(d => d.ClientId).Distinct().Count();
        var durations     = docs.Where(d => d.SessionDurationMinutes.HasValue)
                                .Select(d => d.SessionDurationMinutes!.Value).ToList();
        var avgDuration   = durations.Count > 0 ? durations.Average() : 0.0;

        return new TherapistStats(
            TherapistName:                therapistName,
            TotalSessions:                docs.Count,
            TotalClients:                 clients,
            AverageSessionDurationMinutes: Math.Round(avgDuration, 1),
            TotalBillableUnits:           docs.Sum(d => d.SuggestedCptCodes.Sum(c => c.BillableUnits)),
            SessionsByDiscipline:         GroupCount(docs, d => d.Discipline),
            SessionsBySetting:            GroupCount(docs, d => d.Setting),
            SessionsByPayer:              GroupCount(docs, d => d.Payer),
            TopCptCodes:                  TopCodes(docs, d => d.SuggestedCptCodes),
            TopIcdCodes:                  TopIcdCodes(docs)
        );
    }

    public async Task<ClientStats> GetClientStatsAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) };
        var query = new QueryDefinition(CosmosSessionQueries.StatsProjectionByClient)
            .WithParameter("@clientId", clientId);

        var docs = await DrainStatsIteratorAsync(
            _container.GetItemQueryIterator<SessionStatsProjection>(query, requestOptions: requestOptions),
            cancellationToken);

        var durations   = docs.Where(d => d.SessionDurationMinutes.HasValue)
                              .Select(d => d.SessionDurationMinutes!.Value).ToList();
        var avgDuration = durations.Count > 0 ? durations.Average() : 0.0;

        // Session dates are stored as the document id in yyyy-MM-ddTHH-mm-ssZ format.
        // Lexicographic min/max gives us first/last reliably.
        DateTimeOffset? first = null, last = null;
        if (docs.Count > 0)
        {
            var sorted = docs.Select(d => d.Id).OrderBy(id => id).ToList();
            first = ParseSessionDate(sorted.First());
            last  = ParseSessionDate(sorted.Last());
        }

        return new ClientStats(
            ClientId:                     clientId,
            TotalSessions:                docs.Count,
            AverageSessionDurationMinutes: Math.Round(avgDuration, 1),
            TotalBillableUnits:           docs.Sum(d => d.SuggestedCptCodes.Sum(c => c.BillableUnits)),
            FirstSessionDate:             first,
            LastSessionDate:              last,
            SessionsByTherapist:          GroupCount(docs, d => d.TherapistName),
            SessionsByDiscipline:         GroupCount(docs, d => d.Discipline),
            SessionsBySetting:            GroupCount(docs, d => d.Setting),
            SessionsByPayer:              GroupCount(docs, d => d.Payer),
            TopCptCodes:                  TopCodes(docs, d => d.SuggestedCptCodes),
            TopIcdCodes:                  TopIcdCodes(docs)
        );
    }

    // ── Stats helpers ─────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, int> GroupCount(
        IEnumerable<SessionStatsProjection> docs, Func<SessionStatsProjection, string> key) =>
        docs.GroupBy(key)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

    private static IReadOnlyList<CodeFrequency> TopCodes(
        IEnumerable<SessionStatsProjection> docs,
        Func<SessionStatsProjection, IEnumerable<CptCode>> selector,
        int topN = 10) =>
        docs.SelectMany(selector)
            .GroupBy(c => c.Code)
            .Select(g => new CodeFrequency(
                Code:               g.Key,
                Description:        g.First().Description,
                Count:              g.Count(),
                TotalBillableUnits: g.Sum(c => c.BillableUnits)))
            .OrderByDescending(f => f.Count)
            .Take(topN)
            .ToList();

    private static IReadOnlyList<CodeFrequency> TopIcdCodes(
        IEnumerable<SessionStatsProjection> docs, int topN = 10) =>
        docs.SelectMany(d => d.SuggestedIcdCodes)
            .GroupBy(c => c.Code)
            .Select(g => new CodeFrequency(
                Code:               g.Key,
                Description:        g.First().Description,
                Count:              g.Count(),
                TotalBillableUnits: 0))
            .OrderByDescending(f => f.Count)
            .Take(topN)
            .ToList();

    private static DateTimeOffset? ParseSessionDate(string id)
    {
        if (DateTimeOffset.TryParseExact(id, "yyyy-MM-ddTHH-mm-ssZ",
                null, System.Globalization.DateTimeStyles.AssumeUniversal, out var result))
            return result;
        return null;
    }

    private static async Task<List<SessionStatsProjection>> DrainStatsIteratorAsync(
        FeedIterator<SessionStatsProjection> iterator, CancellationToken cancellationToken)
    {
        var results = new List<SessionStatsProjection>();
        using (iterator)
        {
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }
        }
        return results;
    }

    /// <summary>
    /// Lightweight Cosmos projection used only for stats aggregation.
    /// Omits redaction map fields to avoid loading PHI.
    /// </summary>
    private sealed class SessionStatsProjection
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("clientId")]
        public string ClientId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("therapistName")]
        public string TherapistName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("discipline")]
        public string Discipline { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("setting")]
        public string Setting { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("payer")]
        public string Payer { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("sessionDurationMinutes")]
        public int? SessionDurationMinutes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("suggestedCptCodes")]
        public List<CptCode> SuggestedCptCodes { get; set; } = [];

        [System.Text.Json.Serialization.JsonPropertyName("suggestedIcdCodes")]
        public List<IcdCode> SuggestedIcdCodes { get; set; } = [];
    }

    private async Task<IReadOnlyList<SessionResponse>> DrainIteratorAsync(
        FeedIterator<SessionDocument> iterator, CancellationToken cancellationToken)
    {
        var results = new List<SessionResponse>();

        using (iterator)
        {
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var doc in page)
                    results.Add(MapToResponse(doc));
            }
        }

        return results;
    }
}
