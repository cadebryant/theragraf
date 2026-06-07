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

    private static ComplianceResult BuildCompliantResult(SoapNote note) =>
        new(note, IsCompliant: true, Issues: []);

    private static ComplianceResult BuildNonCompliantResult(SoapNote corrected, params string[] issues) =>
        new(corrected, IsCompliant: false, Issues: issues);

    [Fact]
    public async Task Run_DelegatesToComplianceAgent()
    {
        var input = BuildSoapNote();
        _complianceAgent.ValidateAsync(input).Returns(BuildCompliantResult(input));

        await _sut.Run(input);

        await _complianceAgent.Received(1).ValidateAsync(input);
    }

    [Fact]
    public async Task Run_CompliantNote_ReturnsValidatedNoteUnchanged()
    {
        var input = BuildSoapNote();
        _complianceAgent.ValidateAsync(input).Returns(BuildCompliantResult(input));

        var result = await _sut.Run(input);

        result.Should().Be(input);
    }

    [Fact]
    public async Task Run_NonCompliantNote_ReturnsCorrectedNote()
    {
        var input     = BuildSoapNote(assessment: "unclear");
        var corrected = BuildSoapNote(assessment: "GAD — further assessment required.");
        _complianceAgent.ValidateAsync(input)
            .Returns(BuildNonCompliantResult(corrected, "Assessment is too vague."));

        var result = await _sut.Run(input);

        result.Assessment.Should().Be("GAD — further assessment required.");
    }

    [Fact]
    public async Task Run_ReturnsValidatedNoteNotRawInput()
    {
        var input     = BuildSoapNote(plan: "TBD");
        var corrected = BuildSoapNote(plan: "Schedule follow-up within 7 days.");
        _complianceAgent.ValidateAsync(input)
            .Returns(BuildNonCompliantResult(corrected, "Plan lacks specificity."));

        var result = await _sut.Run(input);

        result.Should().Be(corrected);
        result.Should().NotBe(input);
    }

    [Fact]
    public async Task Run_AgentThrows_ExceptionPropagates()
    {
        var input = BuildSoapNote();
        _complianceAgent.ValidateAsync(Arg.Any<SoapNote>())
            .Returns<ComplianceResult>(_ => throw new InvalidOperationException("LLM unavailable"));

        var act = async () => await _sut.Run(input);

        await act.Should().ThrowAsync<AgentException>()
                 .WithMessage("LLM unavailable");
    }
}
