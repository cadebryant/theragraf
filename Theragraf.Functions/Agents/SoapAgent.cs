namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class SoapAgent(Kernel kernel, ILoggerFactory loggerFactory)
    : BaseAgent(kernel, loggerFactory.CreateLogger<SoapAgent>()), ISoapAgent
{
    public async Task<SoapNote> GenerateSoapNoteAsync(ObservationResult input)
    {
        var raw = await InvokePluginAsync("SoapAgent", "SoapAgent",
            new KernelArguments
            {
                ["input"] = input.RedactedTranscript,
                ["discipline"] = input.Discipline.ToString(),
                ["noteFormat"] = input.NoteFormat.ToString()
            });
        return JsonSerializer.Deserialize<SoapNote>(StripMarkdownCodeFence(raw))!;
    }
}