namespace Theragraf.Functions.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Theragraf.Functions.Models;

/// <summary>
/// Distributed rate limit service backed by Cosmos DB.
/// Each rate limit bucket is stored as a document with TTL set to the time window,
/// ensuring automatic cleanup after the window expires.
/// </summary>
public sealed class CosmosRateLimitService : IRateLimitService
{
    private readonly Container _container;
    private readonly ILogger<CosmosRateLimitService> _logger;

    /// <summary>
    /// Partition key used for all rate limit documents.
    /// </summary>
    private const string PartitionKey = "/userId";

    /// <summary>
    /// Container name where rate limit state is stored.
    /// </summary>
    public const string ContainerName = "rate-limits";

    public CosmosRateLimitService(CosmosClient cosmosClient, string databaseId, ILogger<CosmosRateLimitService> logger)
    {
        var database = cosmosClient.GetDatabase(databaseId);
        _container = database.GetContainer(ContainerName);
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(
        RateLimitKey key,
        RateLimitPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var documentId = GenerateDocumentId(key, policy);
        var now = DateTime.UtcNow;
        var windowStart = now - policy.TimeWindow;

        try
        {
            // Attempt to read the existing counter document.
            var response = await _container.ReadItemAsync<RateLimitDocument>(
                documentId,
                new PartitionKey(key.UserId),
                cancellationToken: cancellationToken);

            var doc = response.Resource;

            // If the window has expired, reset it.
            if (doc.WindowStart < windowStart)
            {
                doc.Count = 1;
                doc.WindowStart = now;
                doc.TimeToLive = (int)policy.TimeWindow.TotalSeconds;

                await _container.UpsertItemAsync(doc, new PartitionKey(key.UserId), cancellationToken: cancellationToken);

                var resetWindowResetTime = now.AddSeconds(policy.TimeWindow.TotalSeconds);
                return RateLimitResult.Allowed(1, policy.MaxRequests, resetWindowResetTime);
            }

            // Window is still active. Check if we've hit the limit.
            if (doc.Count >= policy.MaxRequests)
            {
                var windowResetTime = doc.WindowStart.AddSeconds(policy.TimeWindow.TotalSeconds);
                _logger.LogWarning(
                    "Rate limit exceeded for user {UserId} endpoint {Endpoint}: {Count}/{Max}",
                    key.UserId, key.EndpointName, doc.Count, policy.MaxRequests);

                return RateLimitResult.Denied(doc.Count, policy.MaxRequests, windowResetTime);
            }

            // Increment the counter.
            doc.Count++;
            var windowResetTime2 = doc.WindowStart.AddSeconds(policy.TimeWindow.TotalSeconds);
            await _container.UpsertItemAsync(doc, new PartitionKey(key.UserId), cancellationToken: cancellationToken);

            return RateLimitResult.Allowed(doc.Count, policy.MaxRequests, windowResetTime2);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // First request in this window; create a new document.
            var doc = new RateLimitDocument
            {
                Id = documentId,
                UserId = key.UserId,
                EndpointName = key.EndpointName,
                PolicyName = policy.Name,
                Count = 1,
                WindowStart = now,
                TimeToLive = (int)policy.TimeWindow.TotalSeconds,
            };

            await _container.UpsertItemAsync(doc, new PartitionKey(key.UserId), cancellationToken: cancellationToken);

            var windowResetTime = now.AddSeconds(policy.TimeWindow.TotalSeconds);
            return RateLimitResult.Allowed(1, policy.MaxRequests, windowResetTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for user {UserId} endpoint {Endpoint}", key.UserId, key.EndpointName);
            // On error, allow the request to avoid cascading failures, but log for monitoring.
            var now2 = DateTime.UtcNow;
            var windowResetTime2 = now2.AddSeconds(policy.TimeWindow.TotalSeconds);
            return RateLimitResult.Allowed(0, policy.MaxRequests, windowResetTime2);
        }
    }

    public async Task ResetAsync(RateLimitKey key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find all documents for this user/endpoint and delete them.
            var query = _container.GetItemQueryIterator<RateLimitDocument>(
                new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId AND c.endpointName = @endpoint")
                    .WithParameter("@userId", key.UserId)
                    .WithParameter("@endpoint", key.EndpointName));

            while (query.HasMoreResults)
            {
                var items = await query.ReadNextAsync(cancellationToken);
                foreach (var item in items)
                {
                    await _container.DeleteItemAsync<RateLimitDocument>(
                        item.Id,
                        new PartitionKey(key.UserId),
                        cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting rate limit for user {UserId} endpoint {Endpoint}", key.UserId, key.EndpointName);
        }
    }

    private static string GenerateDocumentId(RateLimitKey key, RateLimitPolicy policy) =>
        $"ratelimit#{key.UserId}#{key.EndpointName}#{policy.Name}";

    /// <summary>
    /// Internal document model for Cosmos DB storage.
    /// </summary>
    private sealed class RateLimitDocument
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string EndpointName { get; set; }
        public required string PolicyName { get; set; }
        public required int Count { get; set; }
        public required DateTime WindowStart { get; set; }
        public required int TimeToLive { get; set; }
    }
}
