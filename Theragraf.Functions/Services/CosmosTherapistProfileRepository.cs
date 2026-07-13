namespace Theragraf.Functions.Services;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

/// <summary>
/// Azure Cosmos DB implementation of <see cref="ITherapistProfileRepository"/>.
/// Database: theragraf   Container: therapist-profiles
/// PartitionKey: /tenantId (level 1) + /therapistId (level 2)
/// </summary>
public class CosmosTherapistProfileRepository(
    CosmosClient cosmosClient,
    string databaseName,
    string containerName,
    ILogger<CosmosTherapistProfileRepository> logger) : ITherapistProfileRepository
{
    private readonly Container _container = cosmosClient.GetContainer(databaseName, containerName);

    public async Task<TherapistProfileDocument?> GetAsync(
        string tenantId,
        string therapistId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pk = new PartitionKeyBuilder()
                .Add(tenantId)
                .Add(therapistId)
                .Build();

            var response = await _container.ReadItemAsync<TherapistProfileDocument>(
                therapistId, pk, cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read therapist profile for tenant={TenantId} therapist={TherapistId}",
                tenantId, therapistId);
            throw;
        }
    }

    public async Task<TherapistProfileDocument> UpsertAsync(
        TherapistProfileDocument profile,
        CancellationToken cancellationToken = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        if (profile.CreatedAt == default)
            profile.CreatedAt = profile.UpdatedAt;

        var pk = new PartitionKeyBuilder()
            .Add(profile.TenantId)
            .Add(profile.TherapistId)
            .Build();

        var response = await _container.UpsertItemAsync(
            profile, pk, cancellationToken: cancellationToken);
        return response.Resource;
    }
}
