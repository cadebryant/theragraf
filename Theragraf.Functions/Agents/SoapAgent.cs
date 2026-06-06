namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class SoapAgent(Kernel kernel) : BaseAgent(kernel), ISoapAgent
{
    public async Task<SoapNote> GenerateSoapNoteAsync(ObservationResult input)
    {
        var function = Kernel.Plugins.GetFunction("SoapAgent", "SoapAgent");
        var arguments = new KernelArguments { ["input"] = input.RedactedTranscript };
        var result = await Kernel.InvokeAsync(function, arguments);
        return JsonSerializer.Deserialize<SoapNote>(StripMarkdownCodeFence(result.ToString()))!;
    }
}