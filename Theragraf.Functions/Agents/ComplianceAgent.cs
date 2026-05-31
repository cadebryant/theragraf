namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class ComplianceAgent(Kernel kernel) : BaseAgent(kernel), IComplianceAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ComplianceResult> ValidateAsync(SoapNote note)
    {
        var function = Kernel.Plugins.GetFunction("ComplianceAgent", "ComplianceAgent");

        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var arguments = new KernelArguments { ["input"] = soapJson };

        var result = await Kernel.InvokeAsync(function, arguments);

        return JsonSerializer.Deserialize<ComplianceResult>(result.ToString(), JsonOptions)!;
    }
}
