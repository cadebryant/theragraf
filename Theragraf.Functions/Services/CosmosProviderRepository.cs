namespace Theragraf.Functions.Services;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

/// <summary>
/// Azure Cosmos DB implementation of <see cref="IProviderRepository"/>.
/// Database: theragraf   Container: providers
/// PartitionKey: /tenantId (level 1) + /providerId (level 2)
/// </summary>
public class CosmosProviderRepository(
    CosmosClient cosmosClient,
    string databaseName,
    string containerName,
    ILogger<CosmosProviderRepository> logger) : IProviderRepository
{
    private readonly Container _container = cosmosClient.GetContainer(databaseName, containerName);

    public async Task<ProviderDocument?> GetAsync(
        string tenantId,
        string providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pk = new PartitionKeyBuilder()
                .Add(tenantId)
                .Add(providerId)
                .Build();

            var response = await _container.ReadItemAsync<ProviderDocument>(
                providerId, pk, cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read provider for tenant={TenantId} provider={ProviderId}",
                tenantId, providerId);
            throw;
        }
    }

    public async Task<ProviderDocument> UpsertAsync(
        ProviderDocument provider,
        CancellationToken cancellationToken = default)
    {
        provider.UpdatedAt = DateTimeOffset.UtcNow;
        if (provider.CreatedAt == default)
            provider.CreatedAt = provider.UpdatedAt;

        var pk = new PartitionKeyBuilder()
            .Add(provider.TenantId)
            .Add(provider.ProviderId)
            .Build();

        var response = await _container.UpsertItemAsync(
            provider, pk, cancellationToken: cancellationToken);
        return response.Resource;
    }
}
