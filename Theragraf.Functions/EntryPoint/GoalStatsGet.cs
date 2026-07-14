namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Helpers;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

/// <summary>
/// Goal progress statistics endpoints.
///   GET /api/goals/stats/client/{clientId}      — per-client goal breakdown
///   GET /api/goals/stats/therapist/{therapistName} — aggregate across all clients
/// </summary>
public class GoalStatsGet(
    IGoalRepository    goalRepository,
    ISessionRepository sessionRepository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GoalStatsGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    // ── Per-client ────────────────────────────────────────────────────────────

    [Function("GetGoalStatsByClient")]
    public async Task<HttpResponseData> GetByClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "goals/stats/client/{clientId}")] HttpRequestData req,
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
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "GoalStats",
                resourceId: clientId, detail: "ClientId namespace mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to access goal stats for this client.", cancellationToken);
            return forbidden;
        }

        _logger.LogInformation("GetGoalStatsByClient clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            var stats = await goalRepository.GetGoalStatsAsync(clientId, cancellationToken);
            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Read, "GoalStats",
                resourceId: clientId));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(stats, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? "dev", AuditAction.Read, "GoalStats",
                resourceId: clientId, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving goal statistics", correlationId),
                cancellationToken);
            return error;
        }
    }

    // ── Therapist aggregate ───────────────────────────────────────────────────

    [Function("GetGoalStatsByTherapist")]
    public async Task<HttpResponseData> GetByTherapist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "goals/stats/therapist/{therapistName}")] HttpRequestData req,
        string therapistName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(therapistName))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("therapistName is required.", cancellationToken);
            return bad;
        }

        var identity = ClaimsHelper.GetTherapistIdentity(req, config);

        // Ownership: callers may only query their own therapist stats.
        if (identity is not null
            && !string.Equals(identity, therapistName, StringComparison.OrdinalIgnoreCase)
            && !ClaimsHelper.IsDemoRecord(therapistName, config))
        {
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "GoalStats",
                resourceId: therapistName, detail: "Ownership check failed"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to view goal statistics for another therapist.", cancellationToken);
            return forbidden;
        }

        _logger.LogInformation("GetGoalStatsByTherapist therapistName={TherapistName}", therapistName);

        try
        {
            // Resolve the therapist's client list from the session caseload.
            var caseload   = await sessionRepository.GetCaseloadAsync(therapistName, cancellationToken);
            var clientIds  = caseload.Clients.Select(c => c.ClientId).ToList();

            var stats = await goalRepository.GetGoalStatsForTherapistAsync(therapistName, clientIds, cancellationToken);
            auditLogger.Log(AuditEvent.Success(identity ?? therapistName, AuditAction.Read, "GoalStats",
                resourceId: therapistName));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(stats, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? therapistName, AuditAction.Read, "GoalStats",
                resourceId: therapistName, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving therapist goal statistics", correlationId),
                cancellationToken);
            return error;
        }
    }
}
