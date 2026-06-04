namespace Theragraf.Core.Services;

/// <summary>
/// Calculates and validates CMS 8-minute rule billable units for timed CPT codes.
/// </summary>
public interface ICmsUnitCalculator
{
    /// <summary>
    /// Returns the maximum billable units derivable from <paramref name="sessionDurationMinutes"/>
    /// across ALL timed codes in the session (the session-level cap).
    /// </summary>
    int SessionUnitCap(int sessionDurationMinutes);

    /// <summary>
    /// Returns the maximum units that a single timed code can claim given that
    /// <paramref name="minutesSpentOnCode"/> were spent on it directly.
    /// Returns 1 for untimed codes regardless of duration.
    /// </summary>
    int MaxUnitsForCode(string cptCode, int minutesSpentOnCode);

    /// <summary>
    /// Clamps <paramref name="suggestedUnits"/> to a safe value for a timed code given
    /// the total <paramref name="sessionDurationMinutes"/>.  Untimed codes always return 1.
    /// This is the primary method used after the LLM suggests unit counts.
    /// </summary>
    int ClampUnits(string cptCode, int suggestedUnits, int? sessionDurationMinutes);
}
