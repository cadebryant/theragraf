namespace Theragraf.Functions.EntryPoint;

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class SessionsRestore(ISessionRepository repository, IConfiguration config, ILoggerFactory loggerFactory, IAuditLogger auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionsRestore>();

    /// <summary>POST /api/sessions/{clientId}/{sessionDate}/restore — restore a soft-deleted session.</summary>
    [Function("RestoreSession")]
    public async Task<HttpResponseData> Restore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions/{clientId}/{sessionDate}/restore")] HttpRequestData req,
        string clientId,
        string sessionDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("clientId is required.", cancellationToken);
            return badRequest;
        }

        if (!DateTimeOffset.TryParseExact(sessionDate, "yyyy-MM-ddTHH-mm-ssZ",
                null, System.Globalization.DateTimeStyles.AssumeUniversal, out _))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("sessionDate must be in yyyy-MM-ddTHH-mm-ssZ format.", cancellationToken);
            return badRequest;
        }

        _logger.LogInformation("RestoreSession called for clientId={ClientId} date={Date}", clientId, sessionDate);

        // Ownership check — fetch the session (including deleted) and verify identity.
        // For restore operations, we need to check against the original therapist who created it.
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        if (identity is not null)
        {
            var existing = await repository.GetByClientIdAndDateAsync(clientId, sessionDate, cancellationToken);
            if (existing is not null
                && !string.Equals(identity, existing.TherapistName, StringComparison.OrdinalIgnoreCase)
                && !ClaimsHelper.IsDemoRecord(existing.TherapistName, config))
            {
                auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Session",
                    resourceId: $"{clientId}/{sessionDate}", detail: "Ownership check failed for restore"));
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You are not authorized to restore this session.", cancellationToken);
                return forbidden;
            }
        }

        bool restored;
        try
        {
            restored = await repository.RestoreAsync(clientId, sessionDate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RestoreSession failed for clientId={ClientId}", clientId);
            auditLogger.Log(AuditEvent.Failure(identity ?? "anonymous", AuditAction.Write, "Session",
                resourceId: $"{clientId}/{sessionDate}", detail: $"Restore failed: {ex.Message}"));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while restoring the session.", cancellationToken);
            return error;
        }

        if (!restored)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync(
                $"No deleted session found for client '{clientId}' at '{sessionDate}', or the session is already active.", cancellationToken);
            return notFound;
        }

        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Write, "Session",
            resourceId: $"{clientId}/{sessionDate}", detail: "Session restored from soft-delete"));
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
