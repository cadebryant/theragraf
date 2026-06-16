using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;
using Theragraf.Functions.Agents;

namespace Theragraf.Tests.Activities;

public class ComplianceActivityTests
{
    private readonly IComplianceAgent _complianceAgent;
    private readonly ComplianceActivity _sut;

    public ComplianceActivityTests()
    {
        _complianceAgent = Substitute.For<IComplianceAgent>();
        _sut = new ComplianceActivity(_complianceAgent, NullLoggerFactory.Instance);
    }

    private static SoapNote BuildSoapNote(
        string subjective = "Patient reports anxiety.",
        string objective  = "Affect flat, mood 4/10.",
        string assessment = "Generalized Anxiety Disorder.",
        string plan       = "Continue CBT weekly.") =>
        new(subjective, objective, assessment, plan);

    private static ComplianceActivityInput BuildInput(SoapNote note, NoteFormat format = NoteFormat.Soap) =>
        new(note, format);

    private static ComplianceResult BuildCompliantResult(SoapNote note) =>
        new(note, IsCompliant: true, Issues: []);

    private static ComplianceResult BuildNonCompliantResult(SoapNote corrected, params string[] issues) =>
        new(corrected, IsCompliant: false, Issues: issues);

    [Fact]
    public async Task Run_DelegatesToComplianceAgent()
    {
        var note = BuildSoapNote();
        var input = BuildInput(note);
        _complianceAgent.ValidateAsync(note, NoteFormat.Soap).Returns(BuildCompliantResult(note));

        await _sut.Run(input);

        await _complianceAgent.Received(1).ValidateAsync(note, NoteFormat.Soap);
    }

    [Fact]
    public async Task Run_CompliantNote_ReturnsValidatedNoteUnchanged()
    {
        var note = BuildSoapNote();
        var input = BuildInput(note);
        _complianceAgent.ValidateAsync(note, NoteFormat.Soap).Returns(BuildCompliantResult(note));

        var result = await _sut.Run(input);

        result.Should().Be(note);
    }

    [Fact]
    public async Task Run_NonCompliantNote_ReturnsCorrectedNote()
    {
        var note      = BuildSoapNote(assessment: "unclear");
        var corrected = BuildSoapNote(assessment: "GAD — further assessment required.");
        var input = BuildInput(note);
        _complianceAgent.ValidateAsync(note, NoteFormat.Soap)
            .Returns(BuildNonCompliantResult(corrected, "Assessment is too vague."));

        var result = await _sut.Run(input);

        result.Assessment.Should().Be("GAD — further assessment required.");
    }

    [Fact]
    public async Task Run_ReturnsValidatedNoteNotRawInput()
    {
        var note      = BuildSoapNote(plan: "TBD");
        var corrected = BuildSoapNote(plan: "Schedule follow-up within 7 days.");
        var input = BuildInput(note);
        _complianceAgent.ValidateAsync(note, NoteFormat.Soap)
            .Returns(BuildNonCompliantResult(corrected, "Plan lacks specificity."));

        var result = await _sut.Run(input);

        result.Should().Be(corrected);
        result.Should().NotBe(note);
    }

    [Fact]
    public async Task Run_DapFormat_ForwardsDapFormatToAgent()
    {
        var note  = BuildSoapNote(subjective: "Client reported low mood and isolation.", objective: "");
        var input = BuildInput(note, NoteFormat.Dap);
        _complianceAgent.ValidateAsync(note, NoteFormat.Dap).Returns(BuildCompliantResult(note));

        await _sut.Run(input);

        await _complianceAgent.Received(1).ValidateAsync(note, NoteFormat.Dap);
    }

    [Fact]
    public async Task Run_AgentThrows_ExceptionPropagates()
    {
        var note = BuildSoapNote();
        var input = BuildInput(note);
        _complianceAgent.ValidateAsync(Arg.Any<SoapNote>(), Arg.Any<NoteFormat>())
            .Returns<ComplianceResult>(_ => throw new InvalidOperationException("LLM unavailable"));

        var act = async () => await _sut.Run(input);

        await act.Should().ThrowAsync<AgentException>()
                 .WithMessage("LLM unavailable");
    }
}
