using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Services;

namespace Theragraf.Functions.Activities;

public class IngestionActivity(IPiiRedactionService piiRedactionService)
{
    [Function(nameof(IngestionActivity))]
    public async Task<ObservationResult> Run([ActivityTrigger] TranscriptInput input)
    {
        var (redactedText, redactionMap) = await piiRedactionService.RedactAsync(input.RawTranscript);

        return new ObservationResult(
            RedactedTranscript: redactedText,
            RedactionMap: redactionMap,
            TherapistName: input.TherapistName,
            ClientId: input.ClientId,
            SessionDate: input.SessionDate
        );
    }
}