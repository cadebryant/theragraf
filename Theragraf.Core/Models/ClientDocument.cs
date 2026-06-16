namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Azure Cosmos DB document for a client's demographic / intake record.
///
/// Container : clients
/// PartitionKey: /clientId
/// id          : same as clientId (one document per client)
///
/// PII handling:
///   <see cref="EncryptedDateOfBirth"/> stores the DOB as an AES-256-GCM encrypted,
///   base64-encoded blob using the same Key Vault key as session redaction maps.
///   The raw DOB value is NEVER stored in plaintext or returned by any API.
///   All other fields (sex, diagnoses, limitations) are considered clinical
///   context, not personal identifiers.
/// </summary>
public class ClientDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;           // == clientId

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;     // partition key

    /// <summary>
    /// AES-256-GCM encrypted ISO-8601 date string, base64-encoded (nonce|cipher|tag).
    /// Null when DOB has not been provided.
    /// </summary>
    [JsonPropertyName("encryptedDateOfBirth")]
    public string? EncryptedDateOfBirth { get; set; }

    [JsonPropertyName("sex")]
    public string Sex { get; set; } = nameof(BiologicalSex.NotSpecified);

    /// <summary>Free-text prior diagnoses / relevant history (e.g. "CVA 2022, L hemiplegia").</summary>
    [JsonPropertyName("priorDiagnoses")]
    public string? PriorDiagnoses { get; set; }

    /// <summary>Functional limitations summary typed by the therapist at intake.</summary>
    [JsonPropertyName("functionalLimitations")]
    public string? FunctionalLimitations { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
}
