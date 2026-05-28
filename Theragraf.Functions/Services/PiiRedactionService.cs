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

        // Process longest entities first to avoid offset corruption
        foreach (var entity in entities.OrderByDescending(e => e.Offset))
        {
            var categoryKey = entity.Category.ToString().ToUpperInvariant();

            if (!counters.TryGetValue(categoryKey, out int value))
                counters[categoryKey] = 1;
            else
                counters[categoryKey] = value + 1;

            var placeholder = $"[{categoryKey}_{counters[categoryKey]}]";

            if (!redactionMap.ContainsValue(entity.Text))
                redactionMap[placeholder] = entity.Text;

            redactedText = redactedText.Remove(entity.Offset, entity.Length)
                                       .Insert(entity.Offset, placeholder);
        }

        return (redactedText, redactionMap);
    }
}