using FluentAssertions;
using Theragraf.Core.Services;

namespace Theragraf.Tests.Services;

public class CmsUnitCalculatorTests
{
    private readonly CmsUnitCalculator _sut = new();

    // ── SessionUnitCap ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,  0)]
    [InlineData(7,  0)]   // 7 min < 8 min → 0 units
    [InlineData(8,  1)]   // exactly 8 min → 1 unit
    [InlineData(22, 1)]   // 22 min → still 1 unit  (< 23 min breakpoint)
    [InlineData(23, 2)]   // 23 min → 2 units
    [InlineData(37, 2)]
    [InlineData(38, 3)]
    [InlineData(45, 3)]   // typical 45-min session → 3 units cap
    [InlineData(52, 3)]
    [InlineData(53, 4)]
    [InlineData(60, 4)]   // 60-min session → 4 units cap
    [InlineData(67, 4)]
    [InlineData(68, 5)]
    [InlineData(90, 6)]
    public void SessionUnitCap_ReturnsCorrectUnits(int minutes, int expectedCap)
    {
        _sut.SessionUnitCap(minutes).Should().Be(expectedCap);
    }

    // ── MaxUnitsForCode — timed code ─────────────────────────────────────────

    [Theory]
    [InlineData(0,  0)]   // 0 min → not billable
    [InlineData(7,  0)]   // 7 min → not billable (< 8 min threshold)
    [InlineData(8,  1)]   // 8 min → 1 unit
    [InlineData(22, 1)]
    [InlineData(23, 2)]
    [InlineData(45, 3)]
    public void MaxUnitsForCode_TimedCode_ReturnsCorrectUnits(int minutes, int expected)
    {
        _sut.MaxUnitsForCode("97530", minutes).Should().Be(expected);
    }

    // ── MaxUnitsForCode — untimed codes ──────────────────────────────────────

    [Theory]
    [InlineData("97165")]   // OT evaluation
    [InlineData("97535")]   // self-care training
    [InlineData("97150")]   // group
    [InlineData("97010")]   // hot/cold packs
    [InlineData("97760")]   // orthotic management
    [InlineData("90791")]   // psychiatric eval
    public void MaxUnitsForCode_UntimedCode_AlwaysReturnsOne(string code)
    {
        // Even with a high minute count, untimed codes are always 1 unit
        _sut.MaxUnitsForCode(code, 90).Should().Be(1);
    }

    // ── ClampUnits — timed code ──────────────────────────────────────────────

    [Fact]
    public void ClampUnits_TimedCode_ClampsSuggestedToSessionCap()
    {
        // 45-min session → cap = 3; LLM suggests 5 → clamped to 3
        _sut.ClampUnits("97530", suggestedUnits: 5, sessionDurationMinutes: 45)
            .Should().Be(3);
    }

    [Fact]
    public void ClampUnits_TimedCode_AllowsValidSuggestion()
    {
        // 45-min session → cap = 3; LLM suggests 2 → accepted as-is
        _sut.ClampUnits("97530", suggestedUnits: 2, sessionDurationMinutes: 45)
            .Should().Be(2);
    }

    [Fact]
    public void ClampUnits_TimedCode_SuggestionAtCapIsAccepted()
    {
        // 45-min session → cap = 3; LLM suggests 3 → accepted
        _sut.ClampUnits("97530", suggestedUnits: 3, sessionDurationMinutes: 45)
            .Should().Be(3);
    }

    [Fact]
    public void ClampUnits_TimedCode_ZeroSuggestionClampsToOne()
    {
        // Zero units is never valid for a billed timed code — minimum is 1
        _sut.ClampUnits("97530", suggestedUnits: 0, sessionDurationMinutes: 45)
            .Should().Be(1);
    }

    [Fact]
    public void ClampUnits_TimedCode_NullDuration_TrustsSuggestion()
    {
        // No session duration → can't validate, accept the LLM suggestion (min 1)
        _sut.ClampUnits("97530", suggestedUnits: 3, sessionDurationMinutes: null)
            .Should().Be(3);
    }

    [Fact]
    public void ClampUnits_TimedCode_NullDurationWithZeroSuggestion_ReturnsOne()
    {
        _sut.ClampUnits("97530", suggestedUnits: 0, sessionDurationMinutes: null)
            .Should().Be(1);
    }

    // ── ClampUnits — untimed code ────────────────────────────────────────────

    [Theory]
    [InlineData("97165", 1)]
    [InlineData("97165", 3)]   // even if LLM suggests 3, untimed → always 1
    [InlineData("97535", 2)]
    [InlineData("97150", 5)]
    public void ClampUnits_UntimedCode_AlwaysReturnsOne(string code, int suggestedUnits)
    {
        _sut.ClampUnits(code, suggestedUnits, sessionDurationMinutes: 60)
            .Should().Be(1);
    }

    // ── Code lookup is case-insensitive ──────────────────────────────────────

    [Fact]
    public void ClampUnits_CodeLookupIsCaseInsensitive()
    {
        _sut.ClampUnits("97535", 2, 60).Should().Be(1);  // upper
    }

    [Fact]
    public void ClampUnits_CodeWithLeadingSpaces_IsHandledGracefully()
    {
        // The CMS calculator trims whitespace from the code before lookup
        _sut.ClampUnits(" 97535 ", 2, 60).Should().Be(1);
    }
}
