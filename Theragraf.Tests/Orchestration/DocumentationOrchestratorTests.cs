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

    private static TranscriptInput BuildTranscriptInput() =>
        new("Raw session transcript.", "Dr. Adams", "client-001", DateTimeOffset.UtcNow);

    private static ObservationResult BuildObservation(string transcript = "Redacted transcript.") =>
        new(transcript, new Dictionary<string, string> { ["[PERSON_1]"] = "John Smith" }, "Dr. Adams", "client-001", DateTimeOffset.UtcNow);

    private static SoapNote BuildSoapNote(string suffix = "") =>
        new($"Subjective{suffix}", $"Objective{suffix}", $"Assessment{suffix}", $"Plan{suffix}");

    private static FinalizeResult BuildFinalizeResult(string suffix = "") =>
        new(BuildSoapNote(suffix), Array.Empty<CptCode>());

    [Fact]
    public async Task RunOrchestrator_CallsAllActivitiesInOrder()
    {
        var observation = BuildObservation();
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote("_soap"));
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote("_compliant"));
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(BuildFinalizeResult("_final"));
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());

        await _sut.RunOrchestrator(_context);

        Received.InOrder(() =>
        {
            _ = _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>());
            _ = _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>());
            _ = _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>());
            _ = _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>());
        });
    }

    [Fact]
    public async Task RunOrchestrator_ReturnsRestoredNoteFromFinalizerActivity()
    {
        var expectedNote = BuildSoapNote("_final");
        var observation = BuildObservation();
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(new FinalizeResult(expectedNote, Array.Empty<CptCode>()));
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());

        var result = await _sut.RunOrchestrator(_context);

        result.RestoredNote.Should().Be(expectedNote);
    }

    [Fact]
    public async Task RunOrchestrator_PassesObservationResultToSoapActivity()
    {
        var observation = BuildObservation("Redacted text.");
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(BuildFinalizeResult());
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<SoapNote>("SoapActivity", observation);
    }

    [Fact]
    public async Task RunOrchestrator_PassesSoapNoteToComplianceActivity()
    {
        var soapNote = BuildSoapNote("_soap");
        var observation = BuildObservation();
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(soapNote);
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(BuildFinalizeResult());
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<SoapNote>("ComplianceActivity", soapNote);
    }

    [Fact]
    public async Task RunOrchestrator_PassesFinalizeInputWithRedactionMapToFinalizerActivity()
    {
        var observation = BuildObservation();
        var complianceNote = BuildSoapNote("_compliant");
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(complianceNote);
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(BuildFinalizeResult());
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<FinalizeResult>(
            "FinalizerActivity",
            Arg.Is<FinalizeInput>(fi =>
                fi.Note == complianceNote &&
                fi.RedactionMap == observation.RedactionMap));
    }
}

