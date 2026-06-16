namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class ClientDemographicsGet(
    IClientRepository repository,
    IConfiguration    config,
    ILoggerFactory    loggerFactory,
    IAuditLogger      auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ClientDemographicsGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    /// <summary>GET /api/clients/{clientId} — return demographics/intake record for a client.</summary>
    [Function("GetClientDemographics")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/{clientId}")] HttpRequestData req,
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
            await forbidden.WriteStringAsync("You are not authorised to access this client's record.", cancellationToken);
            return forbidden;
        }

        _logger.LogInformation("GetClientDemographics clientId={ClientId}", LogSanitizer.ClientId(clientId));

        try
        {
            var record = await repository.GetAsync(clientId, cancellationToken);

            if (record is null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            auditLogger.Log(AuditEvent.Success(identity ?? "dev", AuditAction.Read, "ClientDemographics",
                resourceId: clientId));

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await ok.WriteStringAsync(JsonSerializer.Serialize(record, JsonOptions), cancellationToken);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetClientDemographics failed for clientId={ClientId}", clientId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred.", cancellationToken);
            return error;
        }
    }
}
