namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

public class SessionsUpdate(
    ISessionRepository   repository,
    IPiiRedactionService redaction,
    ILoggerFactory       loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SessionsUpdate>();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Field separator used when joining SOAP sections into a single redaction pass.
    // ASCII Unit Separator (0x1F) is safe against collision with clinical text.
    private const char Separator = '\x1F';

    /// <summary>
    /// PATCH /api/sessions/{clientId}/{sessionDate}
    /// Accepts a partial <see cref="SessionUpdateRequest"/>. Any omitted field is left unchanged.
    /// SOAP note fields must be supplied with PII restored (as the therapist sees them).
    /// The server re-runs PII redaction before persisting so storage stays HIPAA-clean.
    /// Returns 200 OK with the updated session (PII restored), or 404 if not found.
    /// </summary>
    [Function("UpdateSession")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "sessions/{clientId}/{sessionDate}")] HttpRequestData req,
        string clientId,
        string sessionDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            var r = req.CreateResponse(HttpStatusCode.BadRequest);
            await r.WriteStringAsync("clientId is required.", cancellationToken);
            return r;
        }

        if (!DateTimeOffset.TryParseExact(sessionDate, "yyyy-MM-ddTHH-mm-ssZ",
                null, System.Globalization.DateTimeStyles.AssumeUniversal, out _))
        {
            var r = req.CreateResponse(HttpStatusCode.BadRequest);
            await r.WriteStringAsync("sessionDate must be in yyyy-MM-ddTHH-mm-ssZ format.", cancellationToken);
            return r;
        }

        SessionUpdateRequest? update;
        try
        {
            update = await JsonSerializer.DeserializeAsync<SessionUpdateRequest>(
                req.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            var r = req.CreateResponse(HttpStatusCode.BadRequest);
            await r.WriteStringAsync("Request body is not valid JSON.", cancellationToken);
            return r;
        }

        if (update is null)
        {
            var r = req.CreateResponse(HttpStatusCode.BadRequest);
            await r.WriteStringAsync("Request body is required.", cancellationToken);
            return r;
        }

        _logger.LogInformation(
            "UpdateSession clientId={ClientId} date={Date} hasSoapUpdate={HasSoap} hasCptUpdate={HasCpt} hasIcdUpdate={HasIcd}",
            clientId, sessionDate,
            update.SoapNote is not null,
            update.SuggestedCptCodes is not null,
            update.SuggestedIcdCodes is not null);

        // Re-run PII redaction on the incoming SOAP note if any fields were changed.
        SoapNote?                           redactedNote    = null;
        IReadOnlyDictionary<string, string> newRedactionMap = new Dictionary<string, string>();

        if (update.SoapNote is not null)
        {
            var (note, map) = await RedactNoteAsync(update.SoapNote, cancellationToken);
            redactedNote    = note;
            newRedactionMap = map;
        }

        SessionResponse? result;
        try
        {
            result = await repository.UpdateAsync(
                clientId, sessionDate,
                redactedNote, newRedactionMap,
                update.SuggestedCptCodes,
                update.SuggestedIcdCodes,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateSession failed for clientId={ClientId}", clientId);
            var r = req.CreateResponse(HttpStatusCode.InternalServerError);
            await r.WriteStringAsync("An unexpected error occurred while updating the session.", cancellationToken);
            return r;
        }

        if (result is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync(
                $"No session found for client '{clientId}' at '{sessionDate}'.", cancellationToken);
            return notFound;
        }

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(result, JsonOptions), cancellationToken);
        return ok;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Joins all non-null SOAP fields into a single string, runs one PII redaction pass,
    /// then splits the result back into the same fields so placeholder numbering is
    /// consistent across all sections.
    /// </summary>
    private async Task<(SoapNote Note, IReadOnlyDictionary<string, string> Map)> RedactNoteAsync(
        SoapNoteUpdate input, CancellationToken cancellationToken)
    {
        // Collect fields in a fixed order; null means "no change" but we still need a
        // placeholder slot so we can split on separator count later.
        var sections = new[]
        {
            input.Subjective ?? string.Empty,
            input.Objective  ?? string.Empty,
            input.Assessment ?? string.Empty,
            input.Plan       ?? string.Empty,
        };

        var joined = string.Join(Separator, sections);
        var (redactedJoined, map) = await redaction.RedactAsync(joined);

        var parts = redactedJoined.Split(Separator);

        // Guard: if redaction somehow collapsed separators, fall back gracefully.
        var redactedNote = new SoapNote(
            Subjective: parts.Length > 0 ? parts[0] : sections[0],
            Objective:  parts.Length > 1 ? parts[1] : sections[1],
            Assessment: parts.Length > 2 ? parts[2] : sections[2],
            Plan:       parts.Length > 3 ? parts[3] : sections[3]
        );

        return (redactedNote, map);
    }
}
