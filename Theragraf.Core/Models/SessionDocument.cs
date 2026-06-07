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

    [JsonPropertyName("setting")]
    public string Setting { get; set; } = string.Empty;

    [JsonPropertyName("payer")]
    public string Payer { get; set; } = string.Empty;

    [JsonPropertyName("sessionDurationMinutes")]
    public int? SessionDurationMinutes { get; set; }

    [JsonPropertyName("redactionMap")]
    public Dictionary<string, string> RedactionMap { get; set; } = [];

    [JsonPropertyName("soapNote")]
    public SoapNote SoapNote { get; set; } = new("", "", "", "");

    [JsonPropertyName("suggestedCptCodes")]
    public List<CptCode> SuggestedCptCodes { get; set; } = [];

    [JsonPropertyName("suggestedIcdCodes")]
    public List<IcdCode> SuggestedIcdCodes { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}
