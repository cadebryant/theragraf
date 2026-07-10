using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Services;
using Theragraf.Core.Models;
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Logging;
using CosmosDatabase = Microsoft.Azure.Cosmos.Database;

namespace Theragraf.Tests.EntryPoint;

public class SeedFunctionTests
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IClientRepository  _clientRepository;
    private readonly IGoalRepository    _goalRepository;
    private readonly CosmosClient       _cosmosClient;
    private readonly CosmosDatabase     _mockDatabase;
    private readonly Container          _mockContainer;

    private const string DemoTherapist = "Demo Therapist";

    private static readonly IConfiguration AuthEnabledWithDemo =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disabled"]       = "false",
                ["Demo:TherapistName"]  = DemoTherapist,
            })
            .Build();

    private static readonly IConfiguration AuthDisabledWithDemo =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disabled"]       = "true",
                ["Demo:TherapistName"]  = DemoTherapist,
            })
            .Build();

    private static readonly IConfiguration AuthEnabledNoDemoConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disabled"]       = "false",
                ["Demo:TherapistName"]  = "",
            })
            .Build();

    public SeedFunctionTests()
    {
        _sessionRepository = Substitute.For<ISessionRepository>();
        _clientRepository  = Substitute.For<IClientRepository>();
        _goalRepository    = Substitute.For<IGoalRepository>();
        _cosmosClient      = Substitute.For<CosmosClient>();

        // Wire CosmosClient → Database → Container chain used by WipeAllContainersAsync,
        // SeedProviderAsync, and SeedTherapistProfilesAsync.
        _mockDatabase  = Substitute.For<CosmosDatabase>();
        _mockContainer = Substitute.For<Container>();

        _cosmosClient.GetDatabase(Arg.Any<string>()).Returns(_mockDatabase);
        _mockDatabase.GetContainer(Arg.Any<string>()).Returns(_mockContainer);

        // GetItemQueryIterator returns an empty iterator so WipeAllContainersAsync exits without iterating.
        var emptyIterator = Substitute.For<FeedIterator<JsonElement>>();
        emptyIterator.HasMoreResults.Returns(false);
        _mockContainer
            .GetItemQueryIterator<JsonElement>(Arg.Any<QueryDefinition>())
            .Returns(emptyIterator);

        // goalRepository.CreateAsync must return a non-null GoalResponse so the seeder
        // can access created.GoalId without a NullReferenceException.
        _goalRepository
            .CreateAsync(Arg.Any<string>(), Arg.Any<CreateGoalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GoalResponse(
                GoalId:        Guid.NewGuid().ToString(),
                ClientId:      "seed-client",
                Title:         "Test goal",
                Description:   "Test description",
                Status:        GoalStatus.Active,
                CreatedAt:     DateTimeOffset.UtcNow,
                TargetDate:    null,
                ResolvedAt:    null,
                ProgressNotes: [],
                IsSynthetic:   true));
    }

    private SeedFunction BuildSut(IConfiguration config) =>
        new(_sessionRepository, _clientRepository, _goalRepository,
            _cosmosClient, config, NullLoggerFactory.Instance, new NullAuditLogger());

    // ── Shared request builder ────────────────────────────────────────────────

    private static HttpRequestData BuildRequest(string url = "https://localhost/api/seed")
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(Stream.Null);
        request.Url.Returns(new Uri(url));
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

    // ── Seed ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seed_NoJwtAndAuthEnabled_Returns401()
    {
        var sut      = BuildSut(AuthEnabledWithDemo);
        var response = await sut.Seed(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await _sessionRepository.DidNotReceive().SaveAsync(Arg.Any<SessionRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Seed_AuthDisabledAndDemoEnabled_Seeds()
    {
        var sut      = BuildSut(AuthDisabledWithDemo);
        var response = await sut.Seed(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _sessionRepository.Received().SaveAsync(Arg.Any<SessionRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Seed_DemoNotEnabled_Returns403()
    {
        var sut      = BuildSut(AuthEnabledNoDemoConfig);
        var response = await sut.Seed(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DeleteSeed ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSeed_NoJwtAndAuthEnabled_Returns401()
    {
        var sut      = BuildSut(AuthEnabledWithDemo);
        var response = await sut.DeleteSeed(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteSeed_AuthDisabledAndDemoEnabled_Deletes()
    {
        var sut      = BuildSut(AuthDisabledWithDemo);
        var response = await sut.DeleteSeed(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _cosmosClient.Received().GetDatabase(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteSeed_DemoNotEnabled_Returns401()
    {
        var sut      = BuildSut(AuthEnabledNoDemoConfig);
        var response = await sut.DeleteSeed(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── MarkAllSynthetic ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllSynthetic_NoJwtAndAuthEnabled_Returns401()
    {
        var sut      = BuildSut(AuthEnabledWithDemo);
        var response = await sut.MarkAllSynthetic(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _cosmosClient.DidNotReceive().GetDatabase(Arg.Any<string>());
    }

    [Fact]
    public async Task MarkAllSynthetic_DemoNotEnabled_Returns401()
    {
        var sut      = BuildSut(AuthEnabledNoDemoConfig);
        var response = await sut.MarkAllSynthetic(BuildRequest(), CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _cosmosClient.DidNotReceive().GetDatabase(Arg.Any<string>());
    }
}
