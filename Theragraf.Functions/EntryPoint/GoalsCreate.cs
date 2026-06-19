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

public class GoalsCreate(
    IGoalRepository    repository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GoalsCreate>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>POST /api/goals/{clientId} — create a new treatment goal.</summary>
    [Function("CreateGoal")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "goals/{clientId}")] HttpRequestData req,
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
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Goal",
                resourceId: clientId, detail: "ClientId namespace mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to create goals for this client.", cancellationToken);
            return forbidden;
        }

        CreateGoalRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateGoalRequest>(
                req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return bad;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Title))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("title is required.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("CreateGoal clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            var goal = await repository.CreateAsync(clientId, request, cancellationToken);
            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Write, "Goal",
                resourceId: $"{clientId}/{goal.GoalId}"));

            var created = req.CreateResponse(HttpStatusCode.Created);
            created.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await created.WriteStringAsync(JsonSerializer.Serialize(goal, JsonOptions), cancellationToken);
            return created;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? "dev", AuditAction.Write, "Goal",
                resourceId: clientId, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("creating goal", correlationId), 
                cancellationToken);
            return error;
        }
    }
}
