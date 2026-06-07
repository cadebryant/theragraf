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
            { nameof(SessionRecord.Setting),               record.Setting },
            { nameof(SessionRecord.Payer),                 record.Payer },
            { nameof(SessionRecord.SessionDurationMinutes), record.SessionDurationMinutes },
            { nameof(SessionRecord.RedactionMapJson),      record.RedactionMapJson },
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

    public async Task<bool> DeleteAsync(string clientId, string rowKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteEntityAsync(clientId, rowKey, cancellationToken: cancellationToken);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static SessionResponse MapToResponse(TableEntity entity)
    {
        var redactionMap = JsonSerializer.Deserialize<Dictionary<string, string>>(
            entity.GetString(nameof(SessionRecord.RedactionMapJson)) ?? "{}") ?? [];

        var soapNote    = JsonSerializer.Deserialize<SoapNote>(entity.GetString(nameof(SessionRecord.SoapNoteJson))    ?? "{}") ?? new SoapNote("", "", "", "");
        var cptCodes    = JsonSerializer.Deserialize<List<CptCode>>(entity.GetString(nameof(SessionRecord.CptCodesJson)) ?? "[]") ?? [];
        var icdCodes    = JsonSerializer.Deserialize<List<IcdCode>>(entity.GetString(nameof(SessionRecord.IcdCodesJson)) ?? "[]") ?? [];

        var restoredNote = new SoapNote(
            Subjective: Restore(soapNote.Subjective, redactionMap),
            Objective:  Restore(soapNote.Objective,  redactionMap),
            Assessment: Restore(soapNote.Assessment, redactionMap),
            Plan:       Restore(soapNote.Plan,        redactionMap)
        );

        return new SessionResponse(
            ClientId:              entity.PartitionKey,
            SessionDate:           entity.RowKey,
            TherapistName:         entity.GetString(nameof(SessionRecord.TherapistName)) ?? "",
            Discipline:            entity.GetString(nameof(SessionRecord.Discipline)) ?? "",
            Setting:               entity.GetString(nameof(SessionRecord.Setting)) ?? "",
            Payer:                 entity.GetString(nameof(SessionRecord.Payer)) ?? "",
            SessionDurationMinutes: entity.TryGetValue(nameof(SessionRecord.SessionDurationMinutes), out var dur) ? dur as int? : null,
            SoapNote:              restoredNote,
            SuggestedCptCodes:     cptCodes,
            SuggestedIcdCodes:     icdCodes,
            CreatedAt:             entity.GetDateTimeOffset(nameof(SessionRecord.CreatedAt)) ?? DateTimeOffset.MinValue
        );
    }

    private static string Restore(string text, IReadOnlyDictionary<string, string> map)
    {
        foreach (var (placeholder, original) in map)
            text = text.Replace(placeholder, original);
        return text;
    }
}
