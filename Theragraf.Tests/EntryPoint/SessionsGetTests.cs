using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.EntryPoint;

namespace Theragraf.Tests.EntryPoint;

public class SessionsGetTests
{
    private readonly ISessionRepository _repository;
    private readonly SessionsGet _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly SessionResponse SampleSession = new(
        ClientId: "client-001",
        SessionDate: "2024-10-10T10-00-00Z",
        TherapistName: "Dr. Adams",
        Discipline: "OccupationalTherapy",
        Setting: "Outpatient",
        Payer: "Medicare",
        SessionDurationMinutes: 45,
        SoapNote: new SoapNote("S", "O", "A", "P"),
        SuggestedCptCodes: new List<CptCode> { new("97530", "Therapeutic activities", "Rationale") },
        SuggestedIcdCodes: new List<IcdCode> { new("F82", "Coordination disorder", "Rationale") },
        CreatedAt: new DateTimeOffset(2024, 10, 10, 10, 0, 0, TimeSpan.Zero)
    );

    public SessionsGetTests()
    {
        _repository = Substitute.For<ISessionRepository>();
        _sut = new SessionsGet(_repository, NullLoggerFactory.Instance);
    }

    private HttpRequestData BuildRequest()
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
            HttpStatusCode capturedStatus = HttpStatusCode.OK;
            response.When(r => r.StatusCode = Arg.Any<HttpStatusCode>())
                    .Do(ci => capturedStatus = ci.Arg<HttpStatusCode>());
            response.StatusCode.Returns(_ => capturedStatus);
            return response;
        });
        return request;
    }

    // ── GetByClient ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByClient_ReturnsOk()
    {
        _repository.GetByClientIdAsync("client-001", Arg.Any<CancellationToken>())
            .Returns(new List<SessionResponse> { SampleSession });
        var req = BuildRequest();

        var response = await _sut.GetByClient(req, "client-001", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClient_CallsRepositoryWithCorrectClientId()
    {
        _repository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<SessionResponse>());
        var req = BuildRequest();

        await _sut.GetByClient(req, "client-abc", CancellationToken.None);

        await _repository.Received(1).GetByClientIdAsync("client-abc", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_EmptyList_ReturnsOk()
    {
        _repository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<SessionResponse>());
        var req = BuildRequest();

        var response = await _sut.GetByClient(req, "client-001", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClient_WritesSessionJsonToBody()
    {
        _repository.GetByClientIdAsync("client-001", Arg.Any<CancellationToken>())
            .Returns(new List<SessionResponse> { SampleSession });
        var req = BuildRequest();

        var response = await _sut.GetByClient(req, "client-001", CancellationToken.None);

        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().Contain("client-001").And.Contain("97530").And.Contain("F82");
    }

    // ── GetByClientAndDate ───────────────────────────────────────────────────

    [Fact]
    public async Task GetByClientAndDate_SessionExists_ReturnsOk()
    {
        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(SampleSession);
        var req = BuildRequest();

        var response = await _sut.GetByClientAndDate(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClientAndDate_SessionNotFound_ReturnsNotFound()
    {
        _repository.GetByClientIdAndDateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SessionResponse?)null);
        var req = BuildRequest();

        var response = await _sut.GetByClientAndDate(req, "client-001", "bad-date", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByClientAndDate_CallsRepositoryWithCorrectKeys()
    {
        _repository.GetByClientIdAndDateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SampleSession);
        var req = BuildRequest();

        await _sut.GetByClientAndDate(req, "client-xyz", "2024-10-10T10-00-00Z", CancellationToken.None);

        await _repository.Received(1).GetByClientIdAndDateAsync("client-xyz", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClientAndDate_WritesSessionJsonToBody()
    {
        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(SampleSession);
        var req = BuildRequest();

        var response = await _sut.GetByClientAndDate(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().Contain("Dr. Adams").And.Contain("97530");
    }
}
