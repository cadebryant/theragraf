namespace Theragraf.Functions.Middleware;

using System.Net;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

// Disambiguate from Microsoft.IdentityModel.Protocols.HttpRequestData
using HttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;

/// <summary>
/// Resolves the current tenant after <see cref="JwtAuthMiddleware"/> has validated the token.
///
/// Hosted path:
///   Reads the <c>tenantId</c> claim (configurable via <c>MultiTenant:TenantIdClaimType</c>,
///   defaults to <c>"tid"</c>) from the validated <see cref="ClaimsPrincipal"/>, looks up the
///   <see cref="TenantDocument"/> from Cosmos, verifies it is <see cref="TenantStatus.Active"/>,
///   and injects it into <c>FunctionContext.Items["Tenant"]</c>.
///
/// Self-hosted (BYOA) path:
///   When no <c>tenantId</c> claim is present (standard single-tenant Entra deployment),
///   a synthetic <see cref="TenantDocument"/> is constructed from configuration with
///   <see cref="TenantDocument.IsSynthetic"/> = <see langword="true"/> and unlimited quota.
///   Existing self-hosted deployments are completely unaffected.
///
/// Auth-disabled path (local dev):
///   When <c>Auth:Disabled=true</c>, a synthetic tenant is always used.
/// </summary>
public class TenantResolutionMiddleware(
    IConfiguration config,
    ITenantRepository tenantRepository,
    ILoggerFactory loggerFactory) : IFunctionsWorkerMiddleware
{
    /// <summary>Key used to store the resolved <see cref="TenantDocument"/> in <see cref="FunctionContext.Items"/>.</summary>
    public const string TenantContextKey = "Tenant";

    private readonly ILogger _logger = loggerFactory.CreateLogger<TenantResolutionMiddleware>();

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Only resolve tenant for HTTP-triggered functions.
        var httpContext = await context.GetHttpRequestDataAsync();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        // Auth-disabled (local dev) → synthetic tenant, skip all Cosmos lookups.
        if (config.GetValue<bool>("Auth:Disabled"))
        {
            context.Items[TenantContextKey] = BuildSyntheticTenant("local-dev");
            await next(context);
            return;
        }

        // Extract ClaimsPrincipal stored by JwtAuthMiddleware.
        if (!context.Items.TryGetValue("ClaimsPrincipal", out var raw) || raw is not ClaimsPrincipal principal)
        {
            // JwtAuthMiddleware should have already rejected the request; this is a safety net.
            await WriteError(context, httpContext, HttpStatusCode.Unauthorized, "Missing authentication principal.");
            return;
        }

        // Determine the claim type that carries the tenant identifier.
        // Standard Entra: "tid" (Entra tenant GUID).
        // External ID custom attribute: configurable (e.g. "extension_tenantId").
        var claimType = config["MultiTenant:TenantIdClaimType"] ?? "tid";
        var tenantIdClaim = principal.FindFirst(claimType)?.Value;

        if (string.IsNullOrWhiteSpace(tenantIdClaim))
        {
            // No tenantId claim → self-hosted BYOA path.
            // Use the Entra TenantId from config as the synthetic tenant identifier.
            var syntheticId = config["AzureAd:TenantId"] ?? "self-hosted";
            _logger.LogDebug("No '{ClaimType}' claim found; using synthetic self-hosted tenant '{TenantId}'.",
                claimType, syntheticId);
            context.Items[TenantContextKey] = BuildSyntheticTenant(syntheticId);
            await next(context);
            return;
        }

        // Hosted path — look up the TenantDocument from Cosmos.
        TenantDocument? tenant;
        try
        {
            tenant = await tenantRepository.GetAsync(tenantIdClaim, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve tenant '{TenantId}' from repository.", tenantIdClaim);
            await WriteError(context, httpContext, HttpStatusCode.ServiceUnavailable, "Tenant resolution failed.");
            return;
        }

        if (tenant is null)
        {
            _logger.LogWarning("Tenant '{TenantId}' not found in repository.", tenantIdClaim);
            await WriteError(context, httpContext, HttpStatusCode.Forbidden, "Tenant not found.");
            return;
        }

        if (tenant.Status != TenantStatus.Active)
        {
            _logger.LogWarning("Tenant '{TenantId}' is not active (status={Status}).", tenantIdClaim, tenant.Status);
            await WriteError(context, httpContext, HttpStatusCode.Forbidden,
                $"Tenant access is not available (status: {tenant.Status}).");
            return;
        }

        context.Items[TenantContextKey] = tenant;

        _logger.LogDebug("Resolved tenant '{TenantId}' ({OrganizationName}).",
            tenant.TenantId, tenant.OrganizationName);

        await next(context);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private TenantDocument BuildSyntheticTenant(string tenantId) => new()
    {
        Id                  = tenantId,
        TenantId            = tenantId,
        OrganizationName    = config["MultiTenant:SyntheticTenantName"] ?? "Self-Hosted",
        OrganizationType    = TenantOrganizationType.SoloPractitioner,
        Plan                = TenantPlan.Professional,
        MonthlyAiCallQuota  = null,   // unlimited for self-hosted
        AiCallsThisPeriod   = 0,
        BillingPeriodStart  = DateTimeOffset.UtcNow,
        Status              = TenantStatus.Active,
        CreatedAt           = DateTimeOffset.UtcNow,
        UpdatedAt           = DateTimeOffset.UtcNow,
        IsSynthetic         = true
    };

    private static async Task WriteError(FunctionContext context, HttpRequestData req,
        HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse();
        response.StatusCode = statusCode;
        await response.WriteStringAsync(message);

        context.GetInvocationResult().Value = response;
    }
}
