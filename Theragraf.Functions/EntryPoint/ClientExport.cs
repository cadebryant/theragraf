namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Helpers;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

/// <summary>
/// GET /api/clients/{clientId}/export
///
/// Returns every ePHI record held for a single client: demographics, session notes
/// (with PII restored), and treatment goals.
///
/// Satisfies the HIPAA §164.524 right-of-access requirement that covered entities
/// must be able to produce all records about a patient on request.
/// </summary>
public class ClientExport(
    IClientRepository  clientRepository,
    ISessionRepository sessionRepository,
    IGoalRepository    goalRepository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ClientExport>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    [Function("ExportClientData")]
    public async Task<HttpResponseData> Export(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{clientId}/export")] HttpRequestData req,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("clientId is required.", cancellationToken);
            return bad;
        }

        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        if (identity is null && !config.GetValue<bool>("Auth:Disabled"))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication is required.", cancellationToken);
            return unauth;
        }

        if (identity is not null && !ClientIdHelper.IsOwner(identity, clientId))
        {
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "ClientExport",
                resourceId: clientId, detail: "ClientId namespace mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to export this client's records.", cancellationToken);
            return forbidden;
        }

        _logger.LogInformation("ExportClientData clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            // Fetch all three record types in parallel to minimize latency.
            var demographicsTask = clientRepository.GetAsync(clientId, cancellationToken);
            var sessionsTask     = sessionRepository.GetByClientIdAsync(clientId, cancellationToken);
            var goalsTask        = goalRepository.GetByClientIdAsync(clientId, cancellationToken);

            await Task.WhenAll(demographicsTask, sessionsTask, goalsTask);

            var export = new ClientExportResponse(
                ClientId:      clientId,
                ExportedAt:    DateTimeOffset.UtcNow,
                ExportedBy:    identity ?? "dev",
                Demographics:  demographicsTask.Result,
                Sessions:      sessionsTask.Result,
                Goals:         goalsTask.Result);

            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Read, "ClientExport",
                resourceId: clientId,
                detail: $"Exported {export.Sessions.Count} sessions, {export.Goals.Count} goals"));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            // Tell clients not to cache — export contains live ePHI.
            ok.Headers.Add("Cache-Control", "no-store");
            await ok.WriteStringAsync(JsonSerializer.Serialize(export, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? "dev", AuditAction.Read, "ClientExport",
                resourceId: clientId, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("exporting client data", correlationId),
                cancellationToken);
            return error;
        }
    }
}
