namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;

public class DocumentationStart(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DocumentationStart>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("DocumentationStart")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        TranscriptInput? input;

        try
        {
            input = await JsonSerializer.DeserializeAsync<TranscriptInput>(
                req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Invalid request body: {Message}", ex.Message);
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return badRequest;
        }

        if (input is null ||
            string.IsNullOrWhiteSpace(input.RawTranscript) ||
            string.IsNullOrWhiteSpace(input.TherapistName) ||
            string.IsNullOrWhiteSpace(input.ClientId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync(
                "RawTranscript, TherapistName, and ClientId are required.", cancellationToken);
            return badRequest;
        }

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            "DocumentationOrchestrator", input, cancellationToken);

        _logger.LogInformation("Started orchestration {InstanceId} for client {ClientId}",
            instanceId, input.ClientId);

        var management = GetManagementPayload(instanceId, req, durableClient);

        var accepted = req.CreateResponse(HttpStatusCode.Accepted);
        if (!string.IsNullOrEmpty(management.StatusQueryGetUri))
            accepted.Headers.Add("Location", management.StatusQueryGetUri);
        accepted.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await accepted.WriteStringAsync(JsonSerializer.Serialize(new
        {
            instanceId,
            statusQueryGetUri    = management.StatusQueryGetUri,
            sendEventPostUri     = management.SendEventPostUri,
            terminatePostUri     = management.TerminatePostUri,
            purgeHistoryDeleteUri = management.PurgeHistoryDeleteUri
        }, JsonOptions), cancellationToken);

        return accepted;
    }

    protected virtual HttpManagementPayload GetManagementPayload(string instanceId, HttpRequestData req, DurableTaskClient durableClient)
        => durableClient.CreateHttpManagementPayload(instanceId, req);
}
