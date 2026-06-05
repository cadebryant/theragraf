using FluentAssertions;
using Microsoft.DurableTask;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Functions.Activities;
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
        new(BuildSoapNote(suffix), Array.Empty<CptCode>(), Array.Empty<IcdCode>());

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
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

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
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(new FinalizeResult(expectedNote, Array.Empty<CptCode>(), Array.Empty<IcdCode>()));
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

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
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

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
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

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
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<FinalizeResult>(
            "FinalizerActivity",
            Arg.Is<FinalizeInput>(fi =>
                fi.Note == complianceNote &&
                fi.RedactionMap == observation.RedactionMap));
    }

    [Fact]
    public async Task RunOrchestrator_PassesRestoredNoteAndDisciplineToBillingActivity()
    {
        var input = BuildTranscriptInput();
        var observation = BuildObservation();
        var finalized = BuildFinalizeResult("_final");
        _context.GetInput<TranscriptInput>().Returns(input);
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(finalized);
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<IReadOnlyList<CptCode>>(
            "BillingActivity",
            Arg.Is<BillingActivityInput>(b =>
                b.Note == finalized.RestoredNote &&
                b.Discipline == input.Discipline &&
                b.SessionDurationMinutes == input.SessionDurationMinutes &&
                b.Setting == input.Setting &&
                b.Payer == input.Payer));
    }

    [Fact]
    public async Task RunOrchestrator_PassesRestoredNoteAndDisciplineToIcd10Activity()
    {
        var input = BuildTranscriptInput();
        var observation = BuildObservation();
        var finalized = BuildFinalizeResult("_final");
        _context.GetInput<TranscriptInput>().Returns(input);
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(finalized);
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns(Array.Empty<CptCode>());
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns(Array.Empty<IcdCode>());

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<IReadOnlyList<IcdCode>>(
            "Icd10Activity",
            Arg.Is<Icd10ActivityInput>(i =>
                i.Note == finalized.RestoredNote &&
                i.Discipline == input.Discipline));
    }

    [Fact]
    public async Task RunOrchestrator_CallsPersistActivityWithComplianceNoteAndCodes()
    {
        var input = BuildTranscriptInput();
        var observation = BuildObservation();
        var complianceNote = BuildSoapNote("_compliant");
        var finalized = BuildFinalizeResult("_final");
        var cptCodes = new[] { new CptCode("97530", "Therapeutic activities", "Reason") };
        var icdCodes = new[] { new IcdCode("F82", "Coordination disorder", "Reason") };
        _context.GetInput<TranscriptInput>().Returns(input);
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(complianceNote);
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(finalized);
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns((IReadOnlyList<CptCode>)cptCodes);
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns((IReadOnlyList<IcdCode>)icdCodes);

        await _sut.RunOrchestrator(_context);

        // PersistActivity receives the compliance (redacted) note — not the restored note
        // and must include the redaction map so reads can restore PII
        await _context.Received(1).CallActivityAsync(
            "PersistActivity",
            Arg.Is<PersistActivityInput>(p =>
                p.RedactedNote == complianceNote &&
                p.OriginalInput == input &&
                p.RedactionMap == observation.RedactionMap));
    }

    [Fact]
    public async Task RunOrchestrator_ResultContainsMergedCptAndIcdCodes()
    {
        var observation = BuildObservation();
        var finalized = BuildFinalizeResult("_final");
        var cptCodes = new[] { new CptCode("97530", "Therapeutic activities", "Reason") };
        var icdCodes = new[] { new IcdCode("F82", "Coordination disorder", "Reason") };
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<SoapNote>("ComplianceActivity", Arg.Any<object>()).Returns(BuildSoapNote());
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity", Arg.Any<object>()).Returns(finalized);
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object>()).Returns((IReadOnlyList<CptCode>)cptCodes);
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object>()).Returns((IReadOnlyList<IcdCode>)icdCodes);

        var result = await _sut.RunOrchestrator(_context);

        result.SuggestedCptCodes.Should().BeEquivalentTo(cptCodes);
        result.SuggestedIcdCodes.Should().BeEquivalentTo(icdCodes);
    }
}

