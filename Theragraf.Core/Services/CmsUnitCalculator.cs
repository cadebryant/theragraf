namespace Theragraf.Core.Services;

/// <summary>
/// Deterministic CMS 8-minute rule engine.
///
/// Timed codes (billed per 15-minute unit):
///   • Each unit requires at least 8 minutes of direct skilled service.
///   • Breakpoints:  8–22 min = 1 unit | 23–37 min = 2 | 38–52 min = 3 | 53–67 min = 4 …
///   • Formula: units = floor((minutes + 7) / 15)
///   • The sum of all timed units in a session must not exceed the session-level cap.
///
/// Untimed codes (evaluated/modality/group codes) are always 1 unit.
/// </summary>
public sealed class CmsUnitCalculator : ICmsUnitCalculator
{
    // CPT codes that are UNTIMED — billed once per session regardless of duration.
    // Includes: evaluations, re-evaluations, unattended modalities, group, self-care training.
    private static readonly HashSet<string> UntimedCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // OT / PT evaluations
        "97165", "97166", "97167", "97168",  // OT eval / re-eval
        "97161", "97162", "97163", "97164",  // PT eval / re-eval (new codes)
        "97001", "97002", "97003", "97004",  // PT eval / re-eval (legacy)
        // Unattended physical agents
        "97010", "97014", "97016", "97018", "97022", "97024", "97026", "97028",
        // Self-care / home management training (untimed per CMS)
        "97535",
        // Group therapeutic procedure
        "97150",
        // Orthotic/prosthetic management (first visit is untimed)
        "97760", "97761", "97762",
        // Physical performance test
        "97750",
        // Wheelchair management
        "97542",
        // Psychotherapy evaluations
        "90791", "97792",
        // Unlisted
        "97039",
        // SLP evaluations (untimed)
        "92521", "92522", "92523", "92524",  // speech/voice/language evaluations
        "92610", "92611", "92612",           // swallowing evaluations
        "96105", "96125",                    // aphasia / cognitive-communication assessment
        "92597", "92605",                    // AAC evaluation/fitting
        // SLP group (untimed)
        "92508",
    };

    /// <inheritdoc />
    public int SessionUnitCap(int sessionDurationMinutes)
    {
        if (sessionDurationMinutes <= 0) return 0;
        // Each unit block = 15 min; a partial block of ≥8 min earns one more unit.
        return (sessionDurationMinutes + 7) / 15;
    }

    /// <inheritdoc />
    public int MaxUnitsForCode(string cptCode, int minutesSpentOnCode)
    {
        if (IsUntimed(cptCode)) return 1;
        if (minutesSpentOnCode < 8) return 0;  // less than 8 min → not billable
        return (minutesSpentOnCode + 7) / 15;
    }

    /// <inheritdoc />
    public int ClampUnits(string cptCode, int suggestedUnits, int? sessionDurationMinutes)
    {
        if (IsUntimed(cptCode)) return 1;

        // Without a duration we cannot validate — trust the LLM suggestion (minimum 1).
        if (sessionDurationMinutes is null or <= 0)
            return Math.Max(1, suggestedUnits);

        var cap = SessionUnitCap(sessionDurationMinutes.Value);
        // Clamp between 1 (must be at least 8 min to bill) and the session cap.
        return Math.Clamp(suggestedUnits, 1, Math.Max(1, cap));
    }

    private static bool IsUntimed(string cptCode) =>
        UntimedCodes.Contains(cptCode.Trim());
}
