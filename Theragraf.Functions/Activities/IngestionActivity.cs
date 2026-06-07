using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Logging;
using Theragraf.Functions.Services;

namespace Theragraf.Functions.Activities;

public class IngestionActivity(IPiiRedactionService piiRedactionService, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<IngestionActivity>();

    [Function(nameof(IngestionActivity))]
    public async Task<ObservationResult> Run([ActivityTrigger] TranscriptInput input)
    {
        _logger.LogInformation("IngestionActivity started for client={ClientId} transcriptLength={Length}",
            LogSanitizer.ClientId(input.ClientId), LogSanitizer.TextLength(input.RawTranscript));
        try
        {
            var (redactedText, redactionMap) = await piiRedactionService.RedactAsync(input.RawTranscript);

            _logger.LogInformation("IngestionActivity completed for client={ClientId} placeholderCount={Count}",
                LogSanitizer.ClientId(input.ClientId), LogSanitizer.Count(redactionMap));

            return new ObservationResult(
                RedactedTranscript: redactedText,
                RedactionMap: redactionMap,
                TherapistName: input.TherapistName,
                ClientId: input.ClientId,
                SessionDate: input.SessionDate
            );
        }
        catch (Exception ex) when (ex is not IngestionException)
        {
            _logger.LogError(ex, "IngestionActivity failed for client={ClientId}",
                LogSanitizer.ClientId(input.ClientId));
            throw new IngestionException(ex.Message, ex);
        }
    }
}