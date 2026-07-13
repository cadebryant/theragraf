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

/// <summary>PATCH /api/therapists/me — create or update the authenticated therapist's profile.</summary>
public class TherapistProfileUpdate(
    ITherapistProfileRepository profileRepository,
    IConfiguration              config,
    ILoggerFactory              loggerFactory,
    IAuditLogger                auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<TherapistProfileUpdate>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    [Function("UpdateTherapistProfile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "therapists/me")] HttpRequestData req,
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

        // Deserialize the request body.
        TherapistProfileUpdateRequest? update;
        try
        {
            var body = await req.ReadAsStringAsync();
            update = string.IsNullOrWhiteSpace(body)
                ? new TherapistProfileUpdateRequest()
                : JsonSerializer.Deserialize<TherapistProfileUpdateRequest>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "UpdateTherapistProfile: invalid JSON body");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return bad;
        }

        if (update is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body must not be null.", cancellationToken);
            return bad;
        }

        // Validate NPI format if provided.
        if (update.IndividualNpi is not null &&
            (update.IndividualNpi.Length != 10 || !update.IndividualNpi.All(char.IsDigit)))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("individualNpi must be exactly 10 digits.", cancellationToken);
            return bad;
        }

        _logger.LogInformation("UpdateTherapistProfile: therapistId={TherapistId} tenantId={TenantId}",
            therapistId, tenantId);

        // Load existing profile or create a new stub.
        TherapistProfileDocument? existing;
        try
        {
            existing = await profileRepository.GetAsync(tenantId, therapistId, cancellationToken);
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

        var doc = existing ?? new TherapistProfileDocument
        {
            Id           = therapistId,
            TherapistId  = therapistId,
            TenantId     = tenantId,
            CreatedAt    = DateTimeOffset.UtcNow,
        };

        // Apply partial update — only overwrite fields that were explicitly supplied.
        if (update.FirstName    is not null) doc.FirstName    = update.FirstName;
        if (update.LastName     is not null) doc.LastName     = update.LastName;
        if (update.Credentials  is not null) doc.Credentials  = update.Credentials;
        if (update.Discipline   is not null) doc.Discipline   = update.Discipline.Value;
        if (update.IndividualNpi is not null) doc.IndividualNpi = update.IndividualNpi;

        TherapistProfileDocument saved;
        try
        {
            saved = await profileRepository.UpsertAsync(doc, cancellationToken);
        }
        catch (Exception ex)
        {
            var correlationId = SafeErrorHelper.GenerateCorrelationId();
            _logger.LogError(ex, SafeErrorHelper.GetInternalLogDetail(ex, correlationId));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            error.Headers.Add("X-Correlation-ID", correlationId);
            await error.WriteStringAsync(
                SafeErrorHelper.GetSafeErrorMessage("saving therapist profile", correlationId),
                cancellationToken);
            return error;
        }

        var result   = TherapistProfileResponse.FromDocument(saved, isConfigured: true);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Write,
            "TherapistProfile", resourceId: therapistId));
        return response;
    }
}
