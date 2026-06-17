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
using Theragraf.Functions.Logging;
using Theragraf.Functions.Services;

public class ClientDemographicsUpsert(
    IClientRepository repository,
    IConfiguration    config,
    ILoggerFactory    loggerFactory,
    IAuditLogger      auditLogger,
    IPromptInputHardeningService promptInputHardeningService)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ClientDemographicsUpsert>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>
    /// PUT /api/clients/{clientId} — create or replace the demographics / intake record.
    /// DOB (if supplied) is encrypted before persistence and never echoed back.
    /// </summary>
    [Function("UpsertClientDemographics")]
    public async Task<HttpResponseData> Upsert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "clients/{clientId}")] HttpRequestData req,
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
            auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "ClientDemographics",
                resourceId: clientId, detail: "ClientId namespace mismatch"));
            var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
            await forbidden.WriteStringAsync("You are not authorised to modify this client's record.", cancellationToken);
            return forbidden;
        }

        UpsertClientDemographicsRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpsertClientDemographicsRequest>(
                req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return bad;
        }

        if (request is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is required.", cancellationToken);
            return bad;
        }

        if (!promptInputHardeningService.TrySanitize(request, out request, out var hardeningError))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync(hardeningError ?? "Request content failed validation.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("UpsertClientDemographics clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            var saved = await repository.UpsertAsync(clientId, request, cancellationToken);

            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Write, "ClientDemographics",
                resourceId: clientId));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(saved, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertClientDemographics failed for clientId={ClientId}", clientId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred.", cancellationToken);
            return error;
        }
    }
}
