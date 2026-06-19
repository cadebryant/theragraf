using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Logging;

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
        NoteFormat: "Soap",
        Setting: "Outpatient",
        Payer: "Medicare",
        SessionDurationMinutes: 45,
        SoapNote: new SoapNote("S", "O", "A", "P"),
        SuggestedCptCodes: new List<CptCode> { new("97530", "Therapeutic activities", "Rationale") },
        SuggestedIcdCodes: new List<IcdCode> { new("F82", "Coordination disorder", "Rationale") },
        CreatedAt: new DateTimeOffset(2024, 10, 10, 10, 0, 0, TimeSpan.Zero),
        IsApproved: false,
        ApprovedBy: null,
        ApprovedAt: null,
        IsSynthetic: false
    );

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    public SessionsGetTests()
    {
        _repository = Substitute.For<ISessionRepository>();
        _sut = new SessionsGet(_repository, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
    }

    private PagedResult<SessionResponse> SinglePage(IEnumerable<SessionResponse> items) =>
        new(items.ToList(), 20, false, null);

    private HttpRequestData BuildRequest(string url = "http://localhost/api/sessions/client-001")
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Url.Returns(new Uri(url));
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
        _repository.GetByClientIdPagedAsync("client-001", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([SampleSession]));

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClient_CallsRepositoryWithCorrectClientId()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(BuildRequest(), "client-abc", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync("client-abc", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_EmptyList_ReturnsOk()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByClient_WritesPagedEnvelopeToBody()
    {
        _repository.GetByClientIdPagedAsync("client-001", Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([SampleSession]));

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);

        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().Contain("items").And.Contain("pageSize").And.Contain("hasMore")
            .And.Contain("client-001").And.Contain("97530").And.Contain("F82");
    }

    [Fact]
    public async Task GetByClient_DefaultPageSizeIs20()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync("client-001", 20, Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_CustomPageSizePassedToRepository()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(BuildRequest("http://localhost/api/sessions/client-001?pageSize=5"), "client-001", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync("client-001", 5, Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_PageSizeClampedTo100()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(BuildRequest("http://localhost/api/sessions/client-001?pageSize=999"), "client-001", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync("client-001", 100, Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_ContinuationTokenPassedToRepository()
    {
        var token = "dGVzdA==";
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), token, Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(
            BuildRequest($"http://localhost/api/sessions/client-001?continuationToken={Uri.EscapeDataString(token)}"),
            "client-001", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync("client-001", 20, token, Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_HasMoreTrue_ContinuationTokenInResponse()
    {
        var pagedResult = new PagedResult<SessionResponse>([SampleSession], 1, true, "bmV4dA==");
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(pagedResult);

        var response = await _sut.GetByClient(
            BuildRequest("http://localhost/api/sessions/client-001?pageSize=1"), "client-001", CancellationToken.None);

        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().Contain("\"hasMore\":true").And.Contain("bmV4dA==");
    }

    [Fact]
    public async Task GetByClient_FilterParamsForwardedToRepository()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(
            BuildRequest("http://localhost/api/sessions/client-001?discipline=PT&payer=Medicare&therapist=Dr+Adams"),
            "client-001", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync(
            "client-001", 20, null,
            Arg.Is<SessionQueryOptions?>(o => o != null && o.Discipline == "PT" && o.Payer == "Medicare" && o.Therapist == "Dr Adams"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_SortParamsForwardedToRepository()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns(SinglePage([]));

        await _sut.GetByClient(
            BuildRequest("http://localhost/api/sessions/client-001?sortBy=therapistName&sortOrder=asc"),
            "client-001", CancellationToken.None);

        await _repository.Received(1).GetByClientIdPagedAsync(
            "client-001", 20, null,
            Arg.Is<SessionQueryOptions?>(o => o != null && o.SortBy == "therapistName" && o.SortOrder == "asc"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByClient_MissingClientId_ReturnsBadRequest()
    {
        var response = await _sut.GetByClient(BuildRequest(), "", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

        var response = await _sut.GetByClientAndDate(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetByClientAndDate_InvalidDateFormat_ReturnsBadRequest()
    {
        var req = BuildRequest();

        var response = await _sut.GetByClientAndDate(req, "client-001", "bad-date", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    // -- Ownership / caseload tests ------------------------------------------

    private static readonly IConfiguration AuthEnabledConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    private static HttpRequestData BuildAuthenticatedRequest(
        string therapistName,
        string url = "http://localhost/api/sessions/client-001")
    {
        var items = new Dictionary<object, object>();
        items["ClaimsPrincipal"] = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("preferred_username", therapistName) },
                "test"));

        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(items);

        var request = Substitute.For<HttpRequestData>(context);
        request.Url.Returns(new Uri(url));
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

    [Fact]
    public async Task GetByClientAndDate_WrongTherapist_Returns403()
    {
        var sut = new SessionsGet(_repository, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(SampleSession); // TherapistName = "Dr. Adams"

        var req = BuildAuthenticatedRequest("Dr. Other");
        var response = await sut.GetByClientAndDate(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetByClientAndDate_CorrectTherapist_Returns200()
    {
        var sut = new SessionsGet(_repository, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(SampleSession); // TherapistName = "Dr. Adams"

        var req = BuildAuthenticatedRequest("Dr. Adams");
        var response = await sut.GetByClientAndDate(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCaseload_ReturnsCaseloadSummary()
    {
        var sut = new SessionsGet(_repository, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetCaseloadAsync("Dr. Adams", Arg.Any<CancellationToken>())
            .Returns(new CaseloadSummary("Dr. Adams",
                new List<ClientSummary> { new("client-001", "2024-10-10T10-00-00Z", 3, false) }));

        var req = BuildAuthenticatedRequest("Dr. Adams", "http://localhost/api/sessions");
        var response = await sut.GetCaseload(req, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _repository.Received(1).GetCaseloadAsync("Dr. Adams", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCaseload_NotAuthenticated_Returns401()
    {
        var sut = new SessionsGet(_repository, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());

        var req = BuildRequest("http://localhost/api/sessions"); // no claims principal
        var response = await sut.GetCaseload(req, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _repository.DidNotReceive().GetCaseloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByClient_RepositoryThrows_Returns500WithCorrelationId()
    {
        _repository.GetByClientIdPagedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<SessionQueryOptions?>(), Arg.Any<CancellationToken>())
            .Returns<PagedResult<SessionResponse>>(_ => throw new Exception("Database timeout"));

        var response = await _sut.GetByClient(BuildRequest(), "client-001", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Verify X-Correlation-ID header
        response.Headers.TryGetValues("X-Correlation-ID", out var correlationIds).Should().BeTrue();
        var correlationId = correlationIds!.First();
        correlationId.Should().MatchRegex("^[0-9a-f]{16}$");

        // Verify sanitized response (no "Database timeout" leak)
        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().NotContain("Database timeout");
        body.Should().Contain("retrieving sessions");
        body.Should().Contain(correlationId);
    }

    [Fact]
    public async Task GetCaseload_RepositoryThrows_Returns500WithCorrelationId()
    {
        _repository.GetCaseloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<CaseloadSummary>(_ => throw new InvalidOperationException("Connection lost"));

        var response = await _sut.GetCaseload(BuildRequest("http://localhost/api/sessions"), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Verify correlation ID header
        response.Headers.TryGetValues("X-Correlation-ID", out var correlationIds).Should().BeTrue();
        var correlationId = correlationIds!.First();
        correlationId.Should().HaveLength(16);

        // Verify error message is sanitized
        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().NotContain("Connection lost");
        body.Should().NotContain("InvalidOperationException");
        body.Should().Contain("retrieving the caseload");
    }
}
