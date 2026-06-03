namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;

public class SessionsGet(ISessionRepository repository, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionsGet>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>GET /api/sessions/{clientId} — list all sessions for a client.</summary>
    [Function("GetSessionsByClient")]
    public async Task<HttpResponseData> GetByClient(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sessions/{clientId}")] HttpRequestData req,
        string clientId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetSessionsByClient called for clientId={ClientId}", clientId);

        var sessions = await repository.GetByClientIdAsync(clientId, cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(sessions, JsonOptions), cancellationToken);
        return response;
    }

    /// <summary>GET /api/sessions/{clientId}/{sessionDate} — get one specific session.</summary>
    [Function("GetSessionByClientAndDate")]
    public async Task<HttpResponseData> GetByClientAndDate(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "sessions/{clientId}/{sessionDate}")] HttpRequestData req,
        string clientId,
        string sessionDate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetSessionByClientAndDate called for clientId={ClientId} date={Date}",
            clientId, sessionDate);

        var session = await repository.GetByClientIdAndDateAsync(clientId, sessionDate, cancellationToken);

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
