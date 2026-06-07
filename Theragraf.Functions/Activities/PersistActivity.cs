namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Logging;

public record PersistActivityInput(
    TranscriptInput OriginalInput,
    FinalizeResult Result,
    SoapNote RedactedNote,
    IReadOnlyDictionary<string, string> RedactionMap
);

public class PersistActivity(ISessionRepository repository, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<PersistActivity>();

    [Function(nameof(PersistActivity))]
    public async Task Run([ActivityTrigger] PersistActivityInput input)
    {
        _logger.LogInformation("PersistActivity started for client={ClientId}",
            LogSanitizer.ClientId(input.OriginalInput.ClientId));
        try
        {
            var record = new SessionRecord
            {
                PartitionKey           = input.OriginalInput.ClientId,
                RowKey                 = input.OriginalInput.SessionDate
                                             .ToString("yyyy-MM-ddTHH-mm-ssZ"),
                TherapistName          = input.OriginalInput.TherapistName,
                Discipline             = input.OriginalInput.Discipline.ToString(),
                Setting                = input.OriginalInput.Setting.ToString(),
                Payer                  = input.OriginalInput.Payer.ToString(),
                SessionDurationMinutes = input.OriginalInput.SessionDurationMinutes,
                RedactionMapJson       = JsonSerializer.Serialize(input.RedactionMap),
                SoapNoteJson           = JsonSerializer.Serialize(input.RedactedNote),
                CptCodesJson           = JsonSerializer.Serialize(input.Result.SuggestedCptCodes),
                IcdCodesJson           = JsonSerializer.Serialize(input.Result.SuggestedIcdCodes),
                CreatedAt              = DateTimeOffset.UtcNow,
            };

            await repository.SaveAsync(record);

            _logger.LogInformation("PersistActivity completed for client={ClientId}",
                LogSanitizer.ClientId(input.OriginalInput.ClientId));
        }
        catch (Exception ex) when (ex is not PersistenceException)
        {
            _logger.LogError(ex, "PersistActivity failed for client={ClientId}",
                LogSanitizer.ClientId(input.OriginalInput.ClientId));
            throw new PersistenceException(ex.Message, ex);
        }
    }
}
