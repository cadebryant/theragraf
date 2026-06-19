namespace Theragraf.Core.Models;

using System.Text.Json.Serialization;

// ── Status enum ───────────────────────────────────────────────────────────────

/// <summary>Lifecycle stage of a treatment goal.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GoalStatus
{
    /// <summary>Goal is active and being worked toward.</summary>
    Active,

    /// <summary>Goal has been met.</summary>
    Met,

    /// <summary>Goal was discontinued before being met (e.g. discharge, change of focus).</summary>
    Discontinued,

    /// <summary>Goal was not met by the target date; requires re-evaluation.</summary>
    NotMet
}

// ── Embedded progress note ────────────────────────────────────────────────────

/// <summary>
/// A brief progress entry attached to a goal, typically written after each session.
/// Stored inline in <see cref="GoalDocument"/>.
/// </summary>
public record GoalProgressNote(
    string           NoteId,
    DateTimeOffset   RecordedAt,
    string           Note
);

// ── Domain model (read path) ──────────────────────────────────────────────────

/// <summary>
/// Full goal record returned to the client after read or write operations.
/// </summary>
public record GoalResponse(
    string                      GoalId,
    string                      ClientId,
    string                      Title,
    string                      Description,
    GoalStatus                  Status,
    DateTimeOffset              CreatedAt,
    DateTimeOffset?             TargetDate,
    DateTimeOffset?             ResolvedAt,
    IReadOnlyList<GoalProgressNote> ProgressNotes,
    bool                        IsSynthetic
);

// ── Write request models ──────────────────────────────────────────────────────

/// <summary>Body for POST /api/goals/{clientId}.</summary>
public record CreateGoalRequest(
    string           Title,
    string           Description,
    DateTimeOffset?  TargetDate
);

/// <summary>
/// Body for PATCH /api/goals/{clientId}/{goalId}.
/// Any null field is left unchanged.
/// </summary>
public record UpdateGoalRequest(
    string?          Title,
    string?          Description,
    GoalStatus?      Status,
    DateTimeOffset?  TargetDate,
    string?          ProgressNote   // when non-null, a new progress entry is appended
);

// ── Suggestion response ───────────────────────────────────────────────────────

/// <summary>
/// One AI-generated goal suggestion returned by POST /api/goals/{clientId}/suggest.
/// The client presents these for the therapist to accept or reject.
/// </summary>
public record GoalSuggestion(
    string Title,
    string Description
);
