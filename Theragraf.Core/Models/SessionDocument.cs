namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Azure Cosmos DB document representing one completed therapy session.
/// id = SessionDate (ISO-8601, URL-safe), partitionKey = ClientId.
/// Nested objects are stored natively — no JSON-string columns.
/// </summary>
public class SessionDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;          // SessionDate (row key)

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;    // PartitionKey

    [JsonPropertyName("therapistName")]
    public string TherapistName { get; set; } = string.Empty;

    [JsonPropertyName("discipline")]
    public string Discipline { get; set; } = string.Empty;

    [JsonPropertyName("noteFormat")]
    public string NoteFormat { get; set; } = "Soap";

    [JsonPropertyName("setting")]
    public string Setting { get; set; } = string.Empty;

    [JsonPropertyName("payer")]
    public string Payer { get; set; } = string.Empty;

    [JsonPropertyName("sessionDurationMinutes")]
    public int? SessionDurationMinutes { get; set; }

    /// <summary>
    /// Redaction map stored as a plain dictionary when encryption is disabled (local dev).
    /// Null when <see cref="RedactionMapIsEncrypted"/> is <see langword="true"/>.
    /// </summary>
    [JsonPropertyName("redactionMap")]
    public Dictionary<string, string>? RedactionMap { get; set; }

    /// <summary>
    /// AES-256-GCM encrypted, base64-encoded redaction map blob (nonce|ciphertext|tag).
    /// Null when <see cref="RedactionMapIsEncrypted"/> is <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("encryptedRedactionMap")]
    public string? EncryptedRedactionMap { get; set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="EncryptedRedactionMap"/> carries the data;
    /// <see langword="false"/> when <see cref="RedactionMap"/> carries the data.
    /// Allows safe rollout alongside existing unencrypted documents.
    /// </summary>
    [JsonPropertyName("redactionMapIsEncrypted")]
    public bool RedactionMapIsEncrypted { get; set; }

    [JsonPropertyName("soapNote")]
    public SoapNote SoapNote { get; set; } = new("", "", "", "");

    [JsonPropertyName("suggestedCptCodes")]
    public List<CptCode> SuggestedCptCodes { get; set; } = [];

    [JsonPropertyName("suggestedIcdCodes")]
    public List<IcdCode> SuggestedIcdCodes { get; set; } = [];

    [JsonPropertyName("isApproved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("approvedBy")]
    public string? ApprovedBy { get; set; }

    [JsonPropertyName("approvedAt")]
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>True if this is synthetic/demo data, false for real patient data.</summary>
    [JsonPropertyName("isSynthetic")]
    public bool IsSynthetic { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
