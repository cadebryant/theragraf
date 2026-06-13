namespace Theragraf.Functions.EntryPoint;

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

public class SessionsDelete(ISessionRepository repository, IConfiguration config, ILoggerFactory loggerFactory, IAuditLogger auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionsDelete>();

    /// <summary>DELETE /api/sessions/{clientId}/{sessionDate} — delete a specific session.</summary>
    [Function("DeleteSession")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sessions/{clientId}/{sessionDate}")] HttpRequestData req,
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

        _logger.LogInformation("DeleteSession called for clientId={ClientId} date={Date}", clientId, sessionDate);

        // Ownership check — fetch the session and verify the JWT identity matches TherapistName.
        // Demo records (TherapistName == Demo:TherapistName) are deletable by anyone.
        var identity = ClaimsHelper.GetTherapistIdentity(req, config);
        if (identity is not null)
        {
            var existing = await repository.GetByClientIdAndDateAsync(clientId, sessionDate, cancellationToken);
            if (existing is not null
                && !string.Equals(identity, existing.TherapistName, StringComparison.OrdinalIgnoreCase)
                && !ClaimsHelper.IsDemoRecord(existing.TherapistName, config))
            {
                auditLogger.Log(AuditEvent.Failure(identity, AuditAction.AccessDenied, "Session",
                    resourceId: $"{clientId}/{sessionDate}", detail: "Ownership check failed"));
                var forbidden = req.CreateResponse(HttpStatusCode.Forbidden);
                await forbidden.WriteStringAsync("You are not authorised to delete this session.", cancellationToken);
                return forbidden;
            }
        }

        bool deleted;
        try
        {
            deleted = await repository.DeleteAsync(clientId, sessionDate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSession failed for clientId={ClientId}", clientId);
            auditLogger.Log(AuditEvent.Failure(identity ?? "anonymous", AuditAction.Delete, "Session",
                resourceId: $"{clientId}/{sessionDate}", detail: ex.Message));
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("An unexpected error occurred while deleting the session.", cancellationToken);
            return error;
        }

        if (!deleted)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync(
                $"No session found for client '{clientId}' at '{sessionDate}'.", cancellationToken);
            return notFound;
        }

        auditLogger.Log(AuditEvent.Success(identity ?? "anonymous", AuditAction.Delete, "Session",
            resourceId: $"{clientId}/{sessionDate}"));
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
