namespace Theragraf.Functions.Agents;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;

public abstract class BaseAgent : IClinicalAgent
{
    protected readonly Kernel Kernel;
    protected readonly ILogger Logger;

    protected BaseAgent(Kernel kernel, ILogger logger)
    {
        Kernel = kernel;
        Logger = logger;
    }

    public virtual Task<string> ProcessAsync(string input)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Invokes a Semantic Kernel plugin function with structured logging.
    /// Logs only plugin/function names — never prompt content or responses.
    /// </summary>
    protected async Task<string> InvokePluginAsync(string pluginName, string functionName, KernelArguments arguments)
    {
        Logger.LogInformation("Invoking SK plugin={PluginName} function={FunctionName}", pluginName, functionName);
        try
        {
            var function = Kernel.Plugins.GetFunction(pluginName, functionName);
            var result = await Kernel.InvokeAsync(function, arguments);
            Logger.LogInformation("SK plugin={PluginName} function={FunctionName} completed", pluginName, functionName);
            return result.ToString();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SK plugin={PluginName} function={FunctionName} failed", pluginName, functionName);
            throw new AgentException(pluginName, $"SK invocation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Strips markdown code fences (```json ... ``` or ``` ... ```) that the
    /// LLM sometimes wraps around JSON responses before deserialization.
    /// </summary>
    protected static string StripMarkdownCodeFence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```")) return text;

        // Remove opening fence line (e.g. ```json or ```)
        var firstNewline = text.IndexOf('\n');
        if (firstNewline < 0) return text;
        text = text[(firstNewline + 1)..];

        // Remove closing fence
        var closingFence = text.LastIndexOf("```");
        if (closingFence >= 0)
            text = text[..closingFence];

        return text.Trim();
    }
}
