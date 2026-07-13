using System.Net;
using System.Security.Claims;
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
using Theragraf.Functions.Logging;
using Theragraf.Functions.Middleware;

using System.Text.Json.Serialization;

namespace Theragraf.Tests.EntryPoint;

public class TherapistProfileUpdateTests
{
    private readonly ITherapistProfileRepository _repository;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string TherapistOid   = "oid-bob-002";
    private const string TenantId       = "tenant-contoso";
    private const string TherapistEmail = "bob@example.com";

    private static readonly IConfiguration AuthDisabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disabled"]       = "true",
                ["Auth:DevTherapistId"] = TherapistOid,
            })
            .Build();

    private static readonly IConfiguration AuthEnabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public TherapistProfileUpdateTests()
    {
        _repository = Substitute.For<ITherapistProfileRepository>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildAuthRequest(string? jsonBody = null)
    {
        var items = new Dictionary<object, object>
        {
            ["ClaimsPrincipal"] = new ClaimsPrincipal(
                new ClaimsIdentity(new[]
                {
                    new Claim("oid", TherapistOid),
                    new Claim("preferred_username", TherapistEmail),
                }, "test")),
            [TenantResolutionMiddleware.TenantContextKey] = new TenantDocument
            {
                Id = TenantId, TenantId = TenantId, OrganizationName = "Contoso PT"
            },
        };

        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(items);

        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(new MemoryStream(bodyBytes));
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

    private static HttpRequestData BuildUnauthRequest()
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(new Dictionary<object, object>());
        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(new MemoryStream("{}".Select(c => (byte)c).ToArray()));
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

    private TherapistProfileUpdate BuildSut(IConfiguration? config = null)
        => new(_repository, config ?? AuthDisabled, NullLoggerFactory.Instance, new NullAuditLogger());

    private static TherapistProfileDocument ExistingProfile() => new()
    {
        Id          = TherapistOid,
        TherapistId = TherapistOid,
        TenantId    = TenantId,
        FirstName   = "Bob",
        LastName    = "Jones",
        Credentials = "PT, DPT",
        Discipline  = TherapyDiscipline.PhysicalTherapy,
        CreatedAt   = DateTimeOffset.UtcNow.AddMonths(-1),
        UpdatedAt   = DateTimeOffset.UtcNow.AddMonths(-1),
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_NoClaimsPrincipal_Returns401()
    {
        var sut = BuildSut(AuthEnabled);
        var result = await sut.Run(BuildUnauthRequest(), CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_InvalidJsonBody_Returns400()
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.Items.Returns(new Dictionary<object, object>
        {
            ["ClaimsPrincipal"] = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("oid", TherapistOid) }, "test")),
            [TenantResolutionMiddleware.TenantContextKey] = new TenantDocument { Id = TenantId, TenantId = TenantId },
        });

        var request = Substitute.For<HttpRequestData>(context);
        request.Body.Returns(new MemoryStream(Encoding.UTF8.GetBytes("not valid json")));
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

        var result = await BuildSut().Run(request, CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("123")]         // too short
    [InlineData("12345678901")] // too long
    [InlineData("123456789A")]  // contains letter
    public async Task Update_InvalidNpi_Returns400(string badNpi)
    {
        var body = JsonSerializer.Serialize(new { individualNpi = badNpi });
        var result = await BuildSut().Run(BuildAuthRequest(body), CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_NoExistingProfile_CreatesNewProfile()
    {
        _repository.GetAsync(TenantId, TherapistOid, Arg.Any<CancellationToken>())
            .Returns((TherapistProfileDocument?)null);

        _repository.UpsertAsync(Arg.Any<TherapistProfileDocument>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TherapistProfileDocument>());

        var body = JsonSerializer.Serialize(new { firstName = "Bob", lastName = "Jones", credentials = "PT, DPT" });
        var result = await BuildSut().Run(BuildAuthRequest(body), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the document upserted has the correct identity fields.
        await _repository.Received(1).UpsertAsync(
            Arg.Is<TherapistProfileDocument>(d =>
                d.TherapistId == TherapistOid &&
                d.TenantId    == TenantId &&
                d.FirstName   == "Bob" &&
                d.LastName    == "Jones"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ExistingProfile_AppliesPartialUpdate()
    {
        var existing = ExistingProfile();
        _repository.GetAsync(TenantId, TherapistOid, Arg.Any<CancellationToken>())
            .Returns(existing);
        _repository.UpsertAsync(Arg.Any<TherapistProfileDocument>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TherapistProfileDocument>());

        // Only update credentials — other fields should remain unchanged.
        var body = JsonSerializer.Serialize(new { credentials = "PT, DPT, OCS" });
        var result = await BuildSut().Run(BuildAuthRequest(body), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);

        await _repository.Received(1).UpsertAsync(
            Arg.Is<TherapistProfileDocument>(d =>
                d.Credentials == "PT, DPT, OCS" &&
                d.FirstName   == "Bob" &&      // unchanged
                d.LastName    == "Jones"),      // unchanged
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ValidNpi_Accepted()
    {
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TherapistProfileDocument?)null);
        _repository.UpsertAsync(Arg.Any<TherapistProfileDocument>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TherapistProfileDocument>());

        var body = JsonSerializer.Serialize(new { individualNpi = "1234567890" });
        var result = await BuildSut().Run(BuildAuthRequest(body), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_RepositoryThrowsOnRead_Returns500WithSanitizedBody()
    {
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TherapistProfileDocument?>(_ => throw new InvalidOperationException("Cosmos unavailable"));

        var result = await BuildSut().Run(BuildAuthRequest(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.Headers.TryGetValues("X-Correlation-ID", out var ids).Should().BeTrue();

        result.Body.Position = 0;
        var body = await new StreamReader(result.Body).ReadToEndAsync();
        body.Should().NotContain("Cosmos unavailable");
        body.Should().Contain(ids!.First());
    }

    [Fact]
    public async Task Update_ResponseBodyContainsIsConfiguredTrue()
    {
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TherapistProfileDocument?)null);
        _repository.UpsertAsync(Arg.Any<TherapistProfileDocument>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TherapistProfileDocument>());

        var result = await BuildSut().Run(BuildAuthRequest(), CancellationToken.None);

        result.Body.Position = 0;
        var json = await new StreamReader(result.Body).ReadToEndAsync();
        var profile = JsonSerializer.Deserialize<TherapistProfileResponse>(json, JsonOptions);
        profile!.IsConfigured.Should().BeTrue();
    }
}
