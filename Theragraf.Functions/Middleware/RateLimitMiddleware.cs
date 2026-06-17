namespace Theragraf.Functions.Middleware;

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Theragraf.Functions.Configuration;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Models;
using Theragraf.Functions.Services;

using HttpRequestData = Microsoft.Azure.Functions.Worker.Http.HttpRequestData;
using HttpResponseData = Microsoft.Azure.Functions.Worker.Http.HttpResponseData;

/// <summary>
/// Middleware that enforces rate limits on HTTP-triggered functions.
/// Rate limits are keyed by authenticated user identity and endpoint name.
/// </summary>
public sealed class RateLimitMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IRateLimitService _rateLimitService;
    private readonly RateLimitConfiguration _config;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(
        IRateLimitService rateLimitService,
        RateLimitConfiguration config,
        ILogger<RateLimitMiddleware> logger)
    {
        _rateLimitService = rateLimitService;
        _config = config;
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // Only apply rate limiting to HTTP-triggered functions.
        var httpRequestData = await context.GetHttpRequestDataAsync();
        if (httpRequestData is null || !_config.Enabled)
        {
            await next(context);
            return;
        }

        // Extract the authenticated user identity from claims.
        var items = context.Items.ToDictionary(
            kvp => kvp.Key.ToString()!,
            kvp => kvp.Value);
        var identity = ClaimsHelper.GetIdentity(items);

        // Skip rate limiting for bypassed users (e.g., admin, test accounts).
        var bypassUsers = _config.GetBypassUserIds();
        if (bypassUsers.Contains(identity ?? string.Empty))
        {
            _logger.LogDebug("User {UserId} is bypassed from rate limiting", identity);
            await next(context);
            return;
        }

        // Use "anonymous" if no identity is found (unauthenticated requests).
        var userId = identity ?? "anonymous";

        // Extract the endpoint name from the function name.
        var endpointName = context.FunctionDefinition.Name;

        // Determine the policy based on HTTP method and endpoint characteristics.
        var method = httpRequestData.Method;
        var policy = DeterminePolicyForEndpoint(method, endpointName);

        // Check rate limit.
        var key = new RateLimitKey(userId, endpointName);
        var result = await _rateLimitService.CheckRateLimitAsync(key, policy, context.CancellationToken);

        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for user {UserId} on endpoint {Endpoint}: {Current}/{Max}",
                userId, endpointName, result.CurrentCount, result.Limit);

            var response = httpRequestData.CreateResponse(HttpStatusCode.TooManyRequests);
            response.Headers.Add("Retry-After", ((int)result.TimeUntilReset.TotalSeconds).ToString());
            response.Headers.Add("X-RateLimit-Limit", result.Limit.ToString());
            response.Headers.Add("X-RateLimit-Remaining", Math.Max(0, result.Limit - result.CurrentCount).ToString());
            response.Headers.Add("X-RateLimit-Reset", result.WindowResetTime.ToString("O"));

            await response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                limit = result.Limit,
                current = result.CurrentCount,
                resetTime = result.WindowResetTime,
                retryAfterSeconds = (int)result.TimeUntilReset.TotalSeconds,
            });

            context.GetInvocationResult().Value = response;
            return;
        }

        // Allow the request to proceed.
        await next(context);
    }

    private static RateLimitPolicy DeterminePolicyForEndpoint(string method, string functionName)
    {
        // POST/PUT/DELETE operations are more resource-intensive.
        if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            // Speech token is especially rate-limited (quota-sensitive service).
            if (functionName.Contains("SpeechToken", StringComparison.OrdinalIgnoreCase))
                return RateLimitPolicy.Presets.SpeechToken;

            // Documentation start/processing is heavy (triggers AI agents and Durable Functions).
            if (functionName.Contains("DocumentationStart", StringComparison.OrdinalIgnoreCase) ||
                functionName.Contains("Status", StringComparison.OrdinalIgnoreCase))
                return RateLimitPolicy.Presets.DocumentationPipeline;

            // All other mutations.
            return RateLimitPolicy.Presets.Mutation;
        }

        // GET operations are less resource-intensive.
        return RateLimitPolicy.Presets.ReadOnly;
    }
}
