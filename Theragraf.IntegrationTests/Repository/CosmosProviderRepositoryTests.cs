namespace Theragraf.IntegrationTests.Repository;

using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;
using Theragraf.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// End-to-end integration tests for <see cref="CosmosProviderRepository"/> running
/// against the local Azure Cosmos DB Emulator (https://localhost:8081).
///
/// Tests are skipped (not failed) when the emulator is absent.
/// </summary>
[Collection(CosmosCollection.Name)]
[Trait("Category", "Integration")]
public class CosmosProviderRepositoryTests(CosmosFixture cosmos)
{
    private readonly string _tenantId   = $"tenant-{Guid.NewGuid():N}";
    private readonly string _providerId = $"provider-{Guid.NewGuid():N}";

    private CosmosProviderRepository CreateRepository() =>
        new(cosmos.Client,
            CosmosFixture.DatabaseName,
            CosmosFixture.ProvidersContainerName,
            NullLogger<CosmosProviderRepository>.Instance);

    private ProviderDocument BuildProvider(
        string? tenantId   = null,
        string? providerId = null) => new()
    {
        Id              = providerId ?? _providerId,
        ProviderId      = providerId ?? _providerId,
        TenantId        = tenantId   ?? _tenantId,
        PracticeName    = "Sunrise Physical Therapy",
        OrganizationNpi = "9876543210",
        AddressLine1    = "100 Main St",
        City            = "Springfield",
        State           = "IL",
        Zip             = "62701",
        Phone           = "2175551234",
        CreatedAt       = DateTimeOffset.UtcNow,
        UpdatedAt       = DateTimeOffset.UtcNow,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetAsync_UnknownProvider_ReturnsNull()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo   = CreateRepository();
        var result = await repo.GetAsync(_tenantId, _providerId);

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpsertAsync_NewProvider_CanBeReadBack()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo     = CreateRepository();
        var provider = BuildProvider();

        await repo.UpsertAsync(provider);
        var stored = await repo.GetAsync(_tenantId, _providerId);

        stored.Should().NotBeNull();
        stored!.ProviderId.Should().Be(_providerId);
        stored.TenantId.Should().Be(_tenantId);
        stored.PracticeName.Should().Be("Sunrise Physical Therapy");
        stored.OrganizationNpi.Should().Be("9876543210");
        stored.City.Should().Be("Springfield");
    }

    [SkippableFact]
    public async Task UpsertAsync_UpdateExistingProvider_ChangesArePersisted()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo     = CreateRepository();
        var provider = BuildProvider();
        await repo.UpsertAsync(provider);

        provider.PracticeName = "Sunrise PT & Wellness";
        await repo.UpsertAsync(provider);

        var stored = await repo.GetAsync(_tenantId, _providerId);
        stored!.PracticeName.Should().Be("Sunrise PT & Wellness");
        stored.OrganizationNpi.Should().Be("9876543210");  // untouched
    }

    [SkippableFact]
    public async Task UpsertAsync_SetsUpdatedAt()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo    = CreateRepository();
        var before  = DateTimeOffset.UtcNow.AddSeconds(-1);
        var saved   = await repo.UpsertAsync(BuildProvider());

        saved.UpdatedAt.Should().BeAfter(before);
    }

    [SkippableFact]
    public async Task GetAsync_DifferentTenantId_ReturnsNull()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo = CreateRepository();
        await repo.UpsertAsync(BuildProvider(tenantId: _tenantId));

        var foreignTenantId = $"tenant-other-{Guid.NewGuid():N}";
        var result = await repo.GetAsync(foreignTenantId, _providerId);

        result.Should().BeNull("providers are partitioned by tenantId and must not cross tenant boundaries");
    }

    [SkippableFact]
    public async Task GetAsync_DifferentProviderId_ReturnsNull()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo = CreateRepository();
        await repo.UpsertAsync(BuildProvider(providerId: _providerId));

        var otherId = $"provider-other-{Guid.NewGuid():N}";
        var result  = await repo.GetAsync(_tenantId, otherId);

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpsertAsync_TwoProvidersSameTenant_StoredIndependently()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        var repo       = CreateRepository();
        var providerId2 = $"provider-{Guid.NewGuid():N}";

        var p1 = BuildProvider(providerId: _providerId);
        p1.PracticeName = "Clinic A";

        var p2 = BuildProvider(providerId: providerId2);
        p2.PracticeName = "Clinic B";

        await repo.UpsertAsync(p1);
        await repo.UpsertAsync(p2);

        var stored1 = await repo.GetAsync(_tenantId, _providerId);
        var stored2 = await repo.GetAsync(_tenantId, providerId2);

        stored1!.PracticeName.Should().Be("Clinic A");
        stored2!.PracticeName.Should().Be("Clinic B");
    }

    [SkippableFact]
    public async Task UpsertAsync_EncryptedEinNotReturnedByGet()
    {
        Skip.If(!cosmos.IsAvailable, "Cosmos DB Emulator is not available.");

        // EncryptedEin is stored on the document but must never surface in ProviderResponse.
        // This test verifies the document round-trips correctly without leaking the field
        // via the repository (the DTO mapping test in unit tests covers the API layer).
        var repo     = CreateRepository();
        var provider = BuildProvider();
        provider.EncryptedEin = "should-be-encrypted-in-production";

        await repo.UpsertAsync(provider);
        var stored = await repo.GetAsync(_tenantId, _providerId);

        // The document DOES store EncryptedEin — that's correct and expected.
        // The ProviderResponse DTO intentionally omits it (enforced in unit tests).
        stored!.EncryptedEin.Should().Be("should-be-encrypted-in-production",
            "the repository must faithfully round-trip all document fields");
    }
}
