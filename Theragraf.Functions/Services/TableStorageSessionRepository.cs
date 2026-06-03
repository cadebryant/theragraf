namespace Theragraf.Functions.Services;

using Azure.Data.Tables;
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
            { nameof(SessionRecord.TherapistName),        record.TherapistName },
            { nameof(SessionRecord.Discipline),           record.Discipline },
            { nameof(SessionRecord.SessionDurationMinutes), record.SessionDurationMinutes },
            { nameof(SessionRecord.SoapNoteJson),         record.SoapNoteJson },
            { nameof(SessionRecord.CptCodesJson),         record.CptCodesJson },
            { nameof(SessionRecord.IcdCodesJson),         record.IcdCodesJson },
            { nameof(SessionRecord.CreatedAt),            record.CreatedAt },
        };

        await _client.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }
}
