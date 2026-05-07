namespace Theragraf.Functions.EntryPoint;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;

public class DocumentationStart
{
    private readonly ILogger _logger;
    private readonly DurableTaskClient _durableClient;

    public DocumentationStart(ILoggerFactory loggerFactory, DurableTaskClient durableClient)
    {
        _logger = loggerFactory.CreateLogger<DocumentationStart>();
        _durableClient = durableClient;
    }

    [Function("DocumentationStart")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var instanceId = await _durableClient.ScheduleNewOrchestrationInstanceAsync(
            "DocumentationOrchestrator", null!, cancellationToken);
        _logger.LogInformation($"Started orchestration with ID = {instanceId}");
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(instanceId);
        return response;
    }
}
