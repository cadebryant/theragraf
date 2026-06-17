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
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class SessionsDeleteTests
{
    private readonly ISessionRepository _repository;
    private readonly SessionsDelete _sut;

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    public SessionsDeleteTests()
    {
        _repository = Substitute.For<ISessionRepository>();
        _sut = new SessionsDelete(_repository, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());
    }

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
            HttpStatusCode capturedStatus = HttpStatusCode.OK;
            response.When(r => r.StatusCode = Arg.Any<HttpStatusCode>())
                    .Do(ci => capturedStatus = ci.Arg<HttpStatusCode>());
            response.StatusCode.Returns(_ => capturedStatus);
            return response;
        });
        return request;
    }

    [Fact]
    public async Task Delete_ExistingSession_Returns204()
    {
        _repository.DeleteAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
                   .Returns(true);

        var response = await _sut.Delete(BuildRequest(), "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExistingSession_CallsRepositoryWithCorrectKeys()
    {
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(true);

        await _sut.Delete(BuildRequest(), "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        await _repository.Received(1).DeleteAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_SessionNotFound_Returns404()
    {
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns(false);

        var response = await _sut.Delete(BuildRequest(), "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_InvalidDateFormat_Returns400()
    {
        var response = await _sut.Delete(BuildRequest(), "client-001", "2024-10-10", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_MissingClientId_Returns400()
    {
        var response = await _sut.Delete(BuildRequest(), "", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_RepositoryThrows_Returns500()
    {
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                   .Returns<bool>(_ => throw new Exception("Storage failure"));

        var response = await _sut.Delete(BuildRequest(), "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // -- Ownership tests ------------------------------------------------------

    private static readonly IConfiguration AuthEnabledConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    private static HttpRequestData BuildAuthenticatedRequest(string therapistName)
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

    private static Core.Models.SessionResponse BuildSession(string therapistName) =>
        new("client-001", "2024-10-10T10-00-00Z", therapistName, "PT", "Soap", "Outpatient", "Medicare", 45,
            new Core.Models.SoapNote("S", "O", "A", "P"),
            new List<Core.Models.CptCode>(), new List<Core.Models.IcdCode>(),
            DateTimeOffset.UtcNow, false, null, null);

    [Fact]
    public async Task Delete_WrongTherapist_Returns403()
    {
        var sut = new SessionsDelete(_repository, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(BuildSession("Dr. Adams"));

        var req = BuildAuthenticatedRequest("Dr. Other");
        var response = await sut.Delete(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_CorrectTherapist_Returns204()
    {
        var sut = new SessionsDelete(_repository, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());
        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(BuildSession("Dr. Adams"));
        _repository.DeleteAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>()).Returns(true);

        var req = BuildAuthenticatedRequest("Dr. Adams");
        var response = await sut.Delete(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
