namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class SoapAgent : BaseAgent
{
    public SoapAgent(Kernel kernel) : base(kernel) { }

    public async Task<SoapNote> GenerateSoapNoteAsync(ObservationResult input)
    {
        var function = Kernel.Plugins.GetFunction("SoapAgent", "SoapAgent");
        var arguments = new KernelArguments { ["input"] = input.ProcessedTranscript };
        var result = await Kernel.InvokeAsync(function, arguments);
        return JsonSerializer.Deserialize<SoapNote>(result.ToString())!;
    }
}