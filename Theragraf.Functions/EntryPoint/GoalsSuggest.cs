namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Helpers;
using Theragraf.Functions.Agents;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class GoalsSuggest(
    IGoalAgent         goalAgent,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GoalsSuggest>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>
    /// POST /api/goals/{clientId}/suggest
    /// Body: { "soapNote": { ... }, "discipline": "OccupationalTherapy" }
    /// Returns a list of AI-suggested SMART goals. The caller then chooses which to accept.
    /// </summary>
    [Function("SuggestGoals")]
    public async Task<HttpResponseData> Suggest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "goals/{clientId}/suggest")] HttpRequestData req,
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
                resourceId: clientId, detail: "ClientId namespace mismatch on suggest"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorised to request suggestions for this client.", cancellationToken);
            return forbidden;
        }

        GoalSuggestRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<GoalSuggestRequest>(
                req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return bad;
        }

        if (request?.SoapNote is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("soapNote is required.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("SuggestGoals clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            var suggestions = await goalAgent.SuggestGoalsAsync(
                request.SoapNote, request.Discipline, cancellationToken: cancellationToken);

            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Read, "GoalSuggest",
                resourceId: clientId, detail: $"Generated {suggestions.Count} suggestions"));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(suggestions, JsonOptions), cancellationToken);
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
                SafeErrorHelper.GetSafeErrorMessage("suggesting goals", correlationId), 
                cancellationToken);
            return error;
        }
    }

    /// <summary>Request body for the suggest endpoint.</summary>
    private sealed record GoalSuggestRequest(
        [property: JsonPropertyName("soapNote")]   SoapNote          SoapNote,
        [property: JsonPropertyName("discipline")] TherapyDiscipline Discipline = TherapyDiscipline.OccupationalTherapy
    );
}
