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
