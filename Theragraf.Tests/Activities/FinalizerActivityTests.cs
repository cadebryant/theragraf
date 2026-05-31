using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;

namespace Theragraf.Tests.Activities;

public class FinalizerActivityTests
{
    private readonly FinalizerActivity _sut = new();

    private static SoapNote BuildNote(string subjective, string objective, string assessment, string plan) =>
        new(subjective, objective, assessment, plan);

    private static FinalizeInput BuildInput(SoapNote note, Dictionary<string, string> map) =>
        new(note, map);

    [Fact]
    public async Task Run_EmptyRedactionMap_ReturnsSoapNoteUnchanged()
    {
        var note = BuildNote("Patient anxious.", "HR 90.", "GAD.", "CBT weekly.");
        var input = BuildInput(note, new Dictionary<string, string>());

        var result = await _sut.Run(input);

        result.RestoredNote.Should().Be(note);
    }

    [Fact]
    public async Task Run_ReplacesPlaceholderInSubjective()
    {
        var note = BuildNote("[PERSON_1] reported feeling anxious.", "Normal.", "GAD.", "CBT.");
        var map = new Dictionary<string, string> { ["[PERSON_1]"] = "John Smith" };

        var result = await _sut.Run(BuildInput(note, map));

        result.RestoredNote.Subjective.Should().Be("John Smith reported feeling anxious.");
    }

    [Fact]
    public async Task Run_ReplacesPlaceholdersInAllFourSoapFields()
    {
        var note = BuildNote(
            "[PERSON_1] reports anxiety.",
            "Session with [PERSON_1] on [DATE_1].",
            "[PERSON_1] meets criteria for GAD.",
            "Follow-up with [PERSON_1] next week."
        );
        var map = new Dictionary<string, string>
        {
            ["[PERSON_1]"] = "Jane Doe",
            ["[DATE_1]"]   = "01/15/2025"
        };

        var result = await _sut.Run(BuildInput(note, map));

        result.RestoredNote.Subjective.Should().Be("Jane Doe reports anxiety.");
        result.RestoredNote.Objective.Should().Be("Session with Jane Doe on 01/15/2025.");
        result.RestoredNote.Assessment.Should().Be("Jane Doe meets criteria for GAD.");
        result.RestoredNote.Plan.Should().Be("Follow-up with Jane Doe next week.");
    }

    [Fact]
    public async Task Run_MultiplePlaceholdersOfSameCategory_AllRestored()
    {
        var note = BuildNote(
            "[PERSON_1] discussed [PERSON_2]'s progress.", "Normal.", "Normal.", "Continue.");
        var map = new Dictionary<string, string>
        {
            ["[PERSON_1]"] = "Dr. Adams",
            ["[PERSON_2]"] = "John Smith"
        };

        var result = await _sut.Run(BuildInput(note, map));

        result.RestoredNote.Subjective.Should().Be("Dr. Adams discussed John Smith's progress.");
    }

    [Fact]
    public async Task Run_PlaceholderNotPresentInText_MapEntryIgnored()
    {
        var note = BuildNote("Patient reports stress.", "Normal.", "Adjustment disorder.", "Monitor.");
        var map = new Dictionary<string, string> { ["[PERSON_1]"] = "John Smith" };

        var result = await _sut.Run(BuildInput(note, map));

        result.RestoredNote.Should().Be(note);
    }

    [Fact]
    public async Task Run_WrapsRestoredNoteInFinalizeResult()
    {
        var note = BuildNote("[PERSON_1] attended.", "Normal.", "GAD.", "CBT.");
        var map = new Dictionary<string, string> { ["[PERSON_1]"] = "Alice" };

        var result = await _sut.Run(BuildInput(note, map));

        result.Should().BeOfType<FinalizeResult>();
        result.RestoredNote.Subjective.Should().Contain("Alice");
    }
}
