namespace Theragraf.Functions.EntryPoint;

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class GoalsDelete(
    IGoalRepository    repository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GoalsDelete>();

    /// <summary>DELETE /api/goals/{clientId}/{goalId} — delete a treatment goal.</summary>
    [Function("DeleteGoal")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "goals/{clientId}/{goalId}")] HttpRequestData req,
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
            await forbidden.WriteStringAsync("You are not authorised to delete goals for this client.", cancellationToken);
            return forbidden;
        }

        _logger.LogInformation("DeleteGoal clientId={ClientId} goalId={GoalId}", LogSanitizer.ClientId(clientId), goalId);

        try
        {
            var deleted = await repository.DeleteAsync(clientId, goalId, cancellationToken);
            if (!deleted)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"Goal '{goalId}' not found.", cancellationToken);
                return notFound;
            }

            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Delete, "Goal",
                resourceId: $"{clientId}/{goalId}"));
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteGoal failed for clientId={ClientId} goalId={GoalId}", clientId, goalId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred.", cancellationToken);
            return error;
        }
    }
}
