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

public class SessionsGet(
    ISessionRepository repository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory,
    IAuditLogger       auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionsGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>
    /// GET /api/sessions
    /// Returns the caseload overview (distinct clients + last session date) for the
    /// authenticated therapist. Identity is resolved from the JWT claim.
    /// </summary>
    [Function("GetCaseload")]
    public async Task<HttpResponseData> GetCaseload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        if (identity is null)
        {
            if (config.GetValue<bool>("Auth:Disabled"))
                identity = config["Auth:DevIdentity"] ?? "dev-therapist@localhost";
            else
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Authentication is required.", cancellationToken);
                return unauthorized;
            }
        }

        _logger.LogInformation("GetCaseload therapist={TherapistName}", identity);

        try
        {
            // Fire the caller's caseload and the demo caseload in parallel when demo mode
            // is active, halving the Cosmos round-trips compared to two sequential awaits.
            var demoTherapist = config["Demo:TherapistName"];
            var fetchDemo = !string.IsNullOrWhiteSpace(demoTherapist) &&
                            !string.Equals(identity, demoTherapist, StringComparison.OrdinalIgnoreCase);

            var summaryTask = repository.GetCaseloadAsync(identity, cancellationToken);
            var demoTask    = fetchDemo
                ? repository.GetCaseloadAsync(demoTherapist!, cancellationToken)
                : Task.FromResult<Core.Models.CaseloadSummary>(new Core.Models.CaseloadSummary(string.Empty, []));

            await Task.WhenAll(summaryTask, demoTask);

            var summary = summaryTask.Result;
            if (fetchDemo)
            {
                var merged = summary.Clients
                    .Concat(demoTask.Result.Clients)
                    .GroupBy(c => c.ClientId)
                    .Select(g => g.OrderByDescending(c => c.LastSessionDate).First())
                    .OrderByDescending(c => c.LastSessionDate)
                    .ToList();
                summary = new Core.Models.CaseloadSummary(summary.TherapistName, merged);
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(summary, JsonOptions), cancellationToken);
            auditLogger.Log(AuditEvent.Success(identity, AuditAction.Read, "Caseload"));
            return ok;
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.Read, "Caseload", 
                detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving the caseload", correlationId), 
                cancellationToken);
            return error;
        }
    }

    /// <summary>
    /// GET /api/sessions/{clientId}
    /// Query params: pageSize, continuationToken, discipline, therapist, payer,
    ///               dateFrom (ISO-8601), dateTo (ISO-8601), sortBy, sortOrder (asc|desc)
    /// </summary>
    [Function("GetSessionsByClient")]
    public async Task<HttpResponseData> GetByClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{clientId}")] HttpRequestData req,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("clientId is required.", cancellationToken);
            return badRequest;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        if (!int.TryParse(query["pageSize"], out var pageSize) || pageSize < 1)
            pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var continuationToken = query["continuationToken"];

        DateTimeOffset? dateFrom = DateTimeOffset.TryParse(query["dateFrom"], out var df) ? df : null;
        DateTimeOffset? dateTo   = DateTimeOffset.TryParse(query["dateTo"],   out var dt) ? dt : null;

        var options = new SessionQueryOptions(
            Discipline:  query["discipline"],
            Therapist:   query["therapist"],
            Payer:       query["payer"],
            DateFrom:    dateFrom,
            DateTo:      dateTo,
            SortBy:      query["sortBy"]    ?? "sessionDate",
            SortOrder:   query["sortOrder"] ?? "desc"
        );

        // Ownership check — if the JWT is present, the caller must own this client.
        // Demo records (TherapistName == Demo:TherapistName) are readable by anyone.
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        var requestedTherapist = options.Therapist;
        if (identity is not null
            && requestedTherapist is not null
            && !string.Equals(identity, requestedTherapist, StringComparison.OrdinalIgnoreCase)
            && !ClaimsHelper.IsDemoRecord(requestedTherapist, config))
        {
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "SessionList",
                resourceId: clientId, detail: "Therapist filter mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to filter by a different therapist.", cancellationToken);
            return forbidden;
        }

        // When the caller is authenticated but has not passed an explicit therapist filter,
        // scope the query to their own sessions only.
        if (identity is not null && options.Therapist is null)
            options = options with { Therapist = identity };

        _logger.LogInformation(
            "GetSessionsByClient clientId={ClientId} pageSize={PageSize} sortBy={SortBy} sortOrder={SortOrder}",
            clientId, pageSize, options.SortBy, options.SortOrder);

        PagedResult<SessionResponse> result;
        try
        {
            result = await repository.GetByClientIdPagedAsync(clientId, pageSize, continuationToken, options, cancellationToken);
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            auditLogger.Log(AuditEvent.Failure(identity ?? "anonymous", AuditAction.Read, "SessionList",
                resourceId: clientId, detail: SafeErrorHelper.GetAuditLogDetail(ex, correlationId)));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving sessions", correlationId), 
                cancellationToken);
            return error;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read, "SessionList",
            resourceId: clientId));
        return response;
    }

    /// <summary>GET /api/sessions/{clientId}/{sessionDate} — get one specific session.</summary>
    [Function("GetSessionByClientAndDate")]
    public async Task<HttpResponseData> GetByClientAndDate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{clientId}/{sessionDate}")] HttpRequestData req,
        string clientId,
        string sessionDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("clientId is required.", cancellationToken);
            return badRequest;
        }

        if (!DateTimeOffset.TryParseExact(sessionDate, "yyyy-MM-ddTHH-mm-ssZ",
                null, System.Globalization.DateTimeStyles.AssumeUniversal, out _))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("sessionDate must be in yyyy-MM-ddTHH-mm-ssZ format.", cancellationToken);
            return badRequest;
        }

        _logger.LogInformation("GetSessionByClientAndDate called for clientId={ClientId} date={Date}",
            clientId, sessionDate);

        SessionResponse? session;
        try
        {
            session = await repository.GetByClientIdAndDateAsync(clientId, sessionDate, cancellationToken);
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving the session", correlationId), 
                cancellationToken);
            return error;
        }

        if (session is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync(
                $"No session found for client '{clientId}' at '{sessionDate}'.", cancellationToken);
            return notFound;
        }

        // Ownership check — the session must belong to the authenticated therapist.
        // Demo records (TherapistName == Demo:TherapistName) are readable by anyone.
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        if (identity is not null
            && !string.Equals(identity, session.TherapistName, StringComparison.OrdinalIgnoreCase)
            && !ClaimsHelper.IsDemoRecord(session.TherapistName, config))
        {
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Session",
                resourceId: $"{clientId}/{sessionDate}", detail: "Ownership check failed"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorized to access this session.", cancellationToken);
            return forbidden;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(session, JsonOptions), cancellationToken);
        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read, "Session",
            resourceId: $"{clientId}/{sessionDate}"));
        return response;
    }
}