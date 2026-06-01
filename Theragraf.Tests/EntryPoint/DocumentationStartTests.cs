using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Theragraf.Core.Models;
using Theragraf.Functions.EntryPoint;

namespace Theragraf.Tests.EntryPoint;

public class DocumentationStartTests
{
    private readonly DurableTaskClient _durableClient;
    private readonly TestableDocumentationStart _sut;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public DocumentationStartTests()
    {
        _durableClient = Substitute.For<DurableTaskClient>("test");
        _sut = new TestableDocumentationStart(NullLoggerFactory.Instance);
    }

    // Subclass that bypasses the static extension method for unit testing
    private sealed class TestableDocumentationStart(ILoggerFactory lf)
        : DocumentationStart(lf)
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
}
