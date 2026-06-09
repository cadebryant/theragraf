namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;

public class StatsGet(ISessionRepository repository, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StatsGet>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// GET /api/stats/therapist/{therapistName}
    /// Returns aggregated session statistics for the given therapist across all clients.
    /// </summary>
    [Function("GetStatsByTherapist")]
    public async Task<HttpResponseData> GetByTherapist(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stats/therapist/{therapistName}")] HttpRequestData req,
        string therapistName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(therapistName))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("therapistName is required.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("GetStatsByTherapist therapistName={TherapistName}", therapistName);

        try
        {
            var stats = await repository.GetTherapistStatsAsync(therapistName, cancellationToken);
            var ok    = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(stats, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStatsByTherapist failed for therapistName={TherapistName}", therapistName);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while retrieving therapist stats.", cancellationToken);
            return error;
        }
    }

    /// <summary>
    /// GET /api/stats/client/{clientId}
    /// Returns aggregated session statistics for the given client.
    /// </summary>
    [Function("GetStatsByClient")]
    public async Task<HttpResponseData> GetByClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "stats/client/{clientId}")] HttpRequestData req,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("clientId is required.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("GetStatsByClient clientId={ClientId}", clientId);

        try
        {
            var stats = await repository.GetClientStatsAsync(clientId, cancellationToken);
            var ok    = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(stats, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStatsByClient failed for clientId={ClientId}", clientId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while retrieving client stats.", cancellationToken);
            return error;
        }
    }
}
