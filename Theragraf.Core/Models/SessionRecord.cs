namespace Theragraf.Core.Models;

/// <summary>
/// Azure Table Storage entity representing one completed therapy session.
/// PartitionKey = ClientId, RowKey = SessionDate (ISO-8601, URL-safe).
/// SOAP, CPT, and ICD data are stored as serialized JSON strings.
/// </summary>
public class SessionRecord
{
    /// <summary>Client identifier (PartitionKey).</summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>Session date in ISO-8601 format (RowKey).</summary>
    public string RowKey { get; set; } = string.Empty;

    public string TherapistName { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public int? SessionDurationMinutes { get; set; }

    /// <summary>JSON-serialized <see cref="SoapNote"/>.</summary>
    public string SoapNoteJson { get; set; } = string.Empty;

    /// <summary>JSON-serialized list of <see cref="CptCode"/>.</summary>
    public string CptCodesJson { get; set; } = string.Empty;

    /// <summary>JSON-serialized list of <see cref="IcdCode"/>.</summary>
    public string IcdCodesJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
