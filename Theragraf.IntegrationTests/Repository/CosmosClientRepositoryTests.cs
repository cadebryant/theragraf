namespace Theragraf.IntegrationTests.Repository;

using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;
using Theragraf.IntegrationTests.Infrastructure;

/// <summary>
/// End-to-end integration tests for <see cref="CosmosClientRepository"/> running
/// against the local Azure Cosmos DB Emulator.
///
/// <see cref="NullRedactionMapEncryption"/> is used so no Key Vault is required.
/// DOB is stored and compared as plaintext; in production it is AES-256-GCM encrypted.
///
/// Tests are skipped when the emulator is absent.
/// </summary>
[Collection(CosmosCollection.Name)]
[Trait("Category", "Integration")]
public class CosmosClientRepositoryTests(CosmosFixture cosmos)
{
    private readonly string _clientId = $"demo-{Guid.NewGuid():N}";

    private CosmosClientRepository CreateRepository() =>
        new(cosmos.Client, CosmosFixture.DatabaseName, CosmosFixture.ClientsContainerName,
            new NullRedactionMapEncryption());

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UpsertClientDemographicsRequest BasicRequest(
        string? dob = "1985-04-12",
        BiologicalSex sex = BiologicalSex.Female) =>
        new(DateOfBirth: dob, Sex: sex, PriorDiagnoses: "T2DM", FunctionalLimitations: "Limited ROM");

    // ── Get (no record) ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetAsync_NoRecord_ReturnsNull()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.GetAsync(_clientId);

        result.Should().BeNull();
    }

    // ── Upsert (create) ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task UpsertAsync_Create_PersistsRecord()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.UpsertAsync(_clientId, BasicRequest());

        result.Should().NotBeNull();
        result.ClientId.Should().Be(_clientId);
        result.Sex.Should().Be(BiologicalSex.Female);
        result.PriorDiagnoses.Should().Be("T2DM");
        result.FunctionalLimitations.Should().Be("Limited ROM");
    }

    [SkippableFact]
    public async Task UpsertAsync_WithDob_ComputesAgeYears()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();
        var dob = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30));

        var result = await repo.UpsertAsync(_clientId, BasicRequest(dob: dob.ToString("yyyy-MM-dd")));

        // Allow ±1 year tolerance for the test-run date boundary
        result.AgeYears.Should().BeInRange(29, 30);
    }

    [SkippableFact]
    public async Task UpsertAsync_NoDob_AgeYearsIsNull()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        var result = await repo.UpsertAsync(_clientId, BasicRequest(dob: null));

        result.AgeYears.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpsertAsync_EmptyStringDob_ClearsDob()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        // First set a DOB, then clear it.
        await repo.UpsertAsync(_clientId, BasicRequest(dob: "1990-01-01"));
        var cleared = await repo.UpsertAsync(_clientId, BasicRequest(dob: ""));

        cleared.AgeYears.Should().BeNull();
    }

    // ── Get (after upsert) ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetAsync_AfterUpsert_ReturnsSavedRecord()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();
        await repo.UpsertAsync(_clientId, BasicRequest());

        var result = await repo.GetAsync(_clientId);

        result.Should().NotBeNull();
        result!.ClientId.Should().Be(_clientId);
        result.Sex.Should().Be(BiologicalSex.Female);
    }

    [SkippableFact]
    public async Task GetAsync_DobNotReturned_OnlyAgeYearsExposed()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();
        await repo.UpsertAsync(_clientId, BasicRequest(dob: "1975-06-15"));

        var result = await repo.GetAsync(_clientId);

        // The response type has no DateOfBirth field — only AgeYears
        result.Should().NotBeNull();
        result!.AgeYears.Should().NotBeNull();
        // Verify there is no way to access raw DOB through the response
        var props = result.GetType().GetProperties().Select(p => p.Name);
        props.Should().NotContain("DateOfBirth").And.NotContain("EncryptedDateOfBirth");
    }

    // ── Upsert (update) ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task UpsertAsync_Update_ReplacesExistingRecord()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();
        await repo.UpsertAsync(_clientId, BasicRequest(sex: BiologicalSex.Female));

        var updated = await repo.UpsertAsync(_clientId,
            new UpsertClientDemographicsRequest(null, BiologicalSex.Male, "Revised Dx", "New limitations"));

        updated.Sex.Should().Be(BiologicalSex.Male);
        updated.PriorDiagnoses.Should().Be("Revised Dx");
    }

    [SkippableFact]
    public async Task UpsertAsync_OmitDob_PreservesExistingEncryptedDob()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        // Create with a DOB.
        await repo.UpsertAsync(_clientId, BasicRequest(dob: "1980-03-20"));
        // Update without supplying a new DOB (null = preserve).
        var updated = await repo.UpsertAsync(_clientId,
            new UpsertClientDemographicsRequest(null, BiologicalSex.Female, "Updated notes", null));

        // Age should still be computed from the preserved DOB.
        updated.AgeYears.Should().NotBeNull();
    }

    // ── Isolation ─────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetAsync_OtherClientId_ReturnsNull()
    {
        cosmos.SkipIfUnavailable();
        var repo        = CreateRepository();
        var otherClient = $"other-{Guid.NewGuid():N}";

        await repo.UpsertAsync(_clientId, BasicRequest());

        var result = await repo.GetAsync(otherClient);

        result.Should().BeNull();
    }
}
