namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Helpers;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;
using Theragraf.Functions.Services;

public class DocumentationStart(
    ILoggerFactory loggerFactory,
    IConfiguration config,
    IAuditLogger auditLogger,
    IPromptInputHardeningService promptInputHardeningService)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DocumentationStart>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    [Function("DocumentationStart")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
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

        if (!promptInputHardeningService.TrySanitize(input, out input, out var hardeningError))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync(hardeningError ?? "Request content failed validation.", cancellationToken);
            return badRequest;
        }

        // Ownership check — the TherapistName in the request must match the JWT identity.
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        if (identity is not null
            && !string.Equals(identity, input.TherapistName, StringComparison.OrdinalIgnoreCase))
        {
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Session",
                resourceId: input.ClientId, detail: "TherapistName mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync(
                "TherapistName in the request does not match your authenticated identity.",
                cancellationToken);
            return forbidden;
        }

        // Namespace the clientId with an 8-char hex prefix derived from the therapist's
        // email so two different therapists who enter the same patient identifier do not
        // share a Cosmos partition or bleed into each other's statistics.
        // Demo records bypass namespacing so they remain shared across all users.
        // Auth-disabled local dev (identity == null) also bypasses to keep local records simple.
        var namespacedClientId = (identity is not null && !ClaimsHelper.IsDemoRecord(input.TherapistName, config))
            ? ClientIdHelper.Namespace(identity, input.ClientId)
            : input.ClientId;

        input = input with { ClientId = namespacedClientId };

        string instanceId;
        try
        {
            instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
                "DocumentationOrchestrator", input, cancellationToken);
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(input.TherapistName, AuditAction.Write, "Session",
                resourceId: input.ClientId, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("starting the documentation pipeline", correlationId), 
                cancellationToken);
            return error;
        }

        _logger.LogInformation("Started orchestration {InstanceId} for client {ClientId}",
            instanceId, input.ClientId);
        auditLogger.Log(AuditEvent.Success(input.TherapistName, AuditAction.Write, "Session",
            resourceId: input.ClientId, correlationId: instanceId, detail: "Orchestration started"));

        var management = GetManagementPayload(instanceId, req, durableClient);

        var accepted = req.CreateResponse(HttpStatusCode.Accepted);
        if (!string.IsNullOrEmpty(management.StatusQueryGetUri))
            accepted.Headers.Add("Location", management.StatusQueryGetUri);
        accepted.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await accepted.WriteStringAsync(JsonSerializer.Serialize(new
        {
            instanceId,
            clientId              = namespacedClientId,
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
