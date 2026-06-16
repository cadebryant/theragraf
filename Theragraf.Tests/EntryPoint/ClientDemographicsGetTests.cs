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

public class ClientDemographicsGetTests
{
    private readonly IClientRepository _repository;
    private readonly ClientDemographicsGet _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string OwnerEmail  = "alice@example.com";
    private const string RawClientId = "patient-001";
    private static readonly string OwnedClientId = ClientIdHelper.Namespace(OwnerEmail, RawClientId);

    private static readonly IConfiguration AuthDisabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration AuthEnabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public ClientDemographicsGetTests()
    {
        _repository = Substitute.For<IClientRepository>();
        _sut = new ClientDemographicsGet(_repository, AuthDisabled, NullLoggerFactory.Instance, new NullAuditLogger());
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

    private static HttpRequestData BuildAuthRequest(string email)
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

    private static ClientDemographicsResponse SampleRecord(string clientId) =>
        new(clientId, 42, BiologicalSex.Female, "T2DM", "Limited ROM right shoulder", DateTimeOffset.UtcNow);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingClientId_Returns400()
    {
        var result = await _sut.Get(BuildRequest(), "", CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_RecordExists_Returns200()
    {
        _repository.GetAsync("patient-001", Arg.Any<CancellationToken>())
            .Returns(SampleRecord("patient-001"));

        var result = await _sut.Get(BuildRequest(), "patient-001", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_NoRecord_Returns404()
    {
        _repository.GetAsync("patient-missing", Arg.Any<CancellationToken>())
            .Returns((ClientDemographicsResponse?)null);

        var result = await _sut.Get(BuildRequest(), "patient-missing", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_NoJwtAndAuthEnabled_Returns401()
    {
        var sut = new ClientDemographicsGet(_repository, AuthEnabled, NullLoggerFactory.Instance, new NullAuditLogger());
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        // No ClaimsPrincipal in Items
        var req = Substitute.For<HttpRequestData>(context);
        req.Body.Returns(Stream.Null);
        req.CreateResponse().Returns(_ =>
        {
            var r = Substitute.For<HttpResponseData>(context);
            r.Body.Returns(new MemoryStream());
            r.Headers.Returns(new HttpHeadersCollection());
            HttpStatusCode captured = HttpStatusCode.OK;
            r.When(x => x.StatusCode = Arg.Any<HttpStatusCode>()).Do(ci => captured = ci.Arg<HttpStatusCode>());
            r.StatusCode.Returns(_ => captured);
            return r;
        });

        var result = await sut.Get(req, "patient-001", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WrongTherapist_Returns403()
    {
        var sut = new ClientDemographicsGet(_repository, AuthEnabled, NullLoggerFactory.Instance, new NullAuditLogger());
        var req = BuildAuthRequest("wrong@example.com");

        var result = await sut.Get(req, OwnedClientId, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_CorrectTherapist_Returns200()
    {
        var sut = new ClientDemographicsGet(_repository, AuthEnabled, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetAsync(OwnedClientId, Arg.Any<CancellationToken>())
            .Returns(SampleRecord(OwnedClientId));

        var req = BuildAuthRequest(OwnerEmail);
        var result = await sut.Get(req, OwnedClientId, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_RepositoryThrows_Returns500()
    {
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClientDemographicsResponse?>(_ => throw new InvalidOperationException("Cosmos unavailable"));

        var result = await _sut.Get(BuildRequest(), "patient-001", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
