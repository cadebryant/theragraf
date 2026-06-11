using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.EntryPoint;

namespace Theragraf.Tests.EntryPoint;

public class StatsGetTests
{
    private readonly ISessionRepository _repository;
    private readonly StatsGet           _sut;

    public StatsGetTests()
    {
        _repository = Substitute.For<ISessionRepository>();
        var config  = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Demo:TherapistName"] = ""
        }).Build();
        _sut = new StatsGet(_repository, config, NullLoggerFactory.Instance);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildRequest()
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(new MemoryStream());
        request.CreateResponse().Returns(_ =>
        {
            var response = Substitute.For<HttpResponseData>(context);
            response.Body.Returns(new MemoryStream());
            response.Headers.Returns(new HttpHeadersCollection());
            HttpStatusCode captured = HttpStatusCode.OK;
            response.When(r => r.StatusCode = Arg.Any<HttpStatusCode>())
                    .Do(ci => captured = ci.Arg<HttpStatusCode>());
            response.StatusCode.Returns(_ => captured);
            return response;
        });
        return request;
    }

    private static TherapistStats EmptyTherapistStats(string name) => new(
        TherapistName:                name,
        TotalSessions:                0,
        TotalClients:                 0,
        AverageSessionDurationMinutes: 0.0,
        TotalBillableUnits:           0,
        SessionsByDiscipline:         new Dictionary<string, int>(),
        SessionsBySetting:            new Dictionary<string, int>(),
        SessionsByPayer:              new Dictionary<string, int>(),
        TopCptCodes:                  [],
        TopIcdCodes:                  []
    );

    private static TherapistStats PopulatedTherapistStats() => new(
        TherapistName:                "Dr. Smith",
        TotalSessions:                12,
        TotalClients:                 4,
        AverageSessionDurationMinutes: 47.5,
        TotalBillableUnits:           28,
        SessionsByDiscipline:         new Dictionary<string, int> { ["PT"] = 8, ["OT"] = 4 },
        SessionsBySetting:            new Dictionary<string, int> { ["Outpatient"] = 10, ["Home Health"] = 2 },
        SessionsByPayer:              new Dictionary<string, int> { ["Medicare"] = 9, ["Medicaid"] = 3 },
        TopCptCodes:                  [new CodeFrequency("97110", "Therapeutic Exercise", 10, 20)],
        TopIcdCodes:                  [new CodeFrequency("M54.5", "Low back pain", 6, 0)]
    );

    private static ClientStats EmptyClientStats(string id) => new(
        ClientId:                     id,
        TotalSessions:                0,
        AverageSessionDurationMinutes: 0.0,
        TotalBillableUnits:           0,
        FirstSessionDate:             null,
        LastSessionDate:              null,
        SessionsByTherapist:          new Dictionary<string, int>(),
        SessionsByDiscipline:         new Dictionary<string, int>(),
        SessionsBySetting:            new Dictionary<string, int>(),
        SessionsByPayer:              new Dictionary<string, int>(),
        TopCptCodes:                  [],
        TopIcdCodes:                  []
    );

    private static ClientStats PopulatedClientStats() => new(
        ClientId:                     "client-001",
        TotalSessions:                5,
        AverageSessionDurationMinutes: 45.0,
        TotalBillableUnits:           10,
        FirstSessionDate:             new DateTimeOffset(2024, 1, 10, 10, 0, 0, TimeSpan.Zero),
        LastSessionDate:              new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.Zero),
        SessionsByTherapist:          new Dictionary<string, int> { ["Dr. Smith"] = 5 },
        SessionsByDiscipline:         new Dictionary<string, int> { ["PT"] = 5 },
        SessionsBySetting:            new Dictionary<string, int> { ["Outpatient"] = 5 },
        SessionsByPayer:              new Dictionary<string, int> { ["Medicare"] = 5 },
        TopCptCodes:                  [new CodeFrequency("97110", "Therapeutic Exercise", 5, 10)],
        TopIcdCodes:                  [new CodeFrequency("M54.5", "Low back pain", 5, 0)]
    );

    // ── GetByTherapist: validation ────────────────────────────────────────────

    [Fact]
    public async Task GetByTherapist_MissingTherapistName_Returns400()
    {
        var response = await _sut.GetByTherapist(BuildRequest(), "", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetByTherapist_RepositoryThrows_Returns500()
    {
        _repository.GetTherapistStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .ThrowsAsync(new Exception("Cosmos failure"));

        var response = await _sut.GetByTherapist(BuildRequest(), "Dr. Smith", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── GetByTherapist: happy path ────────────────────────────────────────────

    [Fact]
    public async Task GetByTherapist_Returns200()
    {
        _repository.GetTherapistStatsAsync("Dr. Smith", Arg.Any<CancellationToken>())
                   .Returns(EmptyTherapistStats("Dr. Smith"));

        var response = await _sut.GetByTherapist(BuildRequest(), "Dr. Smith", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByTherapist_NoSessions_Returns200WithZeroedStats()
    {
        _repository.GetTherapistStatsAsync("Dr. Nobody", Arg.Any<CancellationToken>())
                   .Returns(EmptyTherapistStats("Dr. Nobody"));

        var response = await _sut.GetByTherapist(BuildRequest(), "Dr. Nobody", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByTherapist_CallsRepositoryWithCorrectTherapistName()
    {
        _repository.GetTherapistStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(EmptyTherapistStats("Dr. Smith"));

        await _sut.GetByTherapist(BuildRequest(), "Dr. Smith", CancellationToken.None);

        await _repository.Received(1).GetTherapistStatsAsync("Dr. Smith", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByTherapist_PopulatedStats_Returns200()
    {
        _repository.GetTherapistStatsAsync("Dr. Smith", Arg.Any<CancellationToken>())
                   .Returns(PopulatedTherapistStats());

        var response = await _sut.GetByTherapist(BuildRequest(), "Dr. Smith", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GetByClient: validation ───────────────────────────────────────────────

    [Fact]
    public async Task GetByClient_MissingClientId_Returns400()
    {
        var response = await _sut.GetByClient(BuildRequest(), "", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetByClient_RepositoryThrows_Returns500()
    {
        _repository.GetClientStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .ThrowsAsync(new Exception("Cosmos failure"));

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── GetByClient: happy path ───────────────────────────────────────────────

    [Fact]
    public async Task GetByClient_Returns200()
    {
        _repository.GetClientStatsAsync("client-001", Arg.Any<CancellationToken>())
                   .Returns(EmptyClientStats("client-001"));

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClient_NoSessions_Returns200WithZeroedStats()
    {
        _repository.GetClientStatsAsync("unknown-client", Arg.Any<CancellationToken>())
                   .Returns(EmptyClientStats("unknown-client"));

        var response = await _sut.GetByClient(BuildRequest(), "unknown-client", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClient_CallsRepositoryWithCorrectClientId()
    {
        _repository.GetClientStatsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(EmptyClientStats("client-001"));

        await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);

        await _repository.Received(1).GetClientStatsAsync("client-001", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_PopulatedStats_Returns200()
    {
        _repository.GetClientStatsAsync("client-001", Arg.Any<CancellationToken>())
                   .Returns(PopulatedClientStats());

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
