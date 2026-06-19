namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Azure Cosmos DB document representing a single treatment goal for a client.
/// Container: goals   PartitionKey: /clientId   id: GoalId (GUID)
/// </summary>
public class GoalDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;          // GUID — goal identifier

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;    // PartitionKey (namespaced)

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = nameof(GoalStatus.Active);

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("targetDate")]
    public DateTimeOffset? TargetDate { get; set; }

    [JsonPropertyName("resolvedAt")]
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>True if this is synthetic/demo data, false for real patient data.</summary>
    [JsonPropertyName("isSynthetic")]
    public bool IsSynthetic { get; set; }

    [JsonPropertyName("progressNotes")]
    public List<GoalProgressNoteDocument> ProgressNotes { get; set; } = [];
}

/// <summary>
/// Inline progress-note sub-document stored inside <see cref="GoalDocument"/>.
/// </summary>
public class GoalProgressNoteDocument
{
    [JsonPropertyName("noteId")]
    public string NoteId { get; set; } = string.Empty;

    [JsonPropertyName("recordedAt")]
    public DateTimeOffset RecordedAt { get; set; }

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;
}
