namespace Theragraf.Functions.Services;

using Azure.Data.Tables;
using System.Text.Json;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

public class TableStorageSessionRepository : ISessionRepository
{
    private const string TableName = "SessionRecords";
    private readonly TableClient _client;

    public TableStorageSessionRepository(TableServiceClient tableServiceClient)
    {
        _client = tableServiceClient.GetTableClient(TableName);
        _client.CreateIfNotExists();
    }

    public async Task SaveAsync(SessionRecord record, CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(record.PartitionKey, record.RowKey)
        {
            { nameof(SessionRecord.TherapistName),         record.TherapistName },
            { nameof(SessionRecord.Discipline),            record.Discipline },
            { nameof(SessionRecord.SessionDurationMinutes), record.SessionDurationMinutes },
            { nameof(SessionRecord.SoapNoteJson),          record.SoapNoteJson },
            { nameof(SessionRecord.CptCodesJson),          record.CptCodesJson },
            { nameof(SessionRecord.IcdCodesJson),          record.IcdCodesJson },
            { nameof(SessionRecord.CreatedAt),             record.CreatedAt },
        };

        await _client.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionResponse>> GetByClientIdAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        var results = new List<SessionResponse>();

        await foreach (var entity in _client.QueryAsync<TableEntity>(
            e => e.PartitionKey == clientId, cancellationToken: cancellationToken))
        {
            results.Add(MapToResponse(entity));
        }

        return results.OrderByDescending(r => r.SessionDate).ToList();
    }

    public async Task<SessionResponse?> GetByClientIdAndDateAsync(
        string clientId, string rowKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetEntityAsync<TableEntity>(
                clientId, rowKey, cancellationToken: cancellationToken);
            return MapToResponse(response.Value);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static SessionResponse MapToResponse(TableEntity entity)
    {
        var soapNote    = JsonSerializer.Deserialize<SoapNote>(entity.GetString(nameof(SessionRecord.SoapNoteJson))    ?? "{}") ?? new SoapNote("", "", "", "");
        var cptCodes    = JsonSerializer.Deserialize<List<CptCode>>(entity.GetString(nameof(SessionRecord.CptCodesJson)) ?? "[]") ?? [];
        var icdCodes    = JsonSerializer.Deserialize<List<IcdCode>>(entity.GetString(nameof(SessionRecord.IcdCodesJson)) ?? "[]") ?? [];

        return new SessionResponse(
            ClientId:              entity.PartitionKey,
            SessionDate:           entity.RowKey,
            TherapistName:         entity.GetString(nameof(SessionRecord.TherapistName)) ?? "",
            Discipline:            entity.GetString(nameof(SessionRecord.Discipline)) ?? "",
            SessionDurationMinutes: entity.TryGetValue(nameof(SessionRecord.SessionDurationMinutes), out var dur) ? dur as int? : null,
            SoapNote:              soapNote,
            SuggestedCptCodes:     cptCodes,
            SuggestedIcdCodes:     icdCodes,
            CreatedAt:             entity.GetDateTimeOffset(nameof(SessionRecord.CreatedAt)) ?? DateTimeOffset.MinValue
        );
    }
}
