using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Activities;

namespace Theragraf.Tests.Activities;

public class PersistActivityTests
{
    private readonly ISessionRepository _repository;
    private readonly PersistActivity _sut;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly SoapNote RedactedNote  = new("[PERSON_1] attended.", "Objective.", "Assessment.", "Plan.");
    private static readonly SoapNote RestoredNote  = new("Jane Doe attended.", "Objective.", "Assessment.", "Plan.");
    private static readonly IReadOnlyDictionary<string, string> RedactionMap =
        new Dictionary<string, string> { ["[PERSON_1]"] = "Jane Doe" };
    private static readonly CptCode  CptCode1      = new("97530", "Therapeutic activities", "Rationale A");
    private static readonly IcdCode  IcdCode1      = new("F82", "Developmental coordination disorder", "Rationale B");

    private static readonly DateTimeOffset SessionDate =
        new(2024, 10, 10, 10, 0, 0, TimeSpan.Zero);

    private static TranscriptInput BuildInput(string clientId = "client-001") =>
        new("Raw transcript.", "Dr. Adams", clientId, SessionDate,
            TherapyDiscipline.OccupationalTherapy, 45);

    private static FinalizeResult BuildResult() =>
        new(RestoredNote, new List<CptCode> { CptCode1 }, new List<IcdCode> { IcdCode1 });

    public PersistActivityTests()
    {
        _repository = Substitute.For<ISessionRepository>();
        _sut = new PersistActivity(_repository);
    }

    [Fact]
    public async Task Run_CallsSaveAsync()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(Arg.Any<SessionRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SetsPartitionKeyToClientId()
    {
        var input = new PersistActivityInput(BuildInput("client-xyz"), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r => r.PartitionKey == "client-xyz"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SetsRowKeyFromSessionDate()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);
        var expectedRowKey = SessionDate.ToString("yyyy-MM-ddTHH-mm-ssZ");

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r => r.RowKey == expectedRowKey),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_StoresRedactedNoteNotRestoredNote()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r =>
                r.SoapNoteJson.Contains("[PERSON_1]") &&
                !r.SoapNoteJson.Contains("Jane Doe")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SerializesCptCodesToJson()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r =>
                r.CptCodesJson.Contains("97530") &&
                r.CptCodesJson.Contains("Therapeutic activities")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SerializesIcdCodesToJson()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r =>
                r.IcdCodesJson.Contains("F82") &&
                r.IcdCodesJson.Contains("Developmental coordination disorder")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SetsDisciplineAsString()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r => r.Discipline == "OccupationalTherapy"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SetsSessionDurationMinutes()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r => r.SessionDurationMinutes == 45),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_SerializesRedactionMapToJson()
    {
        var input = new PersistActivityInput(BuildInput(), BuildResult(), RedactedNote, RedactionMap);

        await _sut.Run(input);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SessionRecord>(r =>
                r.RedactionMapJson.Contains("[PERSON_1]") &&
                r.RedactionMapJson.Contains("Jane Doe")),
            Arg.Any<CancellationToken>());
    }
}
