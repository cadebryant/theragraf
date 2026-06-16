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
using Theragraf.Functions.Agents;
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class GoalsSuggestTests
{
    private readonly IGoalAgent _goalAgent;
    private readonly GoalsSuggest _sut;
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

    private static readonly SoapNote SampleSoap =
        new("Patient reports pain.", "ROM 90°.", "Improving.", "Continue PT.");

    public GoalsSuggestTests()
    {
        _goalAgent = Substitute.For<IGoalAgent>();
        _sut = new GoalsSuggest(_goalAgent, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
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

    private static HttpRequestData BuildAuthenticatedRequest(string email, object? body = null)
    {
        var payload = body ?? new
        {
            soapNote   = SampleSoap,
            discipline = "PhysicalTherapy"
        };
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
        request.Body.Returns(new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions))));
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

    private static IReadOnlyList<GoalSuggestion> SampleSuggestions() =>
    [
        new GoalSuggestion("Improve knee flexion", "Client will achieve 110° knee flexion within 6 weeks."),
        new GoalSuggestion("Reduce pain levels",   "Client will report pain ≤3/10 during activity within 4 weeks.")
    ];

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggest_ValidRequest_Returns200WithSuggestions()
    {
        _goalAgent.SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>())
                  .Returns(SampleSuggestions());

        var body = new { soapNote = SampleSoap, discipline = "PhysicalTherapy" };
        var response = await _sut.Suggest(BuildRequest(body), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _goalAgent.Received(1).SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suggest_PassesDisciplineToAgent()
    {
        _goalAgent.SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>())
                  .Returns(SampleSuggestions());

        var body = new { soapNote = SampleSoap, discipline = "Psychotherapy" };
        await _sut.Suggest(BuildRequest(body), OwnedClientId, CancellationToken.None);

        await _goalAgent.Received(1).SuggestGoalsAsync(
            Arg.Any<SoapNote>(),
            Arg.Is<TherapyDiscipline>(d => d == TherapyDiscipline.Psychotherapy),
            Arg.Any<CancellationToken>());
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggest_MissingClientId_Returns400()
    {
        var body = new { soapNote = SampleSoap, discipline = "OccupationalTherapy" };
        var response = await _sut.Suggest(BuildRequest(body), "", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _goalAgent.DidNotReceive().SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suggest_MissingSoapNote_Returns400()
    {
        var body = new { discipline = "OccupationalTherapy" };
        var response = await _sut.Suggest(BuildRequest(body), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Suggest_InvalidJson_Returns400()
    {
        var response = await _sut.Suggest(BuildRequest("not-json{{{"), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggest_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new GoalsSuggest(_goalAgent, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var body = new { soapNote = SampleSoap, discipline = "OccupationalTherapy" };
        var response = await sut.Suggest(BuildRequest(body), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Suggest_WrongTherapist_Returns403()
    {
        var sut = new GoalsSuggest(_goalAgent, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        var response = await sut.Suggest(BuildAuthenticatedRequest("other@example.com"), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _goalAgent.DidNotReceive().SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suggest_CorrectTherapist_Returns200()
    {
        var sut = new GoalsSuggest(_goalAgent, EnabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _goalAgent.SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>())
                  .Returns(SampleSuggestions());

        var response = await sut.Suggest(BuildAuthenticatedRequest(OwnerEmail), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggest_AgentThrows_Returns500()
    {
        _goalAgent.SuggestGoalsAsync(Arg.Any<SoapNote>(), Arg.Any<TherapyDiscipline>(), Arg.Any<CancellationToken>())
                  .Returns<IReadOnlyList<GoalSuggestion>>(_ => throw new Exception("OpenAI failure"));

        var body = new { soapNote = SampleSoap, discipline = "OccupationalTherapy" };
        var response = await _sut.Suggest(BuildRequest(body), OwnedClientId, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
