namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;

public class StatsGet(ISessionRepository repository, IConfiguration config, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StatsGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

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

            var demoTherapist = config["Demo:TherapistName"];
            if (!string.IsNullOrWhiteSpace(demoTherapist) &&
                !string.Equals(therapistName, demoTherapist, StringComparison.OrdinalIgnoreCase))
            {
                var demoStats = await repository.GetTherapistStatsAsync(demoTherapist, cancellationToken);
                stats = MergeStats(stats, demoStats);
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
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

    private static TherapistStats MergeStats(TherapistStats caller, TherapistStats demo)
    {
        var totalSessions = caller.TotalSessions + demo.TotalSessions;
        var totalDuration = (caller.AverageSessionDurationMinutes * caller.TotalSessions)
                          + (demo.AverageSessionDurationMinutes   * demo.TotalSessions);
        var avgDuration = totalSessions > 0 ? totalDuration / totalSessions : 0.0;
        return caller with
        {
            TotalSessions                 = totalSessions,
            TotalClients                  = caller.TotalClients + demo.TotalClients,
            AverageSessionDurationMinutes = avgDuration,
            TotalBillableUnits            = caller.TotalBillableUnits + demo.TotalBillableUnits,
            SessionsByDiscipline          = MergeCounts(caller.SessionsByDiscipline, demo.SessionsByDiscipline),
            SessionsBySetting             = MergeCounts(caller.SessionsBySetting,    demo.SessionsBySetting),
            SessionsByPayer               = MergeCounts(caller.SessionsByPayer,      demo.SessionsByPayer),
            TopCptCodes                   = MergeCodes(caller.TopCptCodes,  demo.TopCptCodes),
            TopIcdCodes                   = MergeCodes(caller.TopIcdCodes,  demo.TopIcdCodes),
        };
    }

    private static IReadOnlyDictionary<string, int> MergeCounts(
        IReadOnlyDictionary<string, int> a,
        IReadOnlyDictionary<string, int> b)
    {
        var result = new Dictionary<string, int>(a);
        foreach (var (key, val) in b)
            result[key] = result.TryGetValue(key, out var existing) ? existing + val : val;
        return result;
    }

    private static IReadOnlyList<CodeFrequency> MergeCodes(
        IReadOnlyList<CodeFrequency> a,
        IReadOnlyList<CodeFrequency> b)
    {
        var merged = a.ToDictionary(c => c.Code, c => c);
        foreach (var code in b)
        {
            if (merged.TryGetValue(code.Code, out var existing))
                merged[code.Code] = existing with
                {
                    Count              = existing.Count + code.Count,
                    TotalBillableUnits = existing.TotalBillableUnits + code.TotalBillableUnits,
                };
            else
                merged[code.Code] = code;
        }
        return merged.Values.OrderByDescending(c => c.Count).Take(10).ToList();
    }
}
