namespace Theragraf.Functions.Agents;

using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public abstract class BaseAgent : IClinicalAgent
{
    protected readonly Kernel Kernel;

    protected BaseAgent(Kernel kernel)
    {
        Kernel = kernel;
    }

    public virtual Task<string> ProcessAsync(string input)
    {
        throw new NotImplementedException();
    }
}
