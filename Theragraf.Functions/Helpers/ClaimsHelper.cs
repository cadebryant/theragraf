namespace Theragraf.Functions.Helpers;

using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Theragraf.Core.Models;
using Theragraf.Functions.Middleware;

/// <summary>
/// Extracts the therapist identity from the validated <see cref="ClaimsPrincipal"/>
/// stored by <see cref="Middleware.JwtAuthMiddleware"/> in <c>FunctionContext.Items</c>.
/// Also provides demo-mode helpers for shared seed data.
/// </summary>
internal static class ClaimsHelper
{
    /// <summary>
    /// Returns the therapist name resolved from the JWT, or <see langword="null"/> when
    /// authentication is disabled (<c>Auth:Disabled=true</c>) or no principal is present.
    /// </summary>
    internal static string? GetTherapistIdentity(HttpRequestData req, IConfiguration config)
    {
        if (config.GetValue<bool>("Auth:Disabled"))
            return null;

        if (req.FunctionContext.Items.TryGetValue("ClaimsPrincipal", out var raw)
            && raw is ClaimsPrincipal principal)
        {
            // Prefer preferred_username (UPN/email), fall back to email claim, then display name.
            return principal.FindFirst("preferred_username")?.Value
                ?? principal.FindFirst("email")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? principal.FindFirst("name")?.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when a validated <see cref="ClaimsPrincipal"/> is present
    /// in the function context, regardless of which claims it carries. Accepts both user tokens
    /// and app-only (client credentials) tokens.
    /// </summary>
    internal static bool IsAuthenticated(HttpRequestData req) =>
        req.FunctionContext.Items.TryGetValue("ClaimsPrincipal", out var raw)
        && raw is ClaimsPrincipal principal
        && principal.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Extracts the user identity from the FunctionContext.Items dictionary.
    /// Used by rate limiting and other middleware to identify the current user.
    /// Returns null if no ClaimsPrincipal is present.
    /// </summary>
    internal static string? GetIdentity(IDictionary<string, object> items)
    {
        if (items.TryGetValue("ClaimsPrincipal", out var raw)
            && raw is ClaimsPrincipal principal)
        {
            // Prefer preferred_username (UPN/email), fall back to email claim, then display name.
            return principal.FindFirst("preferred_username")?.Value
                ?? principal.FindFirst("email")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? principal.FindFirst("name")?.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns the Entra Object ID (<c>oid</c> claim) of the authenticated user, or
    /// <see langword="null"/> when authentication is disabled or no principal is present.
    /// This is the stable, immutable identifier used as <c>TherapistProfileDocument.TherapistId</c>.
    /// </summary>
    internal static string? GetTherapistId(HttpRequestData req, IConfiguration config)
    {
        if (config.GetValue<bool>("Auth:Disabled"))
            return config["Auth:DevTherapistId"] ?? "dev-therapist-id";

        if (req.FunctionContext.Items.TryGetValue("ClaimsPrincipal", out var raw)
            && raw is ClaimsPrincipal principal)
        {
            return principal.FindFirst("oid")?.Value
                ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="therapistName"/> matches the
    /// configured demo therapist name (<c>Demo:TherapistName</c>). When this is true,
    /// ownership checks should be skipped so all users can browse shared demo records.
    /// Returns <see langword="false"/> when <c>Demo:TherapistName</c> is not configured.
    /// </summary>
    internal static bool IsDemoRecord(string? therapistName, IConfiguration config)
    {
        var demoName = config["Demo:TherapistName"];
        return !string.IsNullOrWhiteSpace(demoName)
            && string.Equals(therapistName, demoName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the resolved <see cref="TenantDocument"/> injected by
    /// <see cref="TenantResolutionMiddleware"/>, or <see langword="null"/> if not present.
    /// </summary>
    internal static TenantDocument? GetTenant(FunctionContext context)
    {
        return context.Items.TryGetValue(TenantResolutionMiddleware.TenantContextKey, out var raw)
            && raw is TenantDocument tenant
                ? tenant
                : null;
    }

    /// <summary>
    /// Returns the <c>tenantId</c> from the resolved <see cref="TenantDocument"/>,
    /// or <see langword="null"/> if the tenant has not been resolved.
    /// </summary>
    internal static string? GetTenantId(FunctionContext context) => GetTenant(context)?.TenantId;
}
