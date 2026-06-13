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
using NSubstitute.ExceptionExtensions;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Logging;

namespace Theragraf.Tests.EntryPoint;

public class SessionsUpdateTests
{
    private readonly ISessionRepository    _repository;
    private readonly IPiiRedactionService  _redaction;
    private readonly SessionsUpdate        _sut;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IConfiguration DisabledAuthConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    public SessionsUpdateTests()
    {
        _repository = Substitute.For<ISessionRepository>();
        _redaction  = Substitute.For<IPiiRedactionService>();
        _sut        = new SessionsUpdate(_repository, _redaction, DisabledAuthConfig, NullLoggerFactory.Instance, new NullAuditLogger());

        // Default: redaction is a pass-through with an empty map
        _redaction
            .RedactAsync(Arg.Any<string>())
            .Returns(ci => (ci.Arg<string>(), (IReadOnlyDictionary<string, string>)new Dictionary<string, string>()));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildRequest(object? body = null)
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());

        var request = Substitute.For<HttpRequestData>(context);
        var bodyStream = body is null
            ? new MemoryStream()
            : new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, JsonOptions)));
        request.Body.Returns(bodyStream);

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

    private static SessionResponse BuildSessionResponse(string therapistName = "Dr. Smith") => new(
        ClientId:               "client-001",
        SessionDate:            "2024-10-10T10-00-00Z",
        TherapistName:          therapistName,
        Discipline:             "PT",
        Setting:                "Outpatient",
        Payer:                  "Medicare",
        SessionDurationMinutes: 45,
        SoapNote:               new SoapNote("S", "O", "A", "P"),
        SuggestedCptCodes:      [],
        SuggestedIcdCodes:      [],
        CreatedAt:              DateTimeOffset.UtcNow
    );

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_MissingClientId_Returns400()
    {
        var response = await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "New text" } }),
            "", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_InvalidDateFormat_Returns400()
    {
        var response = await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "New text" } }),
            "client-001", "2024-10-10", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_MalformedJson_Returns400()
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(new MemoryStream(Encoding.UTF8.GetBytes("{ not valid json")));
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

        var result = await _sut.Update(request, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── 404 ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_SessionNotFound_Returns404()
    {
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns((SessionResponse?)null);

        var response = await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "Updated." } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_Returns200WithBody()
    {
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse());

        var response = await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "Patient improved." } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_SoapNoteUpdate_CallsRedactionService()
    {
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse());

        await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "Patient improved." } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        await _redaction.Received(1).RedactAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Update_CodesOnlyUpdate_DoesNotCallRedactionService()
    {
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse());

        var cptCodes = new[] { new CptCode("97110", "Therapeutic exercise", "Strengthening", 2) };
        await _sut.Update(
            BuildRequest(new { suggestedCptCodes = cptCodes }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        await _redaction.DidNotReceive().RedactAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Update_SoapNoteUpdate_PassesRedactedNoteToRepository()
    {
        _redaction
            .RedactAsync(Arg.Any<string>())
            .Returns(("REDACTED\x1FTEXT\x1FHERE\x1FPLAN",
                (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["[NAME_1]"] = "John" }));

        SoapNoteUpdate? capturedNote = null;
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<SoapNoteUpdate?>(n => capturedNote = n),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse());

        await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "John improved.", objective = "Text", assessment = "Here", plan = "Plan" } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        capturedNote.Should().NotBeNull();
        capturedNote!.Subjective.Should().Be("REDACTED");
        capturedNote.Objective.Should().Be("TEXT");
        capturedNote.Assessment.Should().Be("HERE");
        capturedNote.Plan.Should().Be("PLAN");
    }

    [Fact]
    public async Task Update_PartialSoapNoteUpdate_OnlyRedactsProvidedFields()
    {
        // Redaction echoes back whatever it receives (pass-through default set in constructor).
        SoapNoteUpdate? capturedNote = null;
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<SoapNoteUpdate?>(n => capturedNote = n),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse());

        // Only subjective and plan are provided; objective and assessment are omitted.
        await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "Updated subjective.", plan = "Updated plan." } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        capturedNote.Should().NotBeNull();
        capturedNote!.Subjective.Should().Be("Updated subjective.");
        capturedNote.Plan.Should().Be("Updated plan.");
        // Omitted fields must be null so the repository can preserve the stored values.
        capturedNote.Objective.Should().BeNull();
        capturedNote.Assessment.Should().BeNull();
    }

    [Fact]
    public async Task Update_RepositoryThrows_Returns500()
    {
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Cosmos unavailable"));

        var response = await _sut.Update(
            BuildRequest(new { soapNote = new { subjective = "Updated." } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Update_CallsRepositoryWithCorrectClientIdAndDate()
    {
        _repository
            .UpdateAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse());

        await _sut.Update(
            BuildRequest(new { soapNote = new { plan = "Continue PT." } }),
            "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        await _repository.Received(1).UpdateAsync(
            "client-001", "2024-10-10T10-00-00Z",
            Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
            Arg.Any<CancellationToken>());
    }

    // -- Ownership tests ------------------------------------------------------

    private static readonly IConfiguration AuthEnabledConfig =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    private static HttpRequestData BuildAuthenticatedRequest(string therapistName, object? body = null)
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
        var bodyStream = body is null
            ? new MemoryStream()
            : new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, JsonOptions)));
        request.Body.Returns(bodyStream);
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
    public async Task Update_WrongTherapist_Returns403()
    {
        var sut = new SessionsUpdate(_repository, _redaction, AuthEnabledConfig, NullLoggerFactory.Instance, new NullAuditLogger());

        _repository.GetByClientIdAndDateAsync("client-001", "2024-10-10T10-00-00Z", Arg.Any<CancellationToken>())
            .Returns(BuildSessionResponse(therapistName: "Dr. Adams"));

        var req = BuildAuthenticatedRequest("Dr. Other", new { suggestedCptCodes = new[] { new { code = "97110", description = "Therapeutic exercise", rationale = "r" } } });
        var response = await sut.Update(req, "client-001", "2024-10-10T10-00-00Z", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<SoapNoteUpdate?>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<IReadOnlyList<CptCode>?>(), Arg.Any<IReadOnlyList<IcdCode>?>(),
            Arg.Any<CancellationToken>());
    }
}
