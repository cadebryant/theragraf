namespace Theragraf.Functions.Services;

using Azure.AI.TextAnalytics;
using Theragraf.Core.Services;

public class TextAnalyticsClientAdapter : ITextAnalyticsClientAdapter
{
    private readonly TextAnalyticsClient _client;

    public TextAnalyticsClientAdapter(TextAnalyticsClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<PiiEntity>> RecognizePiiEntitiesAsync(
        string text, string language, RecognizePiiEntitiesOptions options)
    {
        var response = await _client.RecognizePiiEntitiesAsync(text, language, options);
        return response.Value.ToList();
    }
}
