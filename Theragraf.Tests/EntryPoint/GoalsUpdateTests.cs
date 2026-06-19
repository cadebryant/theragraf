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

public class GoalsUpdateTests
{
    private readonly IGoalRepository _repository;
    private readonly GoalsUpdate _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public GoalsUpdateTests()
    {
        _repository = Substitute.For<IGoalRepository>();
        _sut = new GoalsUpdate(_repository, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildRequest(object? body)
    {
        var json    = body is string s ? s : JsonSerializer.Serialize(body, JsonOptions);
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
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
        request.Body.Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"title\":\"Updated\"}")));
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

    private static GoalResponse SampleGoal(GoalStatus status = GoalStatus.Active) =>
        new(GoalId, OwnedClientId, "Improve dressing", "Desc", status,
            DateTimeOffset.UtcNow, null, null, [], false);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingGoal_Returns200()
    {
        _repository.UpdateAsync(OwnedClientId, GoalId, Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal());

        var response = await _sut.Update(BuildRequest(new { title = "New title" }), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_StatusChange_ToMet_IsPassedToRepository()
    {
        _repository.UpdateAsync(OwnedClientId, GoalId, Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal(GoalStatus.Met));

        await _sut.Update(BuildRequest(new { status = "Met" }), OwnedClientId, GoalId, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(
            OwnedClientId, GoalId,
            Arg.Is<UpdateGoalRequest>(r => r.Status == GoalStatus.Met),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithProgressNote_IsPassedToRepository()
    {
        _repository.UpdateAsync(OwnedClientId, GoalId, Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal());

        await _sut.Update(BuildRequest(new { progressNote = "Good progress today" }), OwnedClientId, GoalId, CancellationToken.None);

        await _repository.Received(1).UpdateAsync(
            OwnedClientId, GoalId,
            Arg.Is<UpdateGoalRequest>(r => r.ProgressNote == "Good progress today"),
            Arg.Any<CancellationToken>());
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_MissingClientId_Returns400()
    {
        var response = await _sut.Update(BuildRequest(new { title = "X" }), "", GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_MissingGoalId_Returns400()
    {
        var response = await _sut.Update(BuildRequest(new { title = "X" }), OwnedClientId, "", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_InvalidJson_Returns400()
    {
        var response = await _sut.Update(BuildRequest("not-json{{{"), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_GoalNotFound_Returns404()
    {
        _repository.UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns((GoalResponse?)null);

        var response = await _sut.Update(BuildRequest(new { title = "X" }), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new GoalsUpdate(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Update(BuildRequest(new { title = "X" }), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_WrongTherapist_Returns403()
    {
        var sut = new GoalsUpdate(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Update(BuildAuthenticatedRequest("other@example.com"), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_CorrectTherapist_Returns200()
    {
        var sut = new GoalsUpdate(_repository, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.UpdateAsync(OwnedClientId, GoalId, Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns(SampleGoal());

        var response = await sut.Update(BuildAuthenticatedRequest(OwnerEmail), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_RepositoryThrows_Returns500()
    {
        _repository.UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UpdateGoalRequest>(), Arg.Any<CancellationToken>())
                   .Returns<GoalResponse?>(_ => throw new Exception("Cosmos failure"));

        var response = await _sut.Update(BuildRequest(new { title = "X" }), OwnedClientId, GoalId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
