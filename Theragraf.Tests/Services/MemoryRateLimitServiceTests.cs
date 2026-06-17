namespace Theragraf.Tests.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Theragraf.Functions.Models;
using Theragraf.Functions.Services;

/// <summary>
/// Tests for the in-memory rate limit service.
/// </summary>
public sealed class MemoryRateLimitServiceTests
{
    private readonly MemoryRateLimitService _sut = new();

    [Fact]
    public async Task CheckRateLimitAsync_FirstRequest_AllowsAndReturnsCount1()
    {
        var key = new RateLimitKey("user1", "DocumentationStart");
        var policy = RateLimitPolicy.Presets.DocumentationPipeline;

        var result = await _sut.CheckRateLimitAsync(key, policy);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.CurrentCount);
        Assert.Equal(policy.MaxRequests, result.Limit);
    }

    [Fact]
    public async Task CheckRateLimitAsync_MultipleRequestsUnderLimit_AllowsAll()
    {
        var key = new RateLimitKey("user1", "DocumentationStart");
        var policy = new RateLimitPolicy("test", 3, TimeSpan.FromSeconds(60));

        for (int i = 1; i <= 3; i++)
        {
            var result = await _sut.CheckRateLimitAsync(key, policy);
            Assert.True(result.IsAllowed, $"Request {i} should be allowed");
            Assert.Equal(i, result.CurrentCount);
        }
    }

    [Fact]
    public async Task CheckRateLimitAsync_ExceedLimit_DeniesRequestAndDoesNotIncrement()
    {
        var key = new RateLimitKey("user1", "SpeechToken");
        var policy = new RateLimitPolicy("test", 2, TimeSpan.FromSeconds(60));

        // First two allowed.
        for (int i = 1; i <= 2; i++)
        {
            var result = await _sut.CheckRateLimitAsync(key, policy);
            Assert.True(result.IsAllowed, $"Request {i} should be allowed");
            Assert.Equal(i, result.CurrentCount);
        }

        // Third should be denied, count stays at 2.
        var deniedResult = await _sut.CheckRateLimitAsync(key, policy);
        Assert.False(deniedResult.IsAllowed);
        Assert.Equal(2, deniedResult.CurrentCount); // Count does not increment when denied

        // Fourth should also be denied and count should still be 2.
        var deniedResult2 = await _sut.CheckRateLimitAsync(key, policy);
        Assert.False(deniedResult2.IsAllowed);
        Assert.Equal(2, deniedResult2.CurrentCount);
    }

    [Fact]
    public async Task CheckRateLimitAsync_DifferentUsers_SeparateLimits()
    {
        var key1 = new RateLimitKey("user1", "DocumentationStart");
        var key2 = new RateLimitKey("user2", "DocumentationStart");
        var policy = new RateLimitPolicy("test", 2, TimeSpan.FromSeconds(60));

        // User1 makes 2 requests.
        for (int i = 0; i < 2; i++)
        {
            var result = await _sut.CheckRateLimitAsync(key1, policy);
            Assert.True(result.IsAllowed);
        }

        // User2 should be able to make 2 requests independently.
        var result1 = await _sut.CheckRateLimitAsync(key2, policy);
        Assert.True(result1.IsAllowed);
        Assert.Equal(1, result1.CurrentCount);

        var result2 = await _sut.CheckRateLimitAsync(key2, policy);
        Assert.True(result2.IsAllowed);
        Assert.Equal(2, result2.CurrentCount);

        // User2's third request should be denied.
        var deniedResult = await _sut.CheckRateLimitAsync(key2, policy);
        Assert.False(deniedResult.IsAllowed);
    }

    [Fact]
    public async Task CheckRateLimitAsync_DifferentEndpoints_SeparateLimits()
    {
        var key1 = new RateLimitKey("user1", "DocumentationStart");
        var key2 = new RateLimitKey("user1", "SpeechTokenGet");
        var policy = new RateLimitPolicy("test", 2, TimeSpan.FromSeconds(60));

        // Same user, endpoint 1: 2 requests.
        for (int i = 0; i < 2; i++)
        {
            var result = await _sut.CheckRateLimitAsync(key1, policy);
            Assert.True(result.IsAllowed);
        }

        // Same user, endpoint 2: should be able to make 2 requests independently.
        var result1 = await _sut.CheckRateLimitAsync(key2, policy);
        Assert.True(result1.IsAllowed);
        Assert.Equal(1, result1.CurrentCount);

        var result2 = await _sut.CheckRateLimitAsync(key2, policy);
        Assert.True(result2.IsAllowed);
        Assert.Equal(2, result2.CurrentCount);

        // Endpoint 2's third request should be denied.
        var deniedResult = await _sut.CheckRateLimitAsync(key2, policy);
        Assert.False(deniedResult.IsAllowed);
    }

    [Fact]
    public async Task ResetAsync_ClearsAllBuckets()
    {
        var key = new RateLimitKey("user1", "DocumentationStart");
        var policy = new RateLimitPolicy("test", 2, TimeSpan.FromSeconds(60));

        // Make 2 requests to hit the limit.
        for (int i = 0; i < 2; i++)
        {
            await _sut.CheckRateLimitAsync(key, policy);
        }

        var deniedResult = await _sut.CheckRateLimitAsync(key, policy);
        Assert.False(deniedResult.IsAllowed);

        // Reset.
        await _sut.ResetAsync(key);

        // After reset, should be able to make requests again.
        var result = await _sut.CheckRateLimitAsync(key, policy);
        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.CurrentCount);
    }

    [Fact]
    public async Task RateLimitResult_HasCorrectResetTime()
    {
        var key = new RateLimitKey("user1", "DocumentationStart");
        var policy = new RateLimitPolicy("test", 5, TimeSpan.FromSeconds(10));

        var result = await _sut.CheckRateLimitAsync(key, policy);

        Assert.NotNull(result);
        Assert.True(result.WindowResetTime > DateTime.UtcNow);
        var timeUntilReset = result.TimeUntilReset;
        Assert.True(timeUntilReset.TotalSeconds > 0 && timeUntilReset.TotalSeconds <= 10);
    }
}
