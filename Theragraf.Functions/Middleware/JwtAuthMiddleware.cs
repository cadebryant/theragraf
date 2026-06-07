namespace Theragraf.Functions.Middleware;

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

// Disambiguate from Microsoft.IdentityModel.Protocols.HttpRequestData
using HttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;
using HttpResponseData = Microsoft.Azure.Functions.Worker.Http.HttpResponseData;

/// <summary>
/// Validates Entra ID Bearer tokens on all HTTP-triggered functions.
/// Bypassed locally when Auth:Disabled = true in local.settings.json.
/// </summary>
public class JwtAuthMiddleware(IConfiguration config, ILoggerFactory loggerFactory) : IFunctionsWorkerMiddleware
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<JwtAuthMiddleware>();

    // Cache the OIDC config manager so discovery metadata is only fetched once.
    private ConfigurationManager<OpenIdConnectConfiguration>? _oidcConfigManager;

    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Only authenticate HTTP-triggered functions.
        var httpContext = await context.GetHttpRequestDataAsync();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        // Local dev bypass — never set Auth:Disabled in production.
        if (config.GetValue<bool>("Auth:Disabled"))
        {
            _logger.LogWarning("Auth is DISABLED — skipping token validation. Do not use in production.");
            await next(context);
            return;
        }

        var tenantId = config["AzureAd:TenantId"]!;
        var clientId = config["AzureAd:ClientId"]!;

        var authHeader = httpContext.Headers
            .TryGetValues("Authorization", out var values)
                ? values.FirstOrDefault()
                : null;

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await WriteUnauthorized(context, httpContext, "Missing or invalid Authorization header.");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            var oidcConfig = await GetOidcConfigAsync(tenantId);

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuers             = [$"https://sts.windows.net/{tenantId}/",
                                            $"https://login.microsoftonline.com/{tenantId}/v2.0"],
                ValidateAudience         = true,
                ValidAudiences           = [$"api://{clientId}", clientId],
                ValidateLifetime         = true,
                IssuerSigningKeys        = oidcConfig.SigningKeys,
                ValidateIssuerSigningKey = true,
            };

            var principal = TokenHandler.ValidateToken(token, validationParams, out _);
            context.Items["ClaimsPrincipal"] = principal;

            _logger.LogInformation("Authenticated user: {Subject}",
                principal.FindFirst(ClaimConstants.Sub)?.Value ?? principal.Identity?.Name ?? "unknown");
        }
        catch (SecurityTokenExpiredException)
        {
            await WriteUnauthorized(context, httpContext, "Token has expired.");
            return;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("Token validation failed: {Message}", ex.Message);
            await WriteUnauthorized(context, httpContext, "Token validation failed.");
            return;
        }
        catch (SecurityTokenMalformedException ex)
        {
            _logger.LogWarning("Malformed token: {Message}", ex.Message);
            await WriteUnauthorized(context, httpContext, "Token validation failed.");
            return;
        }

        await next(context);
    }

    protected virtual async Task<OpenIdConnectConfiguration> GetOidcConfigAsync(string tenantId)
    {
        if (_oidcConfigManager is null)
        {
            var metadataAddress = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
            _oidcConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever());
        }

        return await _oidcConfigManager.GetConfigurationAsync();
    }

    private static async Task WriteUnauthorized(FunctionContext context, HttpRequestData req, string message)
    {
        var response = req.CreateResponse();
        response.StatusCode = HttpStatusCode.Unauthorized;
        response.Headers.Add("WWW-Authenticate", "Bearer");
        await response.WriteStringAsync(message);

        var invocationResult = context.GetInvocationResult();
        invocationResult.Value = response;
    }
}
