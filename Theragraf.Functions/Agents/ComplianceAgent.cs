namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class ComplianceAgent(Kernel kernel, ILoggerFactory loggerFactory)
    : BaseAgent(kernel, loggerFactory.CreateLogger<ComplianceAgent>()), IComplianceAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ComplianceResult> ValidateAsync(SoapNote note, NoteFormat noteFormat = NoteFormat.Soap)
    {
        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var raw = await InvokePluginAsync("ComplianceAgent", "ComplianceAgent",
            new KernelArguments
            {
                ["input"] = soapJson,
                ["noteFormat"] = noteFormat.ToString()
            });
        return JsonSerializer.Deserialize<ComplianceResult>(StripMarkdownCodeFence(raw), JsonOptions)!;
    }
}
