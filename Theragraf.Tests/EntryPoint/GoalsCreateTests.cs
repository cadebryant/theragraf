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

public class GoalsCreateTests
{
    private readonly IGoalRepository _repository;
    private readonly GoalsCreate _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string OwnerEmail  = "alice@example.com";
    private const string RawClientId = "patient-001";
    private static readonly string OwnedClientId = ClientIdHelper.Namespace(OwnerEmail, RawClientId);

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration EnabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public GoalsCreateTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _sut = new GoalsCreate(_repository, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildRequest(object? body)
    {
        var json   = body is string s ? s : JsonSerializer.Serialize(body, JsonOptions);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(stream);
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

    private static HttpRequestData BuildAuthenticatedRequest(string email, object? body = null)
    {
        var items = new Dictionary<object, object>
        {
            ["ClaimsPrincipal"] = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("preferred_username", email) }, "test"))
        };
        var json    = JsonSerializer.Serialize(body ?? new { title = "Goal", description = "Desc" }, JsonOptions);
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(items);
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(new MemoryStream(Encoding.UTF8.GetBytes(json)));
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

    private GoalResponse SampleGoal(string title = "Improve dressing") =>
        new(Guid.NewGuid().ToString(), OwnedClientId, title, "Desc", GoalStatus.Active,
            DateTimeOffset.UtcNow, null, null, [], false);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201()
    {
        _repository.CreateAsync(OwnedClientId, Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal());

        var req = BuildRequest(new { title = "Improve dressing", description = "SMART goal" });
        var response = await _sut.Create(BuildRequest(new { title = "Improve dressing", description = "SMART goal" }), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_CallsRepository_WithCorrectClientId()
    {
        _repository.CreateAsync(Arg.Any<string>(), Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal());

        await _sut.Create(BuildRequest(new { title = "Goal" }), OwnedClientId, CancellationToken.None);

        await _repository.Received(1).CreateAsync(
            OwnedClientId,
            Arg.Is<CreateGoalRequest>(r => r.Title == "Goal"),
            Arg.Any<CancellationToken>());
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_MissingClientId_Returns400()
    {
        var response = await _sut.Create(BuildRequest(new { title = "Goal" }), "", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_EmptyTitle_Returns400()
    {
        var response = await _sut.Create(BuildRequest(new { title = "  ", description = "Desc" }), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_NullTitle_Returns400()
    {
        var response = await _sut.Create(BuildRequest(new { description = "Desc" }), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_InvalidJson_Returns400()
    {
        var response = await _sut.Create(BuildRequest("not-valid-json{{{"), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new GoalsCreate(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Create(BuildRequest(new { title = "Goal" }), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WrongTherapist_Returns403()
    {
        var sut = new GoalsCreate(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Create(BuildAuthenticatedRequest("other@example.com"), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_CorrectTherapist_Returns201()
    {
        var sut = new GoalsCreate(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.CreateAsync(OwnedClientId, Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal());

        var response = await sut.Create(BuildAuthenticatedRequest(OwnerEmail), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_RepositoryThrows_Returns500()
    {
        _repository.CreateAsync(Arg.Any<string>(), Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns<GoalResponse>(_ => throw new Exception("Cosmos failure"));

        var response = await _sut.Create(BuildRequest(new { title = "Goal" }), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
