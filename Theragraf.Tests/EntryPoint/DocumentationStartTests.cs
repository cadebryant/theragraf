using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class DocumentationStartTests
{
    private readonly DurableTaskClient _durableClient;
    private readonly TestableDocumentationStart _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    public DocumentationStartTests()
    {
        _durableClient = Substitute.For<DurableTaskClient>("test");
        _sut = new TestableDocumentationStart(NullLoggerFactory.Instance, DisabledAuthConfig, new NullAuditLogger());
    }

    // Subclass that bypasses the static extension method for unit testing
    private sealed class TestableDocumentationStart(ILoggerFactory lf, IConfiguration cfg, IAuditLogger auditLogger)
        : DocumentationStart(lf, cfg, auditLogger)
    {
        protected override HttpManagementPayload GetManagementPayload(string instanceId, HttpRequestData req, DurableTaskClient durableClient)
        {
            // HttpManagementPayload has no public constructor; build via reflection
            var type = typeof(HttpManagementPayload);
            var ctor = type.GetConstructors(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public)
                .OrderByDescending(c => c.GetParameters().Length)
                .First();
            var args = ctor.GetParameters()
                .Select(p => (object?)(p.ParameterType == typeof(string) ? "http://localhost/stub" : null))
                .ToArray();
            return (HttpManagementPayload)ctor.Invoke(args);
        }
    }

    private static TranscriptInput ValidInput() =>
        new("Patient discussed anxiety symptoms.", "Dr. Adams", "client-001", DateTimeOffset.UtcNow);

    private HttpRequestData BuildRequest(object? body)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(stream);
        request.CreateResponse().Returns(_ =>
        {
            var response = Substitute.For<HttpResponseData>(context);
            var responseBody = new MemoryStream();
            response.Body.Returns(responseBody);
            response.Headers.Returns(new HttpHeadersCollection());
            return response;
        });
        return request;
    }

    private HttpRequestData BuildMalformedRequest()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("not valid json {{"));
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(stream);
        request.CreateResponse().Returns(_ =>
        {
            var response = Substitute.For<HttpResponseData>(context);
            response.Body.Returns(new MemoryStream());
            response.Headers.Returns(new HttpHeadersCollection());
            return response;
        });
        return request;
    }

    [Fact]
    public async Task Run_ValidInput_Returns202Accepted()
    {
        _durableClient.ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("instance-123");

        var response = await _sut.Run(BuildRequest(ValidInput()), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Run_ValidInput_SchedulesOrchestratorWithInput()
    {
        _durableClient.ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("instance-123");

        await _sut.Run(BuildRequest(ValidInput()), _durableClient, CancellationToken.None);

        await _durableClient.Received(1).ScheduleNewOrchestrationInstanceAsync(
            Arg.Is<TaskName>(n => n.Name == "DocumentationOrchestrator"),
            Arg.Is<TranscriptInput>(t => t.ClientId == "client-001"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_MissingRawTranscript_Returns400()
    {
        var input = new { RawTranscript = "", TherapistName = "Dr. Adams", ClientId = "client-001", SessionDate = DateTimeOffset.UtcNow };

        var response = await _sut.Run(BuildRequest(input), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Run_MissingTherapistName_Returns400()
    {
        var input = new { RawTranscript = "transcript", TherapistName = "", ClientId = "client-001", SessionDate = DateTimeOffset.UtcNow };

        var response = await _sut.Run(BuildRequest(input), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Run_MissingClientId_Returns400()
    {
        var input = new { RawTranscript = "transcript", TherapistName = "Dr. Adams", ClientId = "", SessionDate = DateTimeOffset.UtcNow };

        var response = await _sut.Run(BuildRequest(input), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Run_DefaultSessionDate_Returns400()
    {
        var input = new { RawTranscript = "transcript", TherapistName = "Dr. Adams", ClientId = "client-001", SessionDate = default(DateTimeOffset) };

        var response = await _sut.Run(BuildRequest(input), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(481)]
    public async Task Run_InvalidSessionDurationMinutes_Returns400(int duration)
    {
        var input = new { RawTranscript = "transcript", TherapistName = "Dr. Adams", ClientId = "client-001", SessionDate = DateTimeOffset.UtcNow, SessionDurationMinutes = duration };

        var response = await _sut.Run(BuildRequest(input), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Run_MalformedJson_Returns400()
    {
        var response = await _sut.Run(BuildMalformedRequest(), _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Run_ValidInput_DoesNotScheduleOrchestratorOnValidationFailure()
    {
        var input = new { RawTranscript = "", TherapistName = "Dr. Adams", ClientId = "client-001", SessionDate = DateTimeOffset.UtcNow };

        await _sut.Run(BuildRequest(input), _durableClient, CancellationToken.None);

        await _durableClient.DidNotReceive().ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // -- Ownership tests ------------------------------------------------------

    private static readonly IConfiguration AuthEnabledConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    private HttpRequestData BuildAuthenticatedRequest(string therapistName, object body)
    {
        var items = new Dictionary<object, object>();
        items["ClaimsPrincipal"] = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("preferred_username", therapistName) },
                "test"));

        var json = JsonSerializer.Serialize(body, JsonOptions);
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(items);
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(stream);
        request.CreateResponse().Returns(_ =>
        {
            var response = Substitute.For<HttpResponseData>(context);
            var responseBody = new MemoryStream();
            response.Body.Returns(responseBody);
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
    public async Task Run_TherapistNameMismatch_Returns403()
    {
        var sut = new TestableDocumentationStart(NullLoggerFactory.Instance, AuthEnabledConfig, new NullAuditLogger());
        var input = new { RawTranscript = "Transcript text.", TherapistName = "Dr. Adams", ClientId = "client-001", SessionDate = DateTimeOffset.UtcNow };

        var req = BuildAuthenticatedRequest("Dr. Other", input); // JWT = Dr. Other, body = Dr. Adams
        var response = await sut.Run(req, _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _durableClient.DidNotReceive().ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_TherapistNameMatches_Returns202()
    {
        var sut = new TestableDocumentationStart(NullLoggerFactory.Instance, AuthEnabledConfig, new NullAuditLogger());
        var input = new { RawTranscript = "Transcript text.", TherapistName = "Dr. Adams", ClientId = "client-001", SessionDate = DateTimeOffset.UtcNow, SessionDurationMinutes = 45 };

        var req = BuildAuthenticatedRequest("Dr. Adams", input);
        var response = await sut.Run(req, _durableClient, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
