namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

/// <summary>
/// GET /api/tenant — returns organization name, plan, and AI quota usage for the
/// authenticated user's tenant. The <see cref="TenantDocument"/> is resolved by
/// <c>TenantResolutionMiddleware</c> and stored in <c>FunctionContext.Items</c>,
/// so no additional Cosmos read is required here.
/// </summary>
public class TenantGet(
    ILoggerFactory loggerFactory,
    IAuditLogger   auditLogger,
    IConfiguration config)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TenantGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    [Function("GetTenant")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tenant")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!ClaimsHelper.IsAuthenticated(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication required.", cancellationToken);
            return unauth;
        }

        var tenant   = ClaimsHelper.GetTenant(req.FunctionContext);
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);

        if (tenant is null)
        {
            _logger.LogWarning("GetTenant: no tenant in FunctionContext for identity={Identity}", identity);
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Tenant context not available.", cancellationToken);
            return notFound;
        }

        var result = TenantSummaryResponse.FromDocument(tenant);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read,
            "Tenant", resourceId: tenant.TenantId));
        return response;
    }
}
