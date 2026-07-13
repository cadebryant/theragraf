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
using Theragraf.Functions.EntryPoint;
using Theragraf.Functions.Logging;
using Theragraf.Functions.Middleware;

using System.Text.Json.Serialization;

namespace Theragraf.Tests.EntryPoint;

public class TenantGetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string TenantId = "tenant-acme";

    private static readonly IConfiguration AuthDisabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "true" })
            .Build();

    private static readonly IConfiguration AuthEnabled =
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:Disabled"] = "false" })
            .Build();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TenantDocument SampleTenant(int aiCalls = 12, int? quota = 500) => new()
    {
        Id                 = TenantId,
        TenantId           = TenantId,
        OrganizationName   = "Acme PT Group",
        OrganizationType   = TenantOrganizationType.GroupPractice,
        Plan               = TenantPlan.Professional,
        AiCallsThisPeriod  = aiCalls,
        MonthlyAiCallQuota = quota,
        Status             = TenantStatus.Active,
        CreatedAt          = DateTimeOffset.UtcNow,
        UpdatedAt          = DateTimeOffset.UtcNow,
    };

    private static HttpRequestData BuildRequest(
        bool includePrincipal  = true,
        TenantDocument? tenant = null)
    {
        var items = new Dictionary<object, object>();

        if (includePrincipal)
            items["ClaimsPrincipal"] = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim("preferred_username", "alice@acme.com") }, "test"));

        if (tenant is not null)
            items[TenantResolutionMiddleware.TenantContextKey] = tenant;

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

    private static TenantGet BuildSut(IConfiguration? config = null)
        => new(NullLoggerFactory.Instance, new NullAuditLogger(), config ?? AuthDisabled);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NoPrincipal_Returns401()
    {
        var sut = BuildSut(AuthEnabled);
        var result = await sut.Run(BuildRequest(includePrincipal: false, tenant: SampleTenant()), CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_NoTenantInContext_Returns404()
    {
        // Principal present but middleware never ran — no TenantDocument in Items.
        var result = await BuildSut().Run(BuildRequest(tenant: null), CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TenantPresent_Returns200()
    {
        var result = await BuildSut().Run(BuildRequest(tenant: SampleTenant()), CancellationToken.None);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_TenantPresent_ResponseContainsOrganizationName()
    {
        var result = await BuildSut().Run(BuildRequest(tenant: SampleTenant()), CancellationToken.None);

        result.Body.Position = 0;
        var json = await new StreamReader(result.Body).ReadToEndAsync();
        var summary = JsonSerializer.Deserialize<TenantSummaryResponse>(json, JsonOptions);

        summary.Should().NotBeNull();
        summary!.OrganizationName.Should().Be("Acme PT Group");
        summary.Plan.Should().Be(TenantPlan.Professional);
    }

    [Fact]
    public async Task Get_TenantPresent_ResponseContainsQuotaFields()
    {
        var result = await BuildSut().Run(BuildRequest(tenant: SampleTenant(aiCalls: 42, quota: 500)), CancellationToken.None);

        result.Body.Position = 0;
        var json = await new StreamReader(result.Body).ReadToEndAsync();
        var summary = JsonSerializer.Deserialize<TenantSummaryResponse>(json, JsonOptions);

        summary!.AiCallsThisPeriod.Should().Be(42);
        summary.MonthlyAiCallQuota.Should().Be(500);
    }

    [Fact]
    public async Task Get_SyntheticTenant_IsSyntheticTrue()
    {
        var syntheticTenant = SampleTenant();
        syntheticTenant.IsSynthetic = true;

        var result = await BuildSut().Run(BuildRequest(tenant: syntheticTenant), CancellationToken.None);

        result.Body.Position = 0;
        var json = await new StreamReader(result.Body).ReadToEndAsync();
        var summary = JsonSerializer.Deserialize<TenantSummaryResponse>(json, JsonOptions);

        summary!.IsSynthetic.Should().BeTrue();
    }

    [Fact]
    public async Task Get_UnlimitedQuota_MonthlyAiCallQuotaIsNull()
    {
        var result = await BuildSut().Run(BuildRequest(tenant: SampleTenant(quota: null)), CancellationToken.None);

        result.Body.Position = 0;
        var json = await new StreamReader(result.Body).ReadToEndAsync();
        var summary = JsonSerializer.Deserialize<TenantSummaryResponse>(json, JsonOptions);

        summary!.MonthlyAiCallQuota.Should().BeNull();
    }
}
