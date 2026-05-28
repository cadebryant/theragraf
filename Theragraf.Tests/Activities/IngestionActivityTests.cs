using FluentAssertions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Activities;

namespace Theragraf.Tests.Activities;

public class IngestionActivityTests
{
    private readonly IPiiRedactionService _redactionService;
    private readonly IngestionActivity _sut;

    public IngestionActivityTests()
    {
        _redactionService = Substitute.For<IPiiRedactionService>();
        _sut = new IngestionActivity(_redactionService);
    }

    private static TranscriptInput BuildInput(string transcript = "Raw session transcript.") =>
        new(transcript, "Dr. Adams", "client-001", DateTimeOffset.UtcNow);

    [Fact]
    public async Task Run_CallsRedactionServiceWithRawTranscript()
    {
        var input = BuildInput("Patient John said he was anxious.");
        _redactionService.RedactAsync(input.RawTranscript)
            .Returns(("[PERSON_1] said he was anxious.", new Dictionary<string, string> { ["[PERSON_1]"] = "John" }));

        await _sut.Run(input);

        await _redactionService.Received(1).RedactAsync(input.RawTranscript);
    }

    [Fact]
    public async Task Run_ReturnsObservationResultWithRedactedTranscript()
    {
        var input = BuildInput("Patient John said he was anxious.");
        const string redacted = "[PERSON_1] said he was anxious.";
        var map = new Dictionary<string, string> { ["[PERSON_1]"] = "John" };

        _redactionService.RedactAsync(input.RawTranscript).Returns((redacted, (IReadOnlyDictionary<string, string>)map));

        var result = await _sut.Run(input);

        result.RedactedTranscript.Should().Be(redacted);
        result.RedactionMap.Should().ContainKey("[PERSON_1]").WhoseValue.Should().Be("John");
    }

    [Fact]
    public async Task Run_PreservesMetadataFromInput()
    {
        var input = BuildInput();
        _redactionService.RedactAsync(Arg.Any<string>())
            .Returns(("redacted", (IReadOnlyDictionary<string, string>)new Dictionary<string, string>()));

        var result = await _sut.Run(input);

        result.TherapistName.Should().Be(input.TherapistName);
        result.ClientId.Should().Be(input.ClientId);
        result.SessionDate.Should().Be(input.SessionDate);
    }

    [Fact]
    public async Task Run_EmptyTranscript_ReturnsEmptyRedactedTextAndEmptyMap()
    {
        var input = BuildInput(string.Empty);
        _redactionService.RedactAsync(string.Empty)
            .Returns((string.Empty, (IReadOnlyDictionary<string, string>)new Dictionary<string, string>()));

        var result = await _sut.Run(input);

        result.RedactedTranscript.Should().BeEmpty();
        result.RedactionMap.Should().BeEmpty();
    }
}
