namespace Theragraf.Functions.EntryPoint;

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Services;

public class SessionsDelete(ISessionRepository repository, ILoggerFactory loggerFactory)
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

        bool deleted;
        try
        {
            deleted = await repository.DeleteAsync(clientId, sessionDate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSession failed for clientId={ClientId}", clientId);
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

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
