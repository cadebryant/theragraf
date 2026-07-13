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

/// <summary>GET /api/therapists/me — returns the authenticated therapist's profile.</summary>
public class TherapistProfileGet(
    ITherapistProfileRepository profileRepository,
    IConfiguration              config,
    ILoggerFactory              loggerFactory,
    IAuditLogger                auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TherapistProfileGet>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    [Function("GetTherapistProfile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "therapists/me")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!ClaimsHelper.IsAuthenticated(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication required.", cancellationToken);
            return unauth;
        }

        var therapistId = ClaimsHelper.GetTherapistId(req, config);
        var tenantId    = ClaimsHelper.GetTenantId(req.FunctionContext);
        var identity    = ClaimsHelper.GetTherapistIdentity(req, config);

        if (string.IsNullOrWhiteSpace(therapistId) || string.IsNullOrWhiteSpace(tenantId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Could not resolve therapist or tenant identity from token.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("GetTherapistProfile: therapistId={TherapistId} tenantId={TenantId}",
            therapistId, tenantId);

        TherapistProfileDocument? doc;
        try
        {
            doc = await profileRepository.GetAsync(tenantId, therapistId, cancellationToken);
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("retrieving therapist profile", correlationId),
                cancellationToken);
            return error;
        }

        if (doc is null)
        {
            auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read,
                "TherapistProfile", resourceId: therapistId, detail: "not-found"));
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("No profile found. Use PATCH /api/therapists/me to create one.",
                cancellationToken);
            return notFound;
        }

        var result = TherapistProfileResponse.FromDocument(doc, isConfigured: true);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Read,
            "TherapistProfile", resourceId: therapistId));
        return response;
    }
}
