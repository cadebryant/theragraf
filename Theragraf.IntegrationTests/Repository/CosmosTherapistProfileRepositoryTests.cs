namespace Theragraf.IntegrationTests.Repository;

using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;
using Theragraf.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// End-to-end integration tests for <see cref="CosmosTherapistProfileRepository"/> running
/// against the local Azure Cosmos DB Emulator (https://localhost:8081).
///
/// Tests are skipped (not failed) when the emulator is absent.
/// </summary>
[Collection(CosmosCollection.Name)]
[Trait("Category", "Integration")]
public class CosmosTherapistProfileRepositoryTests(CosmosFixture cosmos)
{
    // Unique tenant and therapist IDs per test run to avoid cross-test pollution.
    private readonly string _tenantId     = $"tenant-{Guid.NewGuid():N}";
    private readonly string _therapistId  = $"therapist-{Guid.NewGuid():N}";

    private CosmosTherapistProfileRepository CreateRepository() =>
        new(cosmos.Client,
            CosmosFixture.DatabaseName,
            CosmosFixture.TherapistProfilesContainerName,
            NullLogger<CosmosTherapistProfileRepository>.Instance);

    private TherapistProfileDocument BuildProfile(
        string? tenantId    = null,
        string? therapistId = null) => new()
    {
        Id          = therapistId ?? _therapistId,
        TherapistId = therapistId ?? _therapistId,
        TenantId    = tenantId    ?? _tenantId,
        FirstName   = "Alice",
        LastName    = "Smith",
        Credentials = "OTR/L",
        Discipline  = TherapyDiscipline.OccupationalTherapy,
        IndividualNpi = "1234567890",
        CreatedAt   = DateTimeOffset.UtcNow,
        UpdatedAt   = DateTimeOffset.UtcNow,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetAsync_UnknownProfile_ReturnsNull()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo = CreateRepository();
        var result = await repo.GetAsync(_tenantId, _therapistId);

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpsertAsync_NewProfile_CanBeReadBack()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo    = CreateRepository();
        var profile = BuildProfile();

        await repo.UpsertAsync(profile);
        var stored = await repo.GetAsync(_tenantId, _therapistId);

        stored.Should().NotBeNull();
        stored!.TherapistId.Should().Be(_therapistId);
        stored.TenantId.Should().Be(_tenantId);
        stored.FirstName.Should().Be("Alice");
        stored.Credentials.Should().Be("OTR/L");
        stored.IndividualNpi.Should().Be("1234567890");
    }

    [SkippableFact]
    public async Task UpsertAsync_UpdateExistingProfile_ChangesArePersisted()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo    = CreateRepository();
        var profile = BuildProfile();
        await repo.UpsertAsync(profile);

        // Simulate a partial update: change credentials only.
        profile.Credentials = "OTR/L, CHT";
        await repo.UpsertAsync(profile);

        var stored = await repo.GetAsync(_tenantId, _therapistId);
        stored!.Credentials.Should().Be("OTR/L, CHT");
        stored.FirstName.Should().Be("Alice");  // untouched field preserved
    }

    [SkippableFact]
    public async Task UpsertAsync_SetsUpdatedAt()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo    = CreateRepository();
        var before  = DateTimeOffset.UtcNow.AddSeconds(-1);
        var profile = BuildProfile();

        var saved = await repo.UpsertAsync(profile);

        saved.UpdatedAt.Should().BeAfter(before);
    }

    [SkippableFact]
    public async Task GetAsync_DifferentTenantId_ReturnsNull()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo = CreateRepository();
        await repo.UpsertAsync(BuildProfile(tenantId: _tenantId));

        // Same therapistId but different tenantId — must not be found.
        var foreignTenantId = $"tenant-other-{Guid.NewGuid():N}";
        var result = await repo.GetAsync(foreignTenantId, _therapistId);

        result.Should().BeNull("profiles are partitioned by tenantId and must not cross tenant boundaries");
    }

    [SkippableFact]
    public async Task GetAsync_DifferentTherapistId_ReturnsNull()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo = CreateRepository();
        await repo.UpsertAsync(BuildProfile(therapistId: _therapistId));

        var otherId = $"therapist-other-{Guid.NewGuid():N}";
        var result  = await repo.GetAsync(_tenantId, otherId);

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpsertAsync_TwoTherapistsSameTenant_StoredIndependently()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo         = CreateRepository();
        var therapistId2 = $"therapist-{Guid.NewGuid():N}";

        var profile1 = BuildProfile(therapistId: _therapistId);
        profile1.FirstName = "Alice";

        var profile2 = BuildProfile(therapistId: therapistId2);
        profile2.FirstName = "Bob";

        await repo.UpsertAsync(profile1);
        await repo.UpsertAsync(profile2);

        var stored1 = await repo.GetAsync(_tenantId, _therapistId);
        var stored2 = await repo.GetAsync(_tenantId, therapistId2);

        stored1!.FirstName.Should().Be("Alice");
        stored2!.FirstName.Should().Be("Bob");
    }
}
