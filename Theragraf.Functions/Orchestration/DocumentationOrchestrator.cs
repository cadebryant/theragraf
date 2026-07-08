namespace Theragraf.Functions.Orchestration;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;
using Theragraf.Functions.Logging;

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
        // CreateReplaySafeLogger is required here — a normal ILogger would log on every
        // Durable replay, producing duplicates. This wrapper suppresses replayed log calls.
        var logger = context.CreateReplaySafeLogger<DocumentationOrchestrator>();

        var input = context.GetInput<TranscriptInput>();

        logger.LogInformation("Orchestration started instanceId={InstanceId} client={ClientId}",
            context.InstanceId, LogSanitizer.ClientId(input?.ClientId));

        try
        {
            logger.LogInformation("Stage: Ingestion");
            var observation = await context.CallActivityAsync<ObservationResult>(
                "IngestionActivity", input, RetryOptions);

            logger.LogInformation("Stage: SoapGeneration");
            var soap = await context.CallActivityAsync<SoapNote>(
                "SoapActivity", observation, RetryOptions);

            logger.LogInformation("Stage: ComplianceValidation");
            var compliance = await context.CallActivityAsync<SoapNote>(
                "ComplianceActivity", new ComplianceActivityInput(soap, input!.NoteFormat), RetryOptions);

            logger.LogInformation("Stage: Finalization");
            var finalized = await context.CallActivityAsync<FinalizeResult>(
                "FinalizerActivity", new FinalizeInput(compliance, observation.RedactionMap, input!.NoteFormat));

            logger.LogInformation("Stage: BillingSuggestion");
            var cptCodes = await context.CallActivityAsync<IReadOnlyList<CptCode>>(
                "BillingActivity",
                new BillingActivityInput(compliance, input!.Discipline, input.SessionDurationMinutes, input.Setting, input.Payer),
                RetryOptions);

            logger.LogInformation("Stage: Icd10Coding");
            var icdCodes = await context.CallActivityAsync<IReadOnlyList<IcdCode>>(
                "Icd10Activity",
                new Icd10ActivityInput(compliance, input.Discipline, input.Demographics),
                RetryOptions);

            var result = finalized with { SuggestedCptCodes = cptCodes, SuggestedIcdCodes = icdCodes };

            logger.LogInformation("Stage: Persistence");
            await context.CallActivityAsync(
                "PersistActivity",
                new PersistActivityInput(input!, result, compliance, observation.RedactionMap),
                RetryOptions);

            logger.LogInformation("Orchestration completed instanceId={InstanceId} client={ClientId} cptCount={CptCount} icdCount={IcdCount}",
                context.InstanceId,
                LogSanitizer.ClientId(input?.ClientId),
                LogSanitizer.Count(cptCodes),
                LogSanitizer.Count(icdCodes));

            return result;
        }
        catch (TaskFailedException ex)
        {
            logger.LogError(ex, "Orchestration failed instanceId={InstanceId} client={ClientId} failedTask={TaskName}",
                context.InstanceId,
                LogSanitizer.ClientId(input?.ClientId),
                ex.TaskName);
            throw;
        }
    }
}
