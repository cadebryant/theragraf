namespace Theragraf.Functions.Agents;

using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class SoapAgent : BaseAgent
{
    public SoapAgent(Kernel kernel) : base(kernel) { }

    public override async Task<string> ProcessAsync(string input)
    {
        var function = Kernel.Plugins.GetFunction("SoapAgent", "SoapAgent");
        var arguments = new KernelArguments { ["input"] = input };
        var result = await Kernel.InvokeAsync(function, arguments);
        return result.ToString();
    }
}
