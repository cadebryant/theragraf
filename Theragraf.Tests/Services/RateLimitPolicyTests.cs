namespace Theragraf.Tests.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Theragraf.Functions.Models;
using Theragraf.Functions.Services;

/// <summary>
/// Tests for the rate limit policy presets and configuration.
/// </summary>
public sealed class RateLimitPolicyTests
{
    [Fact]
    public void SpeechTokenPreset_HasCorrectLimits()
    {
        var policy = RateLimitPolicy.Presets.SpeechToken;
        Assert.Equal("SpeechToken", policy.Name);
        Assert.Equal(10, policy.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.TimeWindow);
    }

    [Fact]
    public void DocumentationPipelinePreset_HasCorrectLimits()
    {
        var policy = RateLimitPolicy.Presets.DocumentationPipeline;
        Assert.Equal("DocumentationPipeline", policy.Name);
        Assert.Equal(20, policy.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.TimeWindow);
    }

    [Fact]
    public void MutationPreset_HasCorrectLimits()
    {
        var policy = RateLimitPolicy.Presets.Mutation;
        Assert.Equal("Mutation", policy.Name);
        Assert.Equal(50, policy.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.TimeWindow);
    }

    [Fact]
    public void ReadOnlyPreset_HasCorrectLimits()
    {
        var policy = RateLimitPolicy.Presets.ReadOnly;
        Assert.Equal("ReadOnly", policy.Name);
        Assert.Equal(100, policy.MaxRequests);
        Assert.Equal(TimeSpan.FromMinutes(1), policy.TimeWindow);
    }

    [Fact]
    public void RateLimitKey_CreatesCorrectly()
    {
        var key = new RateLimitKey("user1", "DocumentationStart");
        Assert.Equal("user1", key.UserId);
        Assert.Equal("DocumentationStart", key.EndpointName);
    }
}
