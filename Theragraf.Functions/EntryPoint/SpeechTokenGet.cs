namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// GET /api/speech-token
///
/// Exchanges the server-held Azure Speech API key for a short-lived authorization
/// token (10 minutes) and returns it alongside the configured region.
/// The browser uses this token directly with the Azure Speech SDK so the API key
/// never leaves the server.
/// </summary>
public class SpeechTokenGet(IConfiguration config, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SpeechTokenGet>();
    private static readonly HttpClient HttpClient = new();

    [Function("GetSpeechToken")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "speech-token")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // Require a valid JWT — prevents unauthenticated callers from burning Speech quota.
        // Bypassed automatically in local dev when Auth:Disabled=true (no principal is set,
        // but the auth-disabled config flag signals intentional local bypass).
        var authDisabled = config.GetValue<bool>("Auth:Disabled");
        if (!authDisabled && !req.FunctionContext.Items.ContainsKey("ClaimsPrincipal"))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication required.", cancellationToken);
            return unauth;
        }

        var region = config["AzureSpeech:Region"];
        var apiKey  = config["AzureSpeech:ApiKey"];

        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("AzureSpeech:Region or AzureSpeech:ApiKey is not configured.");
            var cfg = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await cfg.WriteStringAsync("Speech service is not configured.", cancellationToken);
            return cfg;
        }

        var tokenUrl = $"https://{region}.api.cognitive.microsoft.com/sts/v1.0/issuetoken";

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        tokenRequest.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

        HttpResponseMessage tokenResponse;
        try
        {
            tokenResponse = await HttpClient.SendAsync(tokenRequest, cancellationToken);
            tokenResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain Azure Speech token from {Url}", tokenUrl);
            var error = req.CreateResponse(HttpStatusCode.BadGateway);
            await error.WriteStringAsync("Failed to obtain speech token.", cancellationToken);
            return error;
        }

        var token = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        // Instruct clients not to cache — tokens expire after 10 minutes.
        ok.Headers.Add("Cache-Control", "no-store");
        await ok.WriteStringAsync(
            JsonSerializer.Serialize(new { token, region }),
            cancellationToken);

        return ok;
    }
}
