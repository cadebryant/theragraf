using Azure.AI.TextAnalytics;
using Theragraf.Core.Services;

namespace Theragraf.Functions.Services;

public class PiiRedactionService(ITextAnalyticsClientAdapter client) : IPiiRedactionService
{
    private readonly ITextAnalyticsClientAdapter _client = client;

    public async Task<(string RedactedText, IReadOnlyDictionary<string, string> RedactionMap)> RedactAsync(string rawText)
    {
        var entities = await _client.RecognizePiiEntitiesAsync(rawText, "en", new RecognizePiiEntitiesOptions
        {
            CategoriesFilter =
            {
                PiiEntityCategory.Person,
                PiiEntityCategory.PhoneNumber,
                PiiEntityCategory.Address,
                PiiEntityCategory.Email,
                PiiEntityCategory.USSocialSecurityNumber,
                PiiEntityCategory.Date,
                PiiEntityCategory.Organization,
            }
        });

        var redactionMap = new Dictionary<string, string>();
        var redactedText = rawText;
        var counters = new Dictionary<string, int>();
        var textToPlaceholder = new Dictionary<string, string>(); // reuse same placeholder for repeated values

        // Process longest entities first to avoid offset corruption
        foreach (var entity in entities.OrderByDescending(e => e.Offset))
        {
            var categoryKey = entity.Category.ToString().ToUpperInvariant();

            // If this exact text was already seen, reuse its placeholder
            if (!textToPlaceholder.TryGetValue(entity.Text, out var placeholder))
            {
                counters[categoryKey] = counters.TryGetValue(categoryKey, out int count) ? count + 1 : 1;
                placeholder = $"[{categoryKey}_{counters[categoryKey]}]";
                redactionMap[placeholder] = entity.Text;
                textToPlaceholder[entity.Text] = placeholder;
            }

            redactedText = redactedText.Remove(entity.Offset, entity.Length)
                                       .Insert(entity.Offset, placeholder);
        }

        return (redactedText, redactionMap);
    }
}