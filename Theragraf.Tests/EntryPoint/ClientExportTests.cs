using System.Net;
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
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class ClientExportTests
{
    private readonly IClientRepository  _clientRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IGoalRepository    _goalRepo;
    private readonly ClientExport       _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string OwnerEmail  = "therapist@example.com";
    private const string RawClientId = "patient-export-01";
    private static readonly string OwnedClientId = ClientIdHelper.Namespace(OwnerEmail, RawClientId);

    private static readonly IConfiguration AuthDisabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration AuthEnabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public ClientExportTests()
    {
        _clientRepo  = Substitute.For<IClientRepository>();
        _sessionRepo = Substitute.For<ISessionRepository>();
        _goalRepo    = Substitute.For<IGoalRepository>();

        _sut = new ClientExport(
            _clientRepo, _sessionRepo, _goalRepo,
            AuthDisabled, NullLoggerFactory.Instance, new NullAuditLogger());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildRequest()
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(Stream.Null);
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

    private static HttpRequestData BuildAuthenticatedRequest(string email)
    {
        var items = new Dictionary<object, object>
        {
            ["ClaimsPrincipal"] = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("preferred_username", email)], "test"))
        };
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(items);
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(Stream.Null);
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

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new ClientExport(
            _clientRepo, _sessionRepo, _goalRepo,
            AuthEnabled, NullLoggerFactory.Instance, new NullAuditLogger());

        var response = await sut.Export(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _sessionRepo.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_MissingClientId_Returns400()
    {
        var response = await _sut.Export(BuildRequest(), "  ", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Ownership ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_WrongTherapist_Returns403()
    {
        var sut = new ClientExport(
            _clientRepo, _sessionRepo, _goalRepo,
            AuthEnabled, NullLoggerFactory.Instance, new NullAuditLogger());

        var req      = BuildAuthenticatedRequest("other@example.com");
        var response = await sut.Export(req, OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _sessionRepo.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Success ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_CorrectTherapist_Returns200WithAllData()
    {
        var sut = new ClientExport(
            _clientRepo, _sessionRepo, _goalRepo,
            AuthEnabled, NullLoggerFactory.Instance, new NullAuditLogger());

        var demographics = new ClientDemographicsResponse(
            OwnedClientId, AgeYears: 45, BiologicalSex.Female,
            "Prior Diagnoses", "Limited ROM", DateTimeOffset.UtcNow, false);

        var sessions = new List<SessionResponse>
        {
            new(OwnedClientId, "2024-06-01T10-00-00Z", OwnerEmail,
                "OccupationalTherapy", "Soap", "Outpatient", "Medicare", 45,
                new SoapNote("S", "O", "A", "P"), [], [], DateTimeOffset.UtcNow,
                false, null, null, false)
        };

        var goals = new List<GoalResponse>
        {
            new("goal-1", OwnedClientId, "Improve ROM", "Description",
                GoalStatus.Active, DateTimeOffset.UtcNow, null, null, [], false)
        };

        _clientRepo.GetAsync(OwnedClientId, Arg.Any<CancellationToken>()).Returns(demographics);
        _sessionRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>()).Returns(sessions.AsReadOnly());
        _goalRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>()).Returns(goals.AsReadOnly());

        var req      = BuildAuthenticatedRequest(OwnerEmail);
        var response = await sut.Export(req, OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Body.Seek(0, SeekOrigin.Begin);
        var body   = await new StreamReader(response.Body).ReadToEndAsync();
        var export = JsonSerializer.Deserialize<ClientExportResponse>(body, JsonOptions);

        export.Should().NotBeNull();
        export!.ClientId.Should().Be(OwnedClientId);
        export.ExportedBy.Should().Be(OwnerEmail);
        export.Demographics.Should().NotBeNull();
        export.Sessions.Should().HaveCount(1);
        export.Goals.Should().HaveCount(1);
    }

    [Fact]
    public async Task Export_AuthDisabled_Returns200WithAllData()
    {
        _clientRepo.GetAsync(OwnedClientId, Arg.Any<CancellationToken>())
                   .Returns((ClientDemographicsResponse?)null);
        _sessionRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                    .Returns(Array.Empty<SessionResponse>());
        _goalRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalResponse>());

        var response = await _sut.Export(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_RepositoryThrows_Returns500WithCorrelationId()
    {
        _sessionRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<IReadOnlyList<SessionResponse>>(new InvalidOperationException("Cosmos error")));

        var response = await _sut.Export(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.Should().ContainKey("X-Correlation-ID");
    }

    [Fact]
    public async Task Export_ResponseHasNoCacheHeader()
    {
        _clientRepo.GetAsync(OwnedClientId, Arg.Any<CancellationToken>())
                   .Returns((ClientDemographicsResponse?)null);
        _sessionRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                    .Returns(Array.Empty<SessionResponse>());
        _goalRepo.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<GoalResponse>());

        var response = await _sut.Export(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.Headers.Should().ContainKey("Cache-Control");
        response.Headers.GetValues("Cache-Control").Should().Contain("no-store");
    }
}
