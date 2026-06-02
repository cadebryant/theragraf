namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class BillingAgent(Kernel kernel) : BaseAgent(kernel), IBillingAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CptCode>> SuggestCptCodesAsync(SoapNote note)
    {
        var function = Kernel.Plugins.GetFunction("BillingAgent", "BillingAgent");
        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var arguments = new KernelArguments { ["input"] = soapJson };
        var result = await Kernel.InvokeAsync(function, arguments);

        var response = JsonSerializer.Deserialize<BillingResponse>(result.ToString(), JsonOptions)!;
        return response.SuggestedCptCodes;
    }

    private record BillingResponse(IReadOnlyList<CptCode> SuggestedCptCodes);
}
