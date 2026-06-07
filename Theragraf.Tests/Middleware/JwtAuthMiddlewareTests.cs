using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Theragraf.Functions.Middleware;

namespace Theragraf.Tests.Middleware;

public class JwtAuthMiddlewareTests
{
    private const string TenantId  = "test-tenant-id";
    private const string ClientId  = "test-client-id";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static JwtAuthMiddleware BuildSut(bool authDisabled = false)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Disabled"]    = authDisabled ? "true" : "false",
                ["AzureAd:TenantId"] = TenantId,
                ["AzureAd:ClientId"] = ClientId,
            })
            .Build();

        return new JwtAuthMiddleware(config, NullLoggerFactory.Instance);
    }

    private static (FunctionContext context, HttpRequestData request, IDictionary<object, object> items) BuildHttpContext(string? authHeader = null)
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());

        // Provide a real dictionary so indexer writes inside the middleware persist.
        var items = new Dictionary<object, object>();
        context.Items.Returns(items);

        var request = Substitute.For<HttpRequestData>(context);
        var headers = new HttpHeadersCollection();
        if (authHeader is not null)
            headers.Add("Authorization", authHeader);
        request.Headers.Returns(headers);
        request.Body.Returns(new MemoryStream());

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

        context.GetHttpRequestDataAsync().Returns(request);

        // Wire invocation result so WriteUnauthorized can set it
        var invocationResult = Substitute.For<InvocationResult>();
        context.GetInvocationResult().Returns(invocationResult);

        return (context, request, items);
    }

    private static FunctionContext BuildNonHttpContext()
    {
        var context = Substitute.For<FunctionContext>();
        context.InstanceServices.Returns(new ServiceCollection().BuildServiceProvider());
        context.GetHttpRequestDataAsync().Returns((HttpRequestData?)null);
        return context;
    }

    private static string BuildValidToken(RsaSecurityKey key)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity([new Claim("sub", "user-123")]),
            Issuer             = $"https://sts.windows.net/{TenantId}/",
            Audience           = $"api://{ClientId}",
            Expires            = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invoke_NonHttpTrigger_CallsNextWithoutAuth()
    {
        var sut     = BuildSut();
        var context = BuildNonHttpContext();
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await sut.Invoke(context, next);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_AuthDisabled_CallsNextWithoutValidatingToken()
    {
        var sut = BuildSut(authDisabled: true);
        var (context, _, _) = BuildHttpContext(); // no Authorization header
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await sut.Invoke(context, next);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Invoke_MissingAuthorizationHeader_Returns401()
    {
        var sut = BuildSut();
        var (context, _, _) = BuildHttpContext(authHeader: null);
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await sut.Invoke(context, next);

        var result = context.GetInvocationResult().Value as HttpResponseData;
        result!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invoke_NonBearerAuthorizationHeader_Returns401()
    {
        var sut = BuildSut();
        var (context, _, _) = BuildHttpContext(authHeader: "Basic dXNlcjpwYXNz");
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await sut.Invoke(context, next);

        var result = context.GetInvocationResult().Value as HttpResponseData;
        result!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invoke_InvalidToken_Returns401()
    {
        // Use testable subclass to avoid live OIDC discovery; the malformed JWT fails validation.
        using var rsa = RSA.Create(2048);
        var sut = new TestableJwtAuthMiddleware(new RsaSecurityKey(rsa));
        var (context, _, _) = BuildHttpContext(authHeader: "Bearer not.a.valid.jwt");
        FunctionExecutionDelegate next = _ => Task.CompletedTask;

        await sut.Invoke(context, next);

        var result = context.GetInvocationResult().Value as HttpResponseData;
        result!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invoke_ValidToken_CallsNextAndStoresClaimsPrincipal()
    {
        // Build a self-signed RSA key and inject it so no OIDC network call is needed.
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa);
        var token = BuildValidToken(key);

        // Subclass to inject the key directly, bypassing OIDC discovery.
        var sut = new TestableJwtAuthMiddleware(key);
        var (context, _, items) = BuildHttpContext(authHeader: $"Bearer {token}");
        var nextCalled = false;
        FunctionExecutionDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        await sut.Invoke(context, next);

        nextCalled.Should().BeTrue();
        items.Should().ContainKey("ClaimsPrincipal");
    }

    // ── Testable subclass — injects a known signing key bypassing OIDC ────────

    private sealed class TestableJwtAuthMiddleware : JwtAuthMiddleware
    {
        private readonly RsaSecurityKey _key;

        public TestableJwtAuthMiddleware(RsaSecurityKey key)
            : base(
                new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Auth:Disabled"]    = "false",
                        ["AzureAd:TenantId"] = TenantId,
                        ["AzureAd:ClientId"] = ClientId,
                    })
                    .Build(),
                NullLoggerFactory.Instance)
        {
            _key = key;
        }

        protected override Task<OpenIdConnectConfiguration> GetOidcConfigAsync(string tenantId)
        {
            var config = new OpenIdConnectConfiguration();
            config.SigningKeys.Add(_key);
            return Task.FromResult(config);
        }
    }
}
