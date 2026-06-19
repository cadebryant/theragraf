namespace Theragraf.IntegrationTests.Repository;

using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;
using Theragraf.IntegrationTests.Infrastructure;

/// <summary>
/// End-to-end integration tests for <see cref="CosmosGoalRepository"/> running
/// against the local Azure Cosmos DB Emulator (https://localhost:8081).
///
/// Tests are skipped (not failed) when the emulator is absent, making them
/// safe to run in CI environments that do not have the emulator installed.
/// </summary>
[Collection(CosmosCollection.Name)]
[Trait("Category", "Integration")]
public class CosmosGoalRepositoryTests(CosmosFixture cosmos)
{
    // Each test uses a unique clientId so parallel runs and test isolation are guaranteed.
    private readonly string _clientId = $"goals-integration-{Guid.NewGuid():N}";

    private CosmosGoalRepository CreateRepository()
    {
        var retentionPolicy = new RetentionPolicy
        {
            RetentionYears = 6,
            AutoPurgeEnabled = false,
            RetentionStartsFrom = RetentionStartMode.CreatedAt
        };
        return new(cosmos.Client, CosmosFixture.DatabaseName, CosmosFixture.GoalsContainerName, retentionPolicy);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CreateGoalRequest BasicRequest(string title = "Improve dressing") =>
        new(Title: title, Description: "Client will independently don upper-body clothing within 4 weeks.", TargetDate: null);

    // ── Create ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task CreateAsync_Persists_GoalDocument()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.CreateAsync(_clientId, BasicRequest());

        result.Should().NotBeNull();
        result.GoalId.Should().NotBeNullOrEmpty();
        result.ClientId.Should().Be(_clientId);
        result.Title.Should().Be("Improve dressing");
        result.Status.Should().Be(GoalStatus.Active);
        result.ProgressNotes.Should().BeEmpty();
        result.ResolvedAt.Should().BeNull();
    }

    [SkippableFact]
    public async Task CreateAsync_AssignsUniqueGoalIds()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var first  = await repo.CreateAsync(_clientId, BasicRequest("Goal 1"));
        var second = await repo.CreateAsync(_clientId, BasicRequest("Goal 2"));

