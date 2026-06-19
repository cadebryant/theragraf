namespace Theragraf.Functions.Services;

using Microsoft.Azure.Cosmos;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

/// <summary>
/// Azure Cosmos DB for NoSQL implementation of <see cref="IGoalRepository"/>.
/// Database: theragraf   Container: goals   PartitionKey: /clientId
/// </summary>
public class CosmosGoalRepository : IGoalRepository
{
    private readonly Container       _container;
    private readonly RetentionPolicy _retentionPolicy;

    public CosmosGoalRepository(
        CosmosClient     cosmosClient,
        string           databaseName,
        string           containerName,
        RetentionPolicy  retentionPolicy)
    {
        _container       = cosmosClient.GetContainer(databaseName, containerName);
        _retentionPolicy = retentionPolicy;
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GoalResponse>> GetByClientIdAsync(
        string clientId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.clientId = @clientId " +
            "AND (NOT IS_DEFINED(c.isDeleted) OR c.isDeleted = false) " +
            "ORDER BY c.createdAt DESC")
            .WithParameter("@clientId", clientId);

        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(clientId) };
        var iterator = _container.GetItemQueryIterator<GoalDocument>(query, requestOptions: options);

        var results = new List<GoalResponse>();
        using (iterator)
        {
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page.Select(MapToResponse));
            }
        }
        return results;
    }

    public async Task<GoalResponse?> GetByIdAsync(
        string clientId, string goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<GoalDocument>(
                goalId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            var doc = response.Resource;
            // Exclude soft-deleted documents
            if (doc.IsDeleted)
                return null;
            return MapToResponse(doc);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task<GoalResponse> CreateAsync(
        string clientId, CreateGoalRequest request, CancellationToken cancellationToken = default)
    {
        var doc = new GoalDocument
        {
            Id          = Guid.NewGuid().ToString(),
            ClientId    = clientId,
            Title       = request.Title,
            Description = request.Description,
            Status      = nameof(GoalStatus.Active),
            CreatedAt   = DateTimeOffset.UtcNow,
            TargetDate  = request.TargetDate,
        };

        await _container.CreateItemAsync(doc, new PartitionKey(clientId), cancellationToken: cancellationToken);
        return MapToResponse(doc);
    }

    public async Task<GoalResponse?> UpdateAsync(
        string clientId, string goalId, UpdateGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        GoalDocument? doc;
        try
        {
            var existing = await _container.ReadItemAsync<GoalDocument>(
                goalId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            doc = existing.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (request.Title       is not null) doc.Title       = request.Title;
        if (request.Description is not null) doc.Description = request.Description;
        if (request.TargetDate  is not null) doc.TargetDate  = request.TargetDate;

        if (request.Status is not null)
        {
            doc.Status = request.Status.Value.ToString();

            // Stamp resolution time when transitioning to a terminal status.
            if (request.Status is GoalStatus.Met or GoalStatus.NotMet or GoalStatus.Discontinued)
                doc.ResolvedAt ??= DateTimeOffset.UtcNow;
            else
                doc.ResolvedAt = null;
        }

        if (!string.IsNullOrWhiteSpace(request.ProgressNote))
        {
            doc.ProgressNotes.Add(new GoalProgressNoteDocument
            {
                NoteId     = Guid.NewGuid().ToString(),
                RecordedAt = DateTimeOffset.UtcNow,
                Note       = request.ProgressNote,
            });
        }

        await _container.UpsertItemAsync(doc, new PartitionKey(clientId), cancellationToken: cancellationToken);
        return MapToResponse(doc);
    }

    public async Task<bool> DeleteAsync(
        string clientId, string goalId, string deletedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read the document first
            var response = await _container.ReadItemAsync<GoalDocument>(
                goalId, new PartitionKey(clientId), cancellationToken: cancellationToken);

            var doc = response.Resource;

            // Mark as soft-deleted
            doc.IsDeleted = true;
            doc.DeletedAt = DateTimeOffset.UtcNow;
            doc.DeletedBy = deletedBy;

            // Calculate TTL if auto-purge is enabled
            var purgeDate = _retentionPolicy.CalculatePurgeDate(doc.CreatedAt, doc.DeletedAt);
            if (purgeDate.HasValue)
            {
                var secondsUntilPurge = (int)(purgeDate.Value - DateTimeOffset.UtcNow).TotalSeconds;
                doc.TimeToLive = secondsUntilPurge > 0 ? secondsUntilPurge : 1;
            }
            else
            {
                doc.TimeToLive = null;
            }

            // Update the document
            await _container.ReplaceItemAsync(doc, goalId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    public async Task<bool> RestoreAsync(
        string clientId, string goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read the document
            var response = await _container.ReadItemAsync<GoalDocument>(
                goalId, new PartitionKey(clientId), cancellationToken: cancellationToken);

            var doc = response.Resource;

            // Only restore if currently deleted
            if (!doc.IsDeleted)
                return false;

            // Clear deletion markers
            doc.IsDeleted = false;
            doc.DeletedAt = null;
            doc.DeletedBy = null;
            doc.TimeToLive = null;

            // Update the document
            await _container.ReplaceItemAsync(doc, goalId, new PartitionKey(clientId), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static GoalResponse MapToResponse(GoalDocument doc)
    {
        var status = Enum.TryParse<GoalStatus>(doc.Status, out var parsed)
            ? parsed
            : GoalStatus.Active;

        return new GoalResponse(
            GoalId:        doc.Id,
            ClientId:      doc.ClientId,
            Title:         doc.Title,
            Description:   doc.Description,
            Status:        status,
            CreatedAt:     doc.CreatedAt,
            TargetDate:    doc.TargetDate,
            ResolvedAt:    doc.ResolvedAt,
            ProgressNotes: doc.ProgressNotes
                .OrderBy(n => n.RecordedAt)
                .Select(n => new GoalProgressNote(n.NoteId, n.RecordedAt, n.Note))
                .ToList(),
            IsSynthetic:   doc.IsSynthetic
        );
    }
}
