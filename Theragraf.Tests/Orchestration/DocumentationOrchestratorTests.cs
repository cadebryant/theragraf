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

    /// <summary>
    /// Configures all activity stubs with default return values.
    /// Uses Arg.Any&lt;TaskOptions?&gt;() so tests remain valid after retry options were added.
    /// </summary>
    private void ConfigureActivityStubs(
        ObservationResult? observation = null,
        SoapNote? soapNote = null,
        SoapNote? complianceNote = null,
        FinalizeResult? finalizeResult = null,
        IReadOnlyList<CptCode>? cptCodes = null,
        IReadOnlyList<IcdCode>? icdCodes = null)
    {
        observation    ??= BuildObservation();
        soapNote       ??= BuildSoapNote("_soap");
        complianceNote ??= BuildSoapNote("_compliant");
        finalizeResult ??= BuildFinalizeResult("_final");
        cptCodes       ??= Array.Empty<CptCode>();
        icdCodes       ??= Array.Empty<IcdCode>();

        _context.CallActivityAsync<ObservationResult>("IngestionActivity",  Arg.Any<object?>(), Arg.Any<TaskOptions?>()).Returns(observation);
        _context.CallActivityAsync<SoapNote>("SoapActivity",                Arg.Any<object?>(), Arg.Any<TaskOptions?>()).Returns(soapNote);
        _context.CallActivityAsync<SoapNote>("ComplianceActivity",          Arg.Any<object?>(), Arg.Any<TaskOptions?>()).Returns(complianceNote);
        _context.CallActivityAsync<FinalizeResult>("FinalizerActivity",     Arg.Any<object?>(), Arg.Any<TaskOptions?>()).Returns(finalizeResult);
        _context.CallActivityAsync<IReadOnlyList<CptCode>>("BillingActivity", Arg.Any<object?>(), Arg.Any<TaskOptions?>()).Returns(cptCodes);
        _context.CallActivityAsync<IReadOnlyList<IcdCode>>("Icd10Activity", Arg.Any<object?>(), Arg.Any<TaskOptions?>()).Returns(icdCodes);
    }

    [Fact]
    public async Task RunOrchestrator_CallsAllActivitiesInOrder()
    {
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        ConfigureActivityStubs();

        await _sut.RunOrchestrator(_context);

        Received.InOrder(() =>
        {
            _ = _context.CallActivityAsync<ObservationResult>("IngestionActivity", Arg.Any<object?>(), Arg.Any<TaskOptions?>());
            _ = _context.CallActivityAsync<SoapNote>("SoapActivity",               Arg.Any<object?>(), Arg.Any<TaskOptions?>());
            _ = _context.CallActivityAsync<SoapNote>("ComplianceActivity",         Arg.Any<object?>(), Arg.Any<TaskOptions?>());
            _ = _context.CallActivityAsync<FinalizeResult>("FinalizerActivity",    Arg.Any<object?>(), Arg.Any<TaskOptions?>());
        });
    }

    [Fact]
    public async Task RunOrchestrator_ReturnsRestoredNoteFromFinalizerActivity()
    {
        var expectedNote = BuildSoapNote("_final");
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        ConfigureActivityStubs(finalizeResult: new FinalizeResult(expectedNote, Array.Empty<CptCode>(), Array.Empty<IcdCode>()));

        var result = await _sut.RunOrchestrator(_context);

        result.RestoredNote.Should().Be(expectedNote);
    }

    [Fact]
    public async Task RunOrchestrator_PassesObservationResultToSoapActivity()
    {
        var observation = BuildObservation("Redacted text.");
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        ConfigureActivityStubs(observation: observation);

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<SoapNote>("SoapActivity", observation, Arg.Any<TaskOptions?>());
    }

    [Fact]
    public async Task RunOrchestrator_PassesSoapNoteToComplianceActivity()
    {
        var soapNote = BuildSoapNote("_soap");
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        ConfigureActivityStubs(soapNote: soapNote);

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<SoapNote>("ComplianceActivity", soapNote, Arg.Any<TaskOptions?>());
    }

    [Fact]
    public async Task RunOrchestrator_PassesFinalizeInputWithRedactionMapToFinalizerActivity()
    {
        var observation    = BuildObservation();
        var complianceNote = BuildSoapNote("_compliant");
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        ConfigureActivityStubs(observation: observation, complianceNote: complianceNote);

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<FinalizeResult>(
            "FinalizerActivity",
            Arg.Is<FinalizeInput>(fi =>
                fi.Note == complianceNote &&
                fi.RedactionMap == observation.RedactionMap),
            Arg.Any<TaskOptions?>());
    }

    [Fact]
    public async Task RunOrchestrator_PassesRestoredNoteAndDisciplineToBillingActivity()
    {
        var input    = BuildTranscriptInput();
        var finalized = BuildFinalizeResult("_final");
        _context.GetInput<TranscriptInput>().Returns(input);
        ConfigureActivityStubs(finalizeResult: finalized);

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<IReadOnlyList<CptCode>>(
            "BillingActivity",
            Arg.Is<BillingActivityInput>(b =>
                b.Note == finalized.RestoredNote &&
                b.Discipline == input.Discipline &&
                b.SessionDurationMinutes == input.SessionDurationMinutes &&
                b.Setting == input.Setting &&
                b.Payer == input.Payer),
            Arg.Any<TaskOptions?>());
    }

    [Fact]
    public async Task RunOrchestrator_PassesRestoredNoteAndDisciplineToIcd10Activity()
    {
        var input    = BuildTranscriptInput();
        var finalized = BuildFinalizeResult("_final");
        _context.GetInput<TranscriptInput>().Returns(input);
        ConfigureActivityStubs(finalizeResult: finalized);

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync<IReadOnlyList<IcdCode>>(
            "Icd10Activity",
            Arg.Is<Icd10ActivityInput>(i =>
                i.Note == finalized.RestoredNote &&
                i.Discipline == input.Discipline),
            Arg.Any<TaskOptions?>());
    }

    [Fact]
    public async Task RunOrchestrator_CallsPersistActivityWithComplianceNoteAndCodes()
    {
        var input          = BuildTranscriptInput();
        var observation    = BuildObservation();
        var complianceNote = BuildSoapNote("_compliant");
        var finalized      = BuildFinalizeResult("_final");
        var cptCodes       = new[] { new CptCode("97530", "Therapeutic activities", "Reason") };
        var icdCodes       = new[] { new IcdCode("F82", "Coordination disorder", "Reason") };
        _context.GetInput<TranscriptInput>().Returns(input);
        ConfigureActivityStubs(
            observation: observation,
            complianceNote: complianceNote,
            finalizeResult: finalized,
            cptCodes: cptCodes,
            icdCodes: icdCodes);

        await _sut.RunOrchestrator(_context);

        await _context.Received(1).CallActivityAsync(
            "PersistActivity",
            Arg.Is<PersistActivityInput>(p =>
                p.RedactedNote == complianceNote &&
                p.OriginalInput == input &&
                p.RedactionMap == observation.RedactionMap),
            Arg.Any<TaskOptions?>());
    }

    [Fact]
    public async Task RunOrchestrator_ResultContainsMergedCptAndIcdCodes()
    {
        var finalized = BuildFinalizeResult("_final");
        var cptCodes  = new[] { new CptCode("97530", "Therapeutic activities", "Reason") };
        var icdCodes  = new[] { new IcdCode("F82", "Coordination disorder", "Reason") };
        _context.GetInput<TranscriptInput>().Returns(BuildTranscriptInput());
        ConfigureActivityStubs(
            finalizeResult: finalized,
            cptCodes: cptCodes,
            icdCodes: icdCodes);

        var result = await _sut.RunOrchestrator(_context);

        result.SuggestedCptCodes.Should().BeEquivalentTo(cptCodes);
        result.SuggestedIcdCodes.Should().BeEquivalentTo(icdCodes);
    }
}

