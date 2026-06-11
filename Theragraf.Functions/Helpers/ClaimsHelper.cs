namespace Theragraf.Functions.Helpers;

using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Extracts the therapist identity from the validated <see cref="ClaimsPrincipal"/>
/// stored by <see cref="Middleware.JwtAuthMiddleware"/> in <c>FunctionContext.Items</c>.
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
            // Prefer preferred_username (UPN), fall back to name claim.
            return principal.FindFirst("preferred_username")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? principal.FindFirst("name")?.Value;
        }

        return null;
    }
}
