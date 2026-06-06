namespace Theragraf.Functions.Orchestration;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;

public class DocumentationOrchestrator
{
    // 3 attempts, starting at 5 s, doubling each time, capped at 30 s.
    // Applied to every activity that makes external network calls.
    private static readonly TaskOptions RetryOptions = new(
        new RetryPolicy(
            maxNumberOfAttempts: 3,
            firstRetryInterval: TimeSpan.FromSeconds(5),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(30)));

    [Function("DocumentationOrchestrator")]
    public async Task<FinalizeResult> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<TranscriptInput>();

        var observation  = await context.CallActivityAsync<ObservationResult>("IngestionActivity",  input,       RetryOptions);
        var soap         = await context.CallActivityAsync<SoapNote>("SoapActivity",                observation, RetryOptions);
        var compliance   = await context.CallActivityAsync<SoapNote>("ComplianceActivity",          soap,        RetryOptions);
        var finalized    = await context.CallActivityAsync<FinalizeResult>("FinalizerActivity",
                               new FinalizeInput(compliance, observation.RedactionMap));
        var cptCodes     = await context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity",
                               new BillingActivityInput(finalized.RestoredNote, input!.Discipline, input.SessionDurationMinutes, input.Setting, input.Payer), RetryOptions);
        var icdCodes     = await context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity",
                               new Icd10ActivityInput(finalized.RestoredNote, input.Discipline), RetryOptions);

        var result = finalized with { SuggestedCptCodes = cptCodes, SuggestedIcdCodes = icdCodes };

        await context.CallActivityAsync("PersistActivity",
            new PersistActivityInput(input!, result, compliance, observation.RedactionMap), RetryOptions);

        return result;
    }
}
