using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;
using Theragraf.Functions.Agents;

namespace Theragraf.Tests.Activities;

public class SoapActivityTests
{
    private readonly ISoapAgent _soapAgent;
    private readonly SoapActivity _sut;

    public SoapActivityTests()
    {
        _soapAgent = Substitute.For<ISoapAgent>();
        _sut = new SoapActivity(_soapAgent, NullLoggerFactory.Instance);
    }

    private static ObservationResult BuildObservation(string transcript = "Patient reports anxiety.") =>
        new(transcript, new Dictionary<string, string>(), "Dr. Adams", "client-001", DateTimeOffset.UtcNow);

    private static SoapNote BuildSoapNote() =>
        new("Subjective text", "Objective text", "Assessment text", "Plan text");

    [Fact]
    public async Task Run_DelegatesToSoapAgent()
    {
        var input = BuildObservation();
        _soapAgent.GenerateSoapNoteAsync(input).Returns(BuildSoapNote());

        await _sut.Run(input);

        await _soapAgent.Received(1).GenerateSoapNoteAsync(input);
    }

    [Fact]
    public async Task Run_ReturnsSoapNoteFromAgent()
    {
        var input = BuildObservation();
        var expected = BuildSoapNote();
        _soapAgent.GenerateSoapNoteAsync(input).Returns(expected);

        var result = await _sut.Run(input);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Run_AgentReturnsCorrectSections()
    {
        var input = BuildObservation();
        var note = new SoapNote("Patient anxious", "HR 90", "Anxiety disorder", "CBT weekly");
        _soapAgent.GenerateSoapNoteAsync(input).Returns(note);

        var result = await _sut.Run(input);

        result.Subjective.Should().Be("Patient anxious");
        result.Objective.Should().Be("HR 90");
        result.Assessment.Should().Be("Anxiety disorder");
        result.Plan.Should().Be("CBT weekly");
    }

    [Fact]
    public async Task Run_AgentThrows_ExceptionPropagates()
    {
        var input = BuildObservation();
        _soapAgent.GenerateSoapNoteAsync(Arg.Any<ObservationResult>())
            .Returns<SoapNote>(_ => throw new InvalidOperationException("LLM unavailable"));

        var act = async () => await _sut.Run(input);

        await act.Should().ThrowAsync<AgentException>()
                 .WithMessage("LLM unavailable");
    }
}
