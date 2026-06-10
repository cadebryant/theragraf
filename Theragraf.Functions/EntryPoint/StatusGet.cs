namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;

/// <summary>
/// GET /api/status/{instanceId}
///
/// Thin wrapper over the Durable orchestration status so the browser can poll
/// through the SWA /api/* proxy instead of calling the Durable management
/// endpoint directly (which would require cross-origin access).
/// </summary>
public class StatusGet(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StatusGet>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Function("GetOrchestrationStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status/{instanceId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("instanceId is required.", cancellationToken);
            return bad;
        }

        OrchestrationMetadata? metadata;
        try
        {
            metadata = await durableClient.GetInstanceAsync(instanceId, getInputsAndOutputs: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orchestration status for {InstanceId}", instanceId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Failed to retrieve orchestration status.", cancellationToken);
            return error;
        }

        if (metadata is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Orchestration instance '{instanceId}' not found.", cancellationToken);
            return notFound;
        }

        FinalizeResult? output = null;
        if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed && metadata.SerializedOutput is not null)
        {
            try
            {
                output = JsonSerializer.Deserialize<FinalizeResult>(
                    metadata.SerializedOutput, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deserialize output for {InstanceId}", instanceId);
            }
        }

        var payload = new
        {
            instanceId = metadata.InstanceId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            output
        };

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
        return ok;
    }
}
