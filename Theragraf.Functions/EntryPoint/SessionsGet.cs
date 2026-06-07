namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

public class SessionsGet(ISessionRepository repository, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionsGet>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            _logger.LogError(ex, "GetSessionsByClient failed for clientId={ClientId}", clientId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while retrieving sessions.", cancellationToken);
            return error;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
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
            _logger.LogError(ex, "GetSessionByClientAndDate failed for clientId={ClientId}", clientId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while retrieving the session.", cancellationToken);
            return error;
        }

        if (session is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync(
                $"No session found for client '{clientId}' at '{sessionDate}'.", cancellationToken);
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(session, JsonOptions), cancellationToken);
        return response;
    }
}
