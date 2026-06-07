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

        if (input.SessionDate == default)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("SessionDate is required.", cancellationToken);
            return badRequest;
        }

        if (input.SessionDurationMinutes.HasValue &&
            (input.SessionDurationMinutes.Value <= 0 || input.SessionDurationMinutes.Value > 480))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync(
                "SessionDurationMinutes must be between 1 and 480.", cancellationToken);
            return badRequest;
        }

        string instanceId;
        try
        {
            instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                "DocumentationOrchestrator", input, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule orchestration for client {ClientId}", input.ClientId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while starting the documentation pipeline.", cancellationToken);
            return error;
        }

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
