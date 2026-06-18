namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;
using Theragraf.Core.Helpers;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class GoalsGet(
    IGoalRepository    repository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GoalsGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>GET /api/goals/{clientId} — return all goals for a client.</summary>
    [Function("GetGoals")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "goals/{clientId}")] HttpRequestData req,
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

        // Ownership: the namespace prefix of the clientId must match the therapist's own prefix.
        if (identity is not null && !ClientIdHelper.IsOwner(identity, clientId))
        {
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Goal",
                resourceId: clientId, detail: "ClientId namespace mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorised to access this client's goals.", cancellationToken);
            return forbidden;
        }

        _logger.LogInformation("GetGoals clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            var goals = await repository.GetByClientIdAsync(clientId, cancellationToken);
            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Read, "Goal",
                resourceId: clientId, detail: $"Fetched {goals.Count} goals"));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(goals, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? "dev", AuditAction.Read, "Goal",
                resourceId: clientId, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving goals", correlationId), 
                cancellationToken);
            return error;
        }
    }
}
