using FluentAssertions;
using Microsoft.DurableTask;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Functions.Orchestration;

namespace Theragraf.Tests.Orchestration;

public class DocumentationOrchestratorTests
{
    private readonly TaskOrchestrationContext _context;
    private readonly DocumentationOrchestrator _sut;

    public DocumentationOrchestratorTests()
    {
        _context = Substitute.For<TaskOrchestrationContext>();
        _sut = new DocumentationOrchestrator();
    }

    private static SoapNote BuildSoapNote(string suffix = "") =>
        new($"Subjective{suffix}", $"Objective{suffix}", $"Assessment{suffix}", $"Plan{suffix}");

    [Fact]
    public async Task RunOrchestrator_CallsAllActivitiesInOrder()
    {
        var finalNote = BuildSoapNote("_final");

        _context.GetInput<string>().Returns("raw transcript");
        _context.CallActivityAsync<string>("IngestionActivity", Arg.Any<object>())
                .Returns("ingested");
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>())
                .Returns(BuildSoapNote("_soap"));
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>())
                .Returns(BuildSoapNote("_compliant"));
        _context.CallActivityAsync<SoapNote>("FinalizerActivity", Arg.Any<object>())
                .Returns(finalNote);

        await _sut.RunOrchestrator(_context);

        Received.InOrder(() =>
        {
            _ = _context.CallActivityAsync<string>("IngestionActivity", Arg.Any<object>());
            _ = _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>());
            _ = _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>());
            _ = _context.CallActivityAsync<SoapNote>("FinalizerActivity", Arg.Any<object>());
        });
    }

    [Fact]
    public async Task RunOrchestrator_ReturnsFinalNoteFromFinalizerActivity()
    {
        var finalNote = BuildSoapNote("_final");

        _context.GetInput<string>().Returns("raw transcript");
        _context.CallActivityAsync<string>("IngestionActivity", Arg.Any<object>()).Returns("ingested");
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("FinalizerActivity", Arg.Any<object>()).Returns(finalNote);

        var result = await _sut.RunOrchestrator(_context);

        result.Should().Be(finalNote);
    }

    [Fact]
    public async Task RunOrchestrator_PassesIngestionOutputToSoapActivity()
    {
        const string ingestionOutput = "processed transcript";

        _context.GetInput<string>().Returns("raw");
        _context.CallActivityAsync<string>("IngestionActivity", Arg.Any<object>()).Returns(ingestionOutput);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("FinalizerActivity", Arg.Any<object>()).Returns(BuildSoapNote());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<SoapNote>("SoapActivity", ingestionOutput);
    }

    [Fact]
    public async Task RunOrchestrator_PassesSoapNoteToComplianceActivity()
    {
        var soapNote = BuildSoapNote("_soap");

        _context.GetInput<string>().Returns("raw");
        _context.CallActivityAsync<string>("IngestionActivity", Arg.Any<object>()).Returns("ingested");
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(soapNote);
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("FinalizerActivity", Arg.Any<object>()).Returns(BuildSoapNote());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<SoapNote>("ComplianceActivity", soapNote);
    }
}
