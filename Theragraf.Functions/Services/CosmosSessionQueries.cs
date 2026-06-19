namespace Theragraf.Functions.Services;

using Theragraf.Core.Models;

/// <summary>
/// All Cosmos DB SQL fragments used by <see cref="CosmosSessionRepository"/>.
/// Keeping SQL text here means the repository contains no inline string literals,
/// and every query clause is findable in one place.
/// </summary>
/// <remarks>
/// Note on LINQ vs SQL: equality filters and ordering are expressed via LINQ in
/// the repository wherever the Cosmos LINQ provider supports them cleanly.
/// Date-range comparisons on the <c>id</c> field (a lexicographic date string)
/// use raw SQL because <c>string &gt;= string</c> has no supported LINQ translation
/// in the Cosmos SDK.
/// </remarks>
internal static class CosmosSessionQueries
{
    // ── Base clause ───────────────────────────────────────────────────────────

    /// <summary>
    /// Starting point for the paged/filtered query. The <c>@clientId</c> parameter
    /// must always be supplied.
    /// </summary>
    internal const string BaseSelect = "SELECT * FROM c WHERE c.clientId = @clientId";

    // ── Optional WHERE fragments (appended only when the filter is active) ────

    internal const string FilterDiscipline  = " AND c.discipline = @discipline";
    internal const string FilterTherapist   = " AND c.therapistName = @therapist";
    internal const string FilterPayer       = " AND c.payer = @payer";

    /// <summary>
    /// Lower-bound date filter. Uses string comparison because <c>id</c> stores the
    /// session date as an ISO-8601 / URL-safe string, and the Cosmos LINQ provider
    /// does not support <c>&gt;=</c> on <c>string</c> properties.
    /// </summary>
    internal const string FilterDateFrom    = " AND c.id >= @dateFrom";

    /// <summary>Upper-bound date filter — same caveat as <see cref="FilterDateFrom"/>.</summary>
    internal const string FilterDateTo      = " AND c.id <= @dateTo";

    // ── ORDER BY ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the <c>ORDER BY</c> clause from a <see cref="SessionQueryOptions"/>
    /// sort request. Returns a complete SQL fragment such as
    /// <c>" ORDER BY c.therapistName ASC"</c>.
    /// </summary>
    internal static string OrderByClause(SessionQueryOptions options)
    {
        var field = options.SortBy?.ToLowerInvariant() switch
        {
            "therapistname" or "therapist" => "c.therapistName",
            "discipline"                   => "c.discipline",
            "setting"                      => "c.setting",
            "payer"                        => "c.payer",
            "duration"                     => "c.sessionDurationMinutes",
            "createdat"                    => "c.createdAt",
            _                              => "c.id",   // default: session date
        };

        var direction = string.Equals(options.SortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC" : "DESC";

        return $" ORDER BY {field} {direction}";
    }

    // ── Stats projections ─────────────────────────────────────────────────────

    /// <summary>
    /// Projects only the fields required for stats aggregation — omits redactionMap
    /// and encryptedRedactionMap to avoid loading PHI unnecessarily.
    /// Filter: <c>@therapistName</c>
    /// </summary>
    internal const string StatsProjectionByTherapist =
        "SELECT c.id, c.clientId, c.therapistName, c.discipline, c.setting, c.payer, " +
        "c.sessionDurationMinutes, c.suggestedCptCodes, c.suggestedIcdCodes " +
        "FROM c WHERE c.therapistName = @therapistName";

    /// <summary>
    /// Returns one row per distinct client for the given therapist, with the most-recent
    /// session date, total session count, and synthetic flag. Ordered by lastSession descending.
    /// Filter: <c>@therapistName</c>
    /// </summary>
    internal const string CaseloadByTherapist =
        "SELECT c.clientId, MAX(c.id) AS lastSession, COUNT(1) AS totalSessions, " +
        "MAX(c.isSynthetic ? 1 : 0) AS isSynthetic " +
        "FROM c WHERE c.therapistName = @therapistName " +
        "GROUP BY c.clientId";

    /// <summary>
    /// Projects only the fields required for stats aggregation — omits redactionMap
    /// and encryptedRedactionMap to avoid loading PHI unnecessarily.
    /// Filter: <c>@clientId</c> (partition-key query).
    /// </summary>
    internal const string StatsProjectionByClient =
        "SELECT c.id, c.clientId, c.therapistName, c.discipline, c.setting, c.payer, " +
        "c.sessionDurationMinutes, c.suggestedCptCodes, c.suggestedIcdCodes, c.isSynthetic " +
        "FROM c WHERE c.clientId = @clientId";
}
