using Azure;
using Azure.AI.TextAnalytics;
using Theragraf.Core.Services;

namespace Theragraf.Functions.Services;

public class PiiRedactionService(TextAnalyticsClient client) : IPiiRedactionService
{
    private readonly TextAnalyticsClient _client = client;

    public async Task<(string RedactedText, IReadOnlyDictionary<string, string> RedactionMap)> RedactAsync(string rawText)
    {
        var response = await _client.RecognizePiiEntitiesAsync(rawText, "en", new RecognizePiiEntitiesOptions
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
        var entities = response.Value
            .OrderByDescending(e => e.Offset)
            .ToList();

        foreach (var entity in entities)
        {
            var categoryKey = entity.Category.ToString().ToUpperInvariant();

            if (!counters.TryGetValue(categoryKey, out int value))
                counters[categoryKey] = 1;
            else
                counters[categoryKey] = ++value;

            var placeholder = $"[{categoryKey}_{counters[categoryKey]}]";

            if (!redactionMap.ContainsValue(entity.Text))
                redactionMap[placeholder] = entity.Text;

            redactedText = redactedText.Remove(entity.Offset, entity.Length)
                                       .Insert(entity.Offset, placeholder);
        }

        return (redactedText, redactionMap);
    }
}