using System.Net;
using System.Security.Claims;
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

public class TherapistProfileGetTests
{
    private readonly ITherapistProfileRepository _repository;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string TherapistOid  = "oid-alice-001";
    private const string TenantId      = "tenant-contoso";
    private const string TherapistEmail = "alice@example.com";

    // Auth:Disabled=true so unit tests don't need a real JWT.
    private static readonly IConfiguration AuthDisabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disabled"]      = "true",
                ["Auth:DevTherapistId"] = TherapistOid,
            })
            .Build();

    private static readonly IConfiguration AuthEnabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public TherapistProfileGetTests()
    {
        _repository = Substitute.For<ITherapistProfileRepository>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a request with a real ClaimsPrincipal and a resolved TenantDocument in Items.
    /// </summary>
    private static HttpRequestData BuildAuthRequest(
        string? oid         = TherapistOid,
        string? email       = TherapistEmail,
        string? tenantId    = TenantId)
    {
        var items = new Dictionary<object, object>();

        if (oid is not null || email is not null)
        {
            var claims = new List<Claim>();
            if (oid   is not null) claims.Add(new Claim("oid", oid));
            if (email is not null) claims.Add(new Claim("preferred_username", email));
            items["ClaimsPrincipal"] = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "test"));
        }

        if (tenantId is not null)
        {
            items[TenantResolutionMiddleware.TenantContextKey] = new TenantDocument
            {
                Id = tenantId, TenantId = tenantId, OrganizationName = "Contoso PT"
            };
        }

        return BuildRequestWithItems(items);
    }

    /// <summary>Builds a request with no ClaimsPrincipal — simulates an unauthenticated call.</summary>
    private static HttpRequestData BuildUnauthRequest()
        => BuildRequestWithItems(new Dictionary<object, object>());

    private static HttpRequestData BuildRequestWithItems(IDictionary<object, object> items)
    {
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

    private TherapistProfileGet BuildSut(IConfiguration? config = null)
        => new(_repository, config ?? AuthDisabled, NullLoggerFactory.Instance, new NullAuditLogger());

    private static TherapistProfileDocument SampleProfile() => new()
    {
        Id          = TherapistOid,
        TherapistId = TherapistOid,
        TenantId    = TenantId,
        FirstName   = "Alice",
        LastName    = "Smith",
        Credentials = "OTR/L",
        Discipline  = TherapyDiscipline.OccupationalTherapy,
        IndividualNpi = "1234567890",
        CreatedAt   = DateTimeOffset.UtcNow,
        UpdatedAt   = DateTimeOffset.UtcNow,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoClaimsPrincipal_Returns401()
    {
        var sut = BuildSut(AuthEnabled);
        var result = await sut.Run(BuildUnauthRequest(), CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_NoTenantInContext_Returns400()
    {
        // Auth:Disabled means IsAuthenticated returns true, but GetTenantId will be null
        // because no TenantDocument is in Items.
        var items = new Dictionary<object, object>
        {
            ["ClaimsPrincipal"] = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("oid", TherapistOid) }, "test"))
        };
        var req = BuildRequestWithItems(items);
        var result = await BuildSut().Run(req, CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ProfileDoesNotExist_Returns404()
    {
        _repository.GetAsync(TenantId, TherapistOid, Arg.Any<CancellationToken>())
            .Returns((TherapistProfileDocument?)null);

        var result = await BuildSut().Run(BuildAuthRequest(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ProfileExists_Returns200WithCorrectFields()
    {
        _repository.GetAsync(TenantId, TherapistOid, Arg.Any<CancellationToken>())
            .Returns(SampleProfile());

        var result = await BuildSut().Run(BuildAuthRequest(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);

        result.Body.Position = 0;
        var body = await new StreamReader(result.Body).ReadToEndAsync();
        var profile = JsonSerializer.Deserialize<TherapistProfileResponse>(body, JsonOptions);

        profile.Should().NotBeNull();
        profile!.TherapistId.Should().Be(TherapistOid);
        profile.TenantId.Should().Be(TenantId);
        profile.FirstName.Should().Be("Alice");
        profile.LastName.Should().Be("Smith");
        profile.Credentials.Should().Be("OTR/L");
        profile.IndividualNpi.Should().Be("1234567890");
        profile.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Get_RepositoryThrows_Returns500WithSanitizedBody()
    {
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TherapistProfileDocument?>(_ => throw new InvalidOperationException("Cosmos down"));

        var result = await BuildSut().Run(BuildAuthRequest(), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.Headers.TryGetValues("X-Correlation-ID", out var ids).Should().BeTrue();

        result.Body.Position = 0;
        var body = await new StreamReader(result.Body).ReadToEndAsync();
        body.Should().NotContain("Cosmos down");
        body.Should().Contain(ids!.First());
    }
}
