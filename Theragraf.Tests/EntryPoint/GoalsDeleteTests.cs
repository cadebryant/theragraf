using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Services;
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class GoalsDeleteTests
{
    private readonly IGoalRepository _repository;
    private readonly GoalsDelete _sut;

    private const string OwnerEmail  = "alice@example.com";
    private const string RawClientId = "patient-001";
    private const string GoalId      = "goal-abc-123";
    private static readonly string OwnedClientId = ClientIdHelper.Namespace(OwnerEmail, RawClientId);

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration EnabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public GoalsDeleteTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _sut = new GoalsDelete(_repository, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
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

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingGoal_Returns204()
    {
        _repository.DeleteAsync(OwnedClientId, GoalId, Arg.Any<CancellationToken>())
                   .Returns(true);

        var response = await _sut.Delete(BuildRequest(), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_CallsRepository_WithCorrectIds()
    {
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(true);

        await _sut.Delete(BuildRequest(), OwnedClientId, GoalId, CancellationToken.None);

        await _repository.Received(1).DeleteAsync(OwnedClientId, GoalId, Arg.Any<CancellationToken>());
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_MissingClientId_Returns400()
    {
        var response = await _sut.Delete(BuildRequest(), "", GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_MissingGoalId_Returns400()
    {
        var response = await _sut.Delete(BuildRequest(), OwnedClientId, "", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_GoalNotFound_Returns404()
    {
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(false);

        var response = await _sut.Delete(BuildRequest(), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new GoalsDelete(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Delete(BuildRequest(), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WrongTherapist_Returns403()
    {
        var sut = new GoalsDelete(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Delete(BuildAuthenticatedRequest("other@example.com"), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_CorrectTherapist_Returns204()
    {
        var sut = new GoalsDelete(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.DeleteAsync(OwnedClientId, GoalId, Arg.Any<CancellationToken>()).Returns(true);

        var response = await sut.Delete(BuildAuthenticatedRequest(OwnerEmail), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RepositoryThrows_Returns500()
    {
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns<bool>(_ => throw new Exception("Cosmos failure"));

        var response = await _sut.Delete(BuildRequest(), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
