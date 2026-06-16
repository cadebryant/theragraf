namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

// ── Biological sex ─────────────────────────────────────────────────────────────

/// <summary>
/// Biological sex field for clinical context.  Stored on the client record
/// and passed (non-PII) to the ICD-10 agent to improve coding precision.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BiologicalSex
{
    NotSpecified,
    Male,
    Female,
    Other,
}

// ── Summary passed to AI pipeline ─────────────────────────────────────────────

/// <summary>
/// Non-PII demographic summary forwarded from the frontend inside
/// <see cref="TranscriptInput"/> so the ICD-10 agent can use age and sex
/// context without ever seeing the client's real name or date of birth.
/// </summary>
public record ClientDemographicsSummary(
    /// <summary>Computed age in years (derived from encrypted DOB — never the raw DOB).</summary>
    int?          AgeYears,
    BiologicalSex Sex,
    /// <summary>Pre-existing diagnoses entered by the therapist; used to anchor code selection.</summary>
    string?       PriorDiagnoses,
    /// <summary>Functional limitations summary (e.g. "Limited ROM right shoulder, >50% ADL dependence").</summary>
    string?       FunctionalLimitations
);

// ── Full record returned by the API ───────────────────────────────────────────

/// <summary>
/// Client demographics returned by <c>GET /api/clients/{clientId}</c>.
/// DOB is never returned; only the computed <see cref="AgeYears"/> is exposed.
/// </summary>
public record ClientDemographicsResponse(
    string                  ClientId,
    int?                    AgeYears,
    BiologicalSex           Sex,
    string?                 PriorDiagnoses,
    string?                 FunctionalLimitations,
    DateTimeOffset          UpdatedAt
);

// ── Upsert request ────────────────────────────────────────────────────────────

/// <summary>
/// Body for <c>PUT /api/clients/{clientId}</c>.
/// All fields are optional; omitted fields are left unchanged on update.
/// DOB is accepted as an ISO-8601 date string (e.g. "1985-04-12") and is
/// immediately encrypted before being written to Cosmos.
/// </summary>
public record UpsertClientDemographicsRequest(
    /// <summary>
    /// ISO 8601 date, e.g. "1985-04-12".  Nullable — send <see langword="null"/>
    /// to clear the stored value.  The API never echoes this back.
    /// </summary>
    string?       DateOfBirth,
    BiologicalSex Sex             = BiologicalSex.NotSpecified,
    string?       PriorDiagnoses = null,
    string?       FunctionalLimitations = null
);