        first.GoalId.Should().NotBe(second.GoalId);
    }

    [SkippableFact]
    public async Task CreateAsync_WithTargetDate_PersistedCorrectly()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var target = DateTimeOffset.UtcNow.AddDays(30);

        var result = await repo.CreateAsync(_clientId, new CreateGoalRequest("Goal", "Desc", target));

        result.TargetDate.Should().BeCloseTo(target, TimeSpan.FromSeconds(1));
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetByIdAsync_ReturnsPersistedGoal()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        var fetched = await repo.GetByIdAsync(_clientId, created.GoalId);

        fetched.Should().NotBeNull();
        fetched!.GoalId.Should().Be(created.GoalId);
        fetched.Title.Should().Be("Improve dressing");
    }

    [SkippableFact]
    public async Task GetByIdAsync_NonExistentGoal_ReturnsNull()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.GetByIdAsync(_clientId, Guid.NewGuid().ToString());

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task GetByClientIdAsync_ReturnsAllGoalsForClient()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.CreateAsync(_clientId, BasicRequest("Goal A"));
        await repo.CreateAsync(_clientId, BasicRequest("Goal B"));
        await repo.CreateAsync(_clientId, BasicRequest("Goal C"));

        var goals = await repo.GetByClientIdAsync(_clientId);

        goals.Should().HaveCount(3);
        goals.Select(g => g.Title).Should().Contain(["Goal A", "Goal B", "Goal C"]);
    }

    [SkippableFact]
    public async Task GetByClientIdAsync_EmptyClient_ReturnsEmptyList()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var goals = await repo.GetByClientIdAsync(_clientId);

        goals.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task GetByClientIdAsync_DoesNotReturnGoalsFromOtherClients()
    {
        cosmos.SkipIfUnavailable();
        var repo        = CreateRepository();
        var otherClient = $"other-{Guid.NewGuid():N}";

        await repo.CreateAsync(_clientId,  BasicRequest("My goal"));
        await repo.CreateAsync(otherClient, BasicRequest("Other goal"));

        var mine = await repo.GetByClientIdAsync(_clientId);

        mine.Should().HaveCount(1);
        mine[0].Title.Should().Be("My goal");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task UpdateAsync_Title_UpdatedInPlace()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        var updated = await repo.UpdateAsync(_clientId, created.GoalId, new UpdateGoalRequest(Title: "Revised title", null, null, null, null));

        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Revised title");
        updated.Description.Should().Be(created.Description);  // unchanged
    }

    [SkippableFact]
    public async Task UpdateAsync_Status_ToMet_SetsResolvedAt()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        var updated = await repo.UpdateAsync(_clientId, created.GoalId,
            new UpdateGoalRequest(null, null, GoalStatus.Met, null, null));

        updated!.Status.Should().Be(GoalStatus.Met);
        updated.ResolvedAt.Should().NotBeNull();
        updated.ResolvedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [SkippableFact]
    public async Task UpdateAsync_Status_ToDiscontinued_SetsResolvedAt()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        var updated = await repo.UpdateAsync(_clientId, created.GoalId,
            new UpdateGoalRequest(null, null, GoalStatus.Discontinued, null, null));

        updated!.Status.Should().Be(GoalStatus.Discontinued);
        updated.ResolvedAt.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task UpdateAsync_Status_BackToActive_ClearsResolvedAt()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        // First mark as Met, then reactivate.
        await repo.UpdateAsync(_clientId, created.GoalId, new UpdateGoalRequest(null, null, GoalStatus.Met, null, null));
        var reactivated = await repo.UpdateAsync(_clientId, created.GoalId, new UpdateGoalRequest(null, null, GoalStatus.Active, null, null));

        reactivated!.Status.Should().Be(GoalStatus.Active);
        reactivated.ResolvedAt.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpdateAsync_ProgressNote_AppendedToList()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        var updated = await repo.UpdateAsync(_clientId, created.GoalId,
            new UpdateGoalRequest(null, null, null, null, ProgressNote: "Good session today"));

        updated!.ProgressNotes.Should().HaveCount(1);
        updated.ProgressNotes[0].Note.Should().Be("Good session today");
        updated.ProgressNotes[0].NoteId.Should().NotBeNullOrEmpty();
    }

    [SkippableFact]
    public async Task UpdateAsync_MultipleProgressNotes_AllPersisted()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());
        var goalId  = created.GoalId;

        await repo.UpdateAsync(_clientId, goalId, new UpdateGoalRequest(null, null, null, null, "Note 1"));
        await repo.UpdateAsync(_clientId, goalId, new UpdateGoalRequest(null, null, null, null, "Note 2"));
        var final = await repo.UpdateAsync(_clientId, goalId, new UpdateGoalRequest(null, null, null, null, "Note 3"));

        final!.ProgressNotes.Should().HaveCount(3);
        final.ProgressNotes.Select(n => n.Note).Should().Contain(["Note 1", "Note 2", "Note 3"]);
    }

    [SkippableFact]
    public async Task UpdateAsync_GoalNotFound_ReturnsNull()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.UpdateAsync(_clientId, Guid.NewGuid().ToString(),
            new UpdateGoalRequest(Title: "X", null, null, null, null));

        result.Should().BeNull();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task DeleteAsync_ExistingGoal_ReturnsTrueAndRemovesDocument()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        var deleted = await repo.DeleteAsync(_clientId, created.GoalId, "test-therapist");

        deleted.Should().BeTrue();
        var fetched = await repo.GetByIdAsync(_clientId, created.GoalId);
        fetched.Should().BeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_NonExistentGoal_ReturnsFalse()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.DeleteAsync(_clientId, Guid.NewGuid().ToString(), "test-therapist");

        result.Should().BeFalse();
    }

    [SkippableFact]
    public async Task DeleteAsync_OtherGoalsUnaffected()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var goalA  = await repo.CreateAsync(_clientId, BasicRequest("A"));
        var goalB  = await repo.CreateAsync(_clientId, BasicRequest("B"));

        await repo.DeleteAsync(_clientId, goalA.GoalId, "test-therapist");

        var remaining = await repo.GetByClientIdAsync(_clientId);
        remaining.Should().HaveCount(1);
        remaining[0].GoalId.Should().Be(goalB.GoalId);
    }

    [SkippableFact]
    public async Task RestoreAsync_RestoresSoftDeletedGoal()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        // Soft-delete
        await repo.DeleteAsync(_clientId, created.GoalId, "test-therapist");

        // Restore
        var restored = await repo.RestoreAsync(_clientId, created.GoalId);
        restored.Should().BeTrue();

        // Verify it's accessible again
        var fetched = await repo.GetByIdAsync(_clientId, created.GoalId);
        fetched.Should().NotBeNull();
        fetched!.Title.Should().Be("Improve dressing");
    }

    [SkippableFact]
    public async Task RestoreAsync_ReturnsFalse_WhenGoalNotDeleted()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var created = await repo.CreateAsync(_clientId, BasicRequest());

        // Try to restore a non-deleted goal
        var restored = await repo.RestoreAsync(_clientId, created.GoalId);
        restored.Should().BeFalse();
    }

    [SkippableFact]
    public async Task RestoreAsync_ReturnsFalse_WhenGoalNotFound()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var restored = await repo.RestoreAsync(_clientId, Guid.NewGuid().ToString());
        restored.Should().BeFalse();
    }
}
