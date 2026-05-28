namespace Theragraf.Core.Services;

using Azure.AI.TextAnalytics;

public interface ITextAnalyticsClientAdapter
{
    Task<IReadOnlyList<PiiEntity>> RecognizePiiEntitiesAsync(string text, string language, RecognizePiiEntitiesOptions options);
}
