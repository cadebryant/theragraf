namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Helpers;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

/// <summary>GET /api/providers/{providerId} — returns group practice info for a provider
/// that belongs to the caller's tenant.</summary>
public class ProviderGet(
    IProviderRepository providerRepository,
    IConfiguration      config,
    ILoggerFactory      loggerFactory,
    IAuditLogger        auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ProviderGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    [Function("GetProvider")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "providers/{providerId}")] HttpRequestData req,
        string providerId,
        CancellationToken cancellationToken)
    {
        if (!ClaimsHelper.IsAuthenticated(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication required.", cancellationToken);
            return unauth;
        }

        var tenantId = ClaimsHelper.GetTenantId(req.FunctionContext);
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Could not resolve tenant identity from token.", cancellationToken);
            return bad;
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("providerId is required.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("GetProvider: tenantId={TenantId} providerId={ProviderId}",
            tenantId, providerId);

        ProviderDocument? doc;
        try
        {
            doc = await providerRepository.GetAsync(tenantId, providerId, cancellationToken);
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving provider", correlationId),
                cancellationToken);
            return error;
        }

        if (doc is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync(
                $"No provider found with id '{providerId}'.", cancellationToken);
            return notFound;
        }

        var result = ProviderResponse.FromDocument(doc);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read,
            "Provider", resourceId: providerId));
        return response;
    }
}
