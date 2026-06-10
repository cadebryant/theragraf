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
        SoapNoteUpdate?                     redactedNote    = null;
        IReadOnlyDictionary<string, string> newRedactionMap = new Dictionary<string, string>();

        if (update.SoapNote is not null)
        {
            var (noteUpdate, map) = await RedactNoteAsync(update.SoapNote, cancellationToken);
            redactedNote    = noteUpdate;
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
    /// Runs one PII redaction pass over only the SOAP fields that were actually provided
    /// (non-null). Null fields are passed straight through as null so the repository can
    /// distinguish "caller omitted this field" from "caller set this field to empty".
    /// Placeholder numbering is kept consistent by joining all non-null sections in a
    /// fixed order with a unique separator before calling the redaction service once.
    /// </summary>
    private async Task<(SoapNoteUpdate NoteUpdate, IReadOnlyDictionary<string, string> Map)> RedactNoteAsync(
        SoapNoteUpdate input, CancellationToken cancellationToken)
    {
        // Build an ordered list of (fieldValue, isProvided) so we can reconstruct
        // which slots were null after splitting the redacted output.
        var slots = new[]
        {
            (input.Subjective, input.Subjective is not null),
            (input.Objective,  input.Objective  is not null),
            (input.Assessment, input.Assessment is not null),
            (input.Plan,       input.Plan       is not null),
        };

        // Only the non-null fields participate in the single redaction pass.
        var provided = slots
            .Where(s => s.Item2)
            .Select(s => s.Item1!)
            .ToArray();

        if (provided.Length == 0)
        {
            // Nothing to redact – return the (all-null) input unchanged.
            return (input, new Dictionary<string, string>());
        }

        var joined = string.Join(Separator, provided);
        var (redactedJoined, map) = await redaction.RedactAsync(joined);
        var parts = redactedJoined.Split(Separator);

        // Walk the slots in order, distributing redacted parts back only to
        // the slots that were provided; leave null slots as null.
        int partIndex = 0;
        var redactedSlots = new string?[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Item2)
                redactedSlots[i] = partIndex < parts.Length ? parts[partIndex++] : slots[i].Item1;
            // else leave redactedSlots[i] as null
        }

        var noteUpdate = new SoapNoteUpdate(
            Subjective: redactedSlots[0],
            Objective:  redactedSlots[1],
            Assessment: redactedSlots[2],
            Plan:       redactedSlots[3]
        );

        return (noteUpdate, map);
    }
}
