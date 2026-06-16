namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class GoalAgent(Kernel kernel, ILoggerFactory loggerFactory)
    : BaseAgent(kernel, loggerFactory.CreateLogger<GoalAgent>()), IGoalAgent
{
    public async Task<IReadOnlyList<GoalSuggestion>> SuggestGoalsAsync(
        SoapNote note, TherapyDiscipline discipline,
        CancellationToken cancellationToken = default)
    {
        var raw = await InvokePluginAsync("GoalAgent", "GoalAgent",
            new KernelArguments
            {
                ["subjective"] = note.Subjective,
                ["objective"]  = note.Objective,
                ["assessment"] = note.Assessment,
                ["plan"]       = note.Plan,
                ["discipline"] = discipline.ToString(),
            });

        return JsonSerializer.Deserialize<List<GoalSuggestion>>(
            StripMarkdownCodeFence(raw),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];
    }
}
