namespace Theragraf.Core.Services;

public interface IPiiRedactionService
{
    Task<(string RedactedText, IReadOnlyDictionary<string, string> RedactionMap)> RedactAsync(string rawText);
}