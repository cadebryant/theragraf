namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

public record PersistActivityInput(
    TranscriptInput OriginalInput,
    FinalizeResult Result,
    SoapNote RedactedNote,
    IReadOnlyDictionary<string, string> RedactionMap
);

public class PersistActivity(ISessionRepository repository)
{
    [Function(nameof(PersistActivity))]
    public async Task Run([ActivityTrigger] PersistActivityInput input)
    {
        var record = new SessionRecord
        {
            PartitionKey          = input.OriginalInput.ClientId,
            RowKey                = input.OriginalInput.SessionDate
                                        .ToString("yyyy-MM-ddTHH-mm-ssZ"),
            TherapistName         = input.OriginalInput.TherapistName,
            Discipline            = input.OriginalInput.Discipline.ToString(),
            SessionDurationMinutes = input.OriginalInput.SessionDurationMinutes,
            RedactionMapJson       = JsonSerializer.Serialize(input.RedactionMap),
            SoapNoteJson          = JsonSerializer.Serialize(input.RedactedNote),
            CptCodesJson          = JsonSerializer.Serialize(input.Result.SuggestedCptCodes),
            IcdCodesJson          = JsonSerializer.Serialize(input.Result.SuggestedIcdCodes),
            CreatedAt             = DateTimeOffset.UtcNow,
        };

        await repository.SaveAsync(record);
    }
}
