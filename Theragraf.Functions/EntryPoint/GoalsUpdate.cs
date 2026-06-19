namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Core.Helpers;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class GoalsUpdate(
    IGoalRepository    repository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GoalsUpdate>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>PATCH /api/goals/{clientId}/{goalId} — partial update a goal.</summary>
    [Function("UpdateGoal")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "goals/{clientId}/{goalId}")] HttpRequestData req,
        string clientId,
        string goalId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(goalId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("clientId and goalId are required.", cancellationToken);
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
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Goal",
                resourceId: $"{clientId}/{goalId}", detail: "ClientId namespace mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to update goals for this client.", cancellationToken);
            return forbidden;
        }

        UpdateGoalRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateGoalRequest>(
                req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return bad;
        }

        if (request is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is required.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("UpdateGoal clientId={ClientId} goalId={GoalId}", LogSanitizer.ClientId(clientId), goalId);

        try
        {
            var updated = await repository.UpdateAsync(clientId, goalId, request, cancellationToken);
            if (updated is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Goal '{goalId}' not found.", cancellationToken);
                return notFound;
            }

            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Write, "Goal",
                resourceId: $"{clientId}/{goalId}"));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(updated, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? "dev", AuditAction.Write, "Goal",
                resourceId: $"{clientId}/{goalId}", detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("updating goal", correlationId), 
                cancellationToken);
            return error;
        }
    }
}
