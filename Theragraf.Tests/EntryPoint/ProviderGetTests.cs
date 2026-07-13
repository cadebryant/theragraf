using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
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

namespace Theragraf.Tests.EntryPoint;

public class ProviderGetTests
{
    private readonly IProviderRepository _repository;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string TenantId   = "tenant-clinic";
    private const string ProviderId = "provider-abc-001";

    private static readonly IConfiguration AuthDisabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration AuthEnabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    public ProviderGetTests()
    {
        _repository = Substitute.For<IProviderRepository>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestData BuildRequest(
        bool includePrincipal = true,
        string? tenantId      = TenantId)
    {
        var items = new Dictionary<object, object>();

        if (includePrincipal)
            items["ClaimsPrincipal"] = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("preferred_username", "carol@clinic.com") }, "test"));

        if (tenantId is not null)
            items[TenantResolutionMiddleware.TenantContextKey] = new TenantDocument
            {
                Id = tenantId, TenantId = tenantId, OrganizationName = "Clinic"
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

    private ProviderGet BuildSut(IConfiguration? config = null)
        => new(_repository, config ?? AuthDisabled, NullLoggerFactory.Instance, new NullAuditLogger());

    private static ProviderDocument SampleProvider() => new()
    {
        Id              = ProviderId,
        ProviderId      = ProviderId,
        TenantId        = TenantId,
        PracticeName    = "Sunrise Physical Therapy",
        OrganizationNpi = "9876543210",
        AddressLine1    = "100 Main St",
        City            = "Springfield",
        State           = "IL",
        Zip             = "62701",
        Phone           = "2175551234",
        CreatedAt       = DateTimeOffset.UtcNow,
        UpdatedAt       = DateTimeOffset.UtcNow,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoPrincipal_Returns401()
    {
        var sut = BuildSut(AuthEnabled);
        var result = await sut.Run(BuildRequest(includePrincipal: false), ProviderId, CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_NoTenantInContext_Returns400()
    {
        // Tenant resolution failed — no TenantDocument in context.
        var result = await BuildSut().Run(BuildRequest(tenantId: null), ProviderId, CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ProviderNotFound_Returns404()
    {
        _repository.GetAsync(TenantId, ProviderId, Arg.Any<CancellationToken>())
            .Returns((ProviderDocument?)null);

        var result = await BuildSut().Run(BuildRequest(), ProviderId, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ProviderFound_Returns200WithFields()
    {
        _repository.GetAsync(TenantId, ProviderId, Arg.Any<CancellationToken>())
            .Returns(SampleProvider());

        var result = await BuildSut().Run(BuildRequest(), ProviderId, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);

        result.Body.Position = 0;
        var json = await new StreamReader(result.Body).ReadToEndAsync();
        var provider = JsonSerializer.Deserialize<ProviderResponse>(json, JsonOptions);

        provider.Should().NotBeNull();
        provider!.ProviderId.Should().Be(ProviderId);
        provider.TenantId.Should().Be(TenantId);
        provider.PracticeName.Should().Be("Sunrise Physical Therapy");
        provider.OrganizationNpi.Should().Be("9876543210");
        provider.City.Should().Be("Springfield");
    }

    [Fact]
    public async Task Get_ProviderFound_EncryptedEinNotExposed()
    {
        // EncryptedEin is a server-only field and must not appear in the ProviderResponse DTO.
        // Verifying that the DTO class itself has no such property is sufficient.
        var responseType = typeof(ProviderResponse);
        responseType.GetProperty("EncryptedEin").Should().BeNull(
            "encryptedEin is a sensitive billing field and must not be returned in the API response");
    }

    [Fact]
    public async Task Get_RepositoryThrows_Returns500WithSanitizedBody()
    {
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProviderDocument?>(_ => throw new InvalidOperationException("Cosmos partition error"));

        var result = await BuildSut().Run(BuildRequest(), ProviderId, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.Headers.TryGetValues("X-Correlation-ID", out var ids).Should().BeTrue();

        result.Body.Position = 0;
        var body = await new StreamReader(result.Body).ReadToEndAsync();
        body.Should().NotContain("Cosmos partition error");
        body.Should().Contain(ids!.First());
    }

    [Fact]
    public async Task Get_AlwaysPassesTenantIdToRepository()
    {
        // Ensures the repository is called with the tenant from context, not a URL param —
        // preventing a hypothetical cross-tenant lookup if the caller supplied a foreign tenantId.
        _repository.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ProviderDocument?)null);

        await BuildSut().Run(BuildRequest(tenantId: TenantId), ProviderId, CancellationToken.None);

        await _repository.Received(1).GetAsync(
            TenantId,   // must come from the resolved tenant context, not user input
            ProviderId,
            Arg.Any<CancellationToken>());
    }
}
