namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

/// <summary>
/// GET /api/status/{instanceId}
///
/// Thin wrapper over the Durable orchestration status so the browser can poll
/// through the SWA /api/* proxy instead of calling the Durable management
/// endpoint directly (which would require cross-origin access).
/// </summary>
public class StatusGet(ILoggerFactory loggerFactory, IConfiguration config, IAuditLogger auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StatusGet>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonConfig.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Function("GetOrchestrationStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId,
        CancellationToken cancellationToken)
    {
        // Require authentication in production — prevents unauthenticated callers
        // from polling orchestration output which may contain restored PHI.
        var authDisabled = config.GetValue<bool>("Auth:Disabled");
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);

        if (!authDisabled && identity is null)
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication required.", cancellationToken);
            return unauth;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("instanceId is required.", cancellationToken);
            return bad;
        }

        OrchestrationMetadata? metadata;
        try
        {
            metadata = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orchestration status for {InstanceId}", instanceId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to retrieve orchestration status.", cancellationToken);
            return error;
        }

        if (metadata is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Orchestration instance '{instanceId}' not found.", cancellationToken);
            return notFound;
        }

        // Ownership check — the TherapistName stored in the orchestration INPUT must
        // match the authenticated caller. This prevents one user from reading another
        // user's completed orchestration output (which contains restored PHI) even if
        // they obtained the instanceId through another channel.
        if (identity is not null)
        {
            var inputTherapist = TryExtractTherapistFromInput(metadata);
            if (inputTherapist is not null
                && !string.Equals(identity, inputTherapist, StringComparison.OrdinalIgnoreCase)
                && !ClaimsHelper.IsDemoRecord(inputTherapist, config))
            {
                auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "OrchestrationStatus",
                    resourceId: instanceId, detail: "Ownership check failed"));
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You are not authorized to access this orchestration status.", cancellationToken);
                return forbidden;
            }
        }

        FinalizeResult? output = null;
        if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed && metadata.SerializedOutput is not null)
        {
            try
            {
                output = JsonSerializer.Deserialize<FinalizeResult>(
                    metadata.SerializedOutput, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deserialize output for {InstanceId}", instanceId);
            }
        }

        var payload = new
        {
            instanceId = metadata.InstanceId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            output
        };

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);

        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read, "OrchestrationStatus",
            resourceId: instanceId));
        return ok;
    }

    /// <summary>
    /// Attempts to extract TherapistName from the serialised Durable orchestration INPUT
    /// (<see cref="TranscriptInput"/>). Returns null if the field is absent or unparseable.
    /// We read from the input (not the output) because it is always present regardless of
    /// orchestration runtime status, and it is set by the authenticated <c>DocumentationStart</c>
    /// caller, making it a trustworthy ownership anchor.
    /// </summary>
    private static string? TryExtractTherapistFromInput(OrchestrationMetadata metadata)
    {
        try
        {
            if (metadata.SerializedInput is null) return null;
            using var doc = JsonDocument.Parse(metadata.SerializedInput);
            if (doc.RootElement.TryGetProperty("therapistName", out var prop))
                return prop.GetString();
        }
        catch { /* ignore parse errors — treated as no ownership anchor */ }
        return null;
    }
}
