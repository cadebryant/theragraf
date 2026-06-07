namespace Theragraf.Core.Models;

/// <summary>
/// Optional filter and sort parameters for <see cref="Services.ISessionRepository.GetByClientIdPagedAsync"/>.
/// All properties are optional — omitted values apply no constraint.
/// </summary>
public record SessionQueryOptions(
    string?          Discipline   = null,
    string?          Therapist    = null,
    string?          Payer        = null,
    DateTimeOffset?  DateFrom     = null,
    DateTimeOffset?  DateTo       = null,
    string           SortBy       = "sessionDate",
    string           SortOrder    = "desc"
);
