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
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class GoalsGetTests
{
    private readonly IGoalRepository _repository;
    private readonly GoalsGet _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string OwnerEmail  = "alice@example.com";
    private const string RawClientId = "patient-001";

    // A namespaced clientId that belongs to OwnerEmail.
    private static readonly string OwnedClientId =
        ClientIdHelper.Namespace(OwnerEmail, RawClientId);

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration EnabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public GoalsGetTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _sut = new GoalsGet(_repository, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
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
            var body = new MemoryStream();
            response.Body.Returns(body);
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
                    new[] { new System.Security.Claims.Claim("preferred_username", email) }, "test"))
        };
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(items);
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

    private static IReadOnlyList<GoalResponse> SampleGoals() =>
    [
        new GoalResponse(
            GoalId:        Guid.NewGuid().ToString(),
            ClientId:      OwnedClientId,
            Title:         "Improve independent dressing",
            Description:   "Client will independently don upper-body clothing within 4 weeks.",
            Status:        GoalStatus.Active,
            CreatedAt:     DateTimeOffset.UtcNow,
            TargetDate:    DateTimeOffset.UtcNow.AddDays(28),
            ResolvedAt:    null,
            ProgressNotes: [],
            IsSynthetic:   false
        )
    ];

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsGoals_Returns200()
    {
        _repository.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                   .Returns(SampleGoals());

        var response = await _sut.Get(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _repository.Received(1).GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_EmptyList_Returns200()
    {
        _repository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(Array.Empty<GoalResponse>());

        var response = await _sut.Get(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingClientId_Returns400()
    {
        var response = await _sut.Get(BuildRequest(), "", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new GoalsGet(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Get(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _repository.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_WrongTherapist_Returns403()
    {
        var sut = new GoalsGet(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var req = BuildAuthenticatedRequest("other@example.com");

        var response = await sut.Get(req, OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_CorrectTherapist_Returns200()
    {
        var sut = new GoalsGet(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetByClientIdAsync(OwnedClientId, Arg.Any<CancellationToken>())
                   .Returns(SampleGoals());

        var req = BuildAuthenticatedRequest(OwnerEmail);
        var response = await sut.Get(req, OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_UnprefixedClientId_IsAccessibleToAnyTherapist()
    {
        // Demo / unnamespaced records have no namespace prefix and are accessible to all.
        var sut = new GoalsGet(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetByClientIdAsync("demo-client", Arg.Any<CancellationToken>())
                   .Returns(Array.Empty<GoalResponse>());

        var req = BuildAuthenticatedRequest("anyone@example.com");
        var response = await sut.Get(req, "demo-client", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_RepositoryThrows_Returns500WithCorrelationId()
    {
        _repository.GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns<IReadOnlyList<GoalResponse>>(_ => throw new Exception("Cosmos failure"));

        var response = await _sut.Get(BuildRequest(), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Verify correlation ID header is present
        response.Headers.TryGetValues("X-Correlation-ID", out var correlationIds).Should().BeTrue();
        var correlationId = correlationIds!.First();
        correlationId.Should().NotBeNullOrEmpty();
        correlationId.Should().HaveLength(16);
        correlationId.Should().MatchRegex("^[0-9a-f]{16}$");

        // Verify response body is sanitized (doesn't leak "Cosmos failure")
        response.Body.Position = 0;
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        body.Should().NotContain("Cosmos failure");
        body.Should().NotContain("Exception");
        body.Should().Contain("retrieving goals");
        body.Should().Contain(correlationId);
    }
}
