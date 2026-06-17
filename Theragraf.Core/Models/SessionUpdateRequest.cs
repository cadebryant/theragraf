namespace Theragraf.Core.Models;

/// <summary>
/// Partial-update payload for PATCH /api/sessions/{clientId}/{sessionDate}.
/// All fields are optional — omitting a field leaves the stored value unchanged.
///
/// SOAP note fields must be provided in their PII-restored form (i.e. with real
/// names/identifiers as the therapist sees them). The server will re-run PII
/// redaction before persisting so that storage remains HIPAA-clean.
/// </summary>
public record SessionUpdateRequest(
    SoapNoteUpdate?          SoapNote          = null,
    IReadOnlyList<CptCode>?  SuggestedCptCodes = null,
    IReadOnlyList<IcdCode>?  SuggestedIcdCodes = null,
    ApprovalUpdate?          Approval          = null
);

public record ApprovalUpdate(
    bool VerifyAndApprove,
    string? ApprovedBy = null
);

/// <summary>Individual SOAP section updates — all fields optional.</summary>
public record SoapNoteUpdate(
    string? Subjective = null,
    string? Objective  = null,
    string? Assessment = null,
    string? Plan       = null
);
