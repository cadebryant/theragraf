namespace Theragraf.Functions.Agents;

using Theragraf.Core.Models;

public interface IGoalAgent
{
    /// <summary>
    /// Generates 3–5 SMART goal suggestions from the given SOAP note sections.
    /// </summary>
    Task<IReadOnlyList<GoalSuggestion>> SuggestGoalsAsync(
        SoapNote note, TherapyDiscipline discipline,
        CancellationToken cancellationToken = default);
}
