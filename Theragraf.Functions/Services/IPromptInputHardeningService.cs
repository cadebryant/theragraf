using Theragraf.Core.Models;

namespace Theragraf.Functions.Services;

public interface IPromptInputHardeningService
{
    bool TrySanitize(TranscriptInput input, out TranscriptInput sanitized, out string? errorMessage);
    bool TrySanitize(ClientDemographicsSummary input, out ClientDemographicsSummary sanitized, out string? errorMessage);
    bool TrySanitize(UpsertClientDemographicsRequest input, out UpsertClientDemographicsRequest sanitized, out string? errorMessage);
}
