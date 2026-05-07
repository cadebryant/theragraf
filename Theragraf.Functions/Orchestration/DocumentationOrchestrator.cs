namespace Theragraf.Functions.Orchestration;

using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Theragraf.Core.Models;

public class DocumentationOrchestrator
{
    [Function("DocumentationOrchestrator")]
    public async Task<SoapNote> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<string>();
        var ingestion = await context.CallActivityAsync<string>("IngestionActivity", input);
        var soap = await context.CallActivityAsync<SoapNote>("SoapActivity", ingestion);
        var compliance = await context.CallActivityAsync<SoapNote>("ComplianceActivity", soap);
        var final = await context.CallActivityAsync<SoapNote>("FinalizerActivity", compliance);
        return final;
    }
}
