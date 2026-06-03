using Azure.AI.TextAnalytics;
using FluentAssertions;
using NSubstitute;
using Theragraf.Core.Services;
using Theragraf.Functions.Services;

namespace Theragraf.Tests.Services;

public class PiiRedactionServiceTests
{
    private readonly ITextAnalyticsClientAdapter _adapter;
    private readonly PiiRedactionService _sut;

    public PiiRedactionServiceTests()
    {
        _adapter = Substitute.For<ITextAnalyticsClientAdapter>();
        _sut = new PiiRedactionService(_adapter);
    }

    [Fact]
    public async Task RedactAsync_NoEntitiesDetected_ReturnsOriginalTextAndEmptyMap()
    {
        const string raw = "The session went well today.";
        _adapter.RecognizePiiEntitiesAsync(raw, "en", Arg.Any<RecognizePiiEntitiesOptions>())
                .Returns(new List<PiiEntity>());

        var (redacted, map) = await _sut.RedactAsync(raw);

        redacted.Should().Be(raw);
        map.Should().BeEmpty();
    }

    [Fact]
    public async Task RedactAsync_SinglePersonEntity_ReplacesNameWithPlaceholder()
    {
        const string raw = "John Smith reported feeling anxious.";
        var entity = CreatePiiEntity("John Smith", PiiEntityCategory.Person, offset: 0, length: 10);

        _adapter.RecognizePiiEntitiesAsync(raw, "en", Arg.Any<RecognizePiiEntitiesOptions>())
                .Returns(new List<PiiEntity> { entity });

        var (redacted, map) = await _sut.RedactAsync(raw);

        redacted.Should().Be("[PERSON_1] reported feeling anxious.");
        map.Should().ContainKey("[PERSON_1]").WhoseValue.Should().Be("John Smith");
    }

    [Fact]
    public async Task RedactAsync_MultipleEntitiesOfSameCategory_GeneratesIncrementingPlaceholders()
    {
        const string raw = "John Smith called Jane Doe.";
        var entity1 = CreatePiiEntity("John Smith", PiiEntityCategory.Person, offset: 0, length: 10);
        var entity2 = CreatePiiEntity("Jane Doe", PiiEntityCategory.Person, offset: 18, length: 8);

        // Return ordered by descending offset (as service processes them)
        _adapter.RecognizePiiEntitiesAsync(raw, "en", Arg.Any<RecognizePiiEntitiesOptions>())
                .Returns(new List<PiiEntity> { entity1, entity2 });

        var (redacted, map) = await _sut.RedactAsync(raw);

        map.Should().ContainKey("[PERSON_1]");
        map.Should().ContainKey("[PERSON_2]");
        redacted.Should().NotContain("John Smith");
        redacted.Should().NotContain("Jane Doe");
    }

    [Fact]
    public async Task RedactAsync_MixedEntityTypes_PlaceholdersReflectCategory()
    {
        const string raw = "John Smith, 555-1234, attended the session.";
        var personEntity = CreatePiiEntity("John Smith", PiiEntityCategory.Person, offset: 0, length: 10);
        var phoneEntity = CreatePiiEntity("555-1234", PiiEntityCategory.PhoneNumber, offset: 12, length: 8);

        _adapter.RecognizePiiEntitiesAsync(raw, "en", Arg.Any<RecognizePiiEntitiesOptions>())
                .Returns(new List<PiiEntity> { personEntity, phoneEntity });

        var (redacted, map) = await _sut.RedactAsync(raw);

        map.Keys.Should().Contain(k => k.StartsWith("[PERSON_"));
        map.Keys.Should().Contain(k => k.StartsWith("[PHONENUMBER_"));
    }

    [Fact]
    public async Task RedactAsync_RepeatedEntityText_ReusesSamePlaceholder()
    {
        // "John Smith" appears twice; both occurrences should map to the same [PERSON_1]
        // so the redaction map has exactly one entry and both can be restored.
        const string raw = "John Smith called. Later John Smith left.";
        var entity1 = CreatePiiEntity("John Smith", PiiEntityCategory.Person, offset: 0, length: 10);
        var entity2 = CreatePiiEntity("John Smith", PiiEntityCategory.Person, offset: 19, length: 10);

        _adapter.RecognizePiiEntitiesAsync(raw, "en", Arg.Any<RecognizePiiEntitiesOptions>())
                .Returns(new List<PiiEntity> { entity1, entity2 });

        var (redacted, map) = await _sut.RedactAsync(raw);

        map.Should().HaveCount(1, "repeated entity text should reuse a single placeholder");
        map.Should().ContainKey("[PERSON_1]").WhoseValue.Should().Be("John Smith");
        redacted.Should().NotContain("John Smith");
        redacted.Should().Contain("[PERSON_1]");
    }

    [Fact]
    public async Task RedactAsync_AdapterThrows_ExceptionPropagates()
    {
        _adapter.RecognizePiiEntitiesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RecognizePiiEntitiesOptions>())
                .Returns<IReadOnlyList<PiiEntity>>(_ => throw new InvalidOperationException("Service unavailable"));

        var act = async () => await _sut.RedactAsync("some text");

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Service unavailable");
    }

    // Helper: PiiEntity has no public constructor, so we use reflection to create test instances
    private static PiiEntity CreatePiiEntity(string text, PiiEntityCategory category, int offset, int length)
    {
        // PiiEntity internal constructor signature: (string text, string category, string subcategory, double confidenceScore, int offset, int length)
        return (PiiEntity)Activator.CreateInstance(
            typeof(PiiEntity),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null,
            args: [text, category.ToString(), null, 0.99, offset, length],
            culture: null)!;
    }
}
