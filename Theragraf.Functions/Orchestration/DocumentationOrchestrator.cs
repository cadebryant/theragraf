namespace Theragraf.Functions.Orchestration;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Theragraf.Core.Models;

public class DocumentationOrchestrator
{
    [Function("DocumentationOrchestrator")]
    public async Task<FinalizeResult> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<TranscriptInput>();

        var observation  = await context.CallActivityAsync<ObservationResult>("IngestionActivity", input);
        var soap         = await context.CallActivityAsync<SoapNote>("SoapActivity", observation);
        var compliance   = await context.CallActivityAsync<SoapNote>("ComplianceActivity", soap);
        var finalized    = await context.CallActivityAsync<FinalizeResult>("FinalizerActivity",
                               new FinalizeInput(compliance, observation.RedactionMap));
        var cptCodes     = await context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity",
                               finalized.RestoredNote);

        return finalized with { SuggestedCptCodes = cptCodes };
    }
}
