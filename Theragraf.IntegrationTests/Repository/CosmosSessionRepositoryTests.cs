namespace Theragraf.IntegrationTests.Repository;

using System.Text.Json;
using FluentAssertions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;
using Theragraf.IntegrationTests.Infrastructure;

/// <summary>
/// End-to-end integration tests for <see cref="CosmosSessionRepository"/> running
/// against the local Azure Cosmos DB Emulator (https://localhost:8081).
///
/// Prerequisites:
///   - Azure Cosmos DB Emulator must be running locally.
///   - The emulator can be started from: Start > Azure Cosmos DB Emulator.
///     or via the Debug auto-start in Theragraf.Functions.csproj.
///
/// Tests are skipped (not failed) when the emulator is absent, making them
/// safe to run in CI environments that do not have the emulator installed.
/// </summary>
[Collection(CosmosCollection.Name)]
[Trait("Category", "Integration")]
public class CosmosSessionRepositoryTests(CosmosFixture cosmos)
{
    // Each test class uses a unique client prefix so parallel runs don't clash.
    private readonly string _clientId = $"integration-{Guid.NewGuid():N}";

    private CosmosSessionRepository CreateRepository() =>
        new(cosmos.Client, CosmosFixture.DatabaseName, CosmosFixture.ContainerName);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private SessionRecord BuildRecord(
        string rowKey,
        string therapist   = "Dr. Smith",
        string discipline  = "PT",
        string setting     = "Outpatient",
        string payer       = "Medicare",
        int    duration    = 45,
        string subjective  = "Patient reports mild pain.",
        string objective   = "ROM measured at 90 degrees.",
        string assessment  = "Progressing well.",
        string plan        = "Continue current plan.")
    {
        var soap = new SoapNote(subjective, objective, assessment, plan);
        var cptCodes = new List<CptCode> { new("97110", "Therapeutic exercise", "Standard exercise") };
        var icdCodes = new List<IcdCode> { new("M54.5", "Low back pain", "Primary diagnosis") };

        return new SessionRecord
        {
            PartitionKey           = _clientId,
            RowKey                 = rowKey,
            TherapistName          = therapist,
            Discipline             = discipline,
            Setting                = setting,
            Payer                  = payer,
            SessionDurationMinutes = duration,
            RedactionMapJson       = "{}",
            SoapNoteJson           = JsonSerializer.Serialize(soap),
            CptCodesJson           = JsonSerializer.Serialize(cptCodes),
            IcdCodesJson           = JsonSerializer.Serialize(icdCodes),
            CreatedAt              = DateTimeOffset.UtcNow
        };
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task SaveAsync_Persists_NewDocument()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = $"2024-06-01T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey));

        var result = await repo.GetByClientIdAndDateAsync(_clientId, rowKey);
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(_clientId);
        result.TherapistName.Should().Be("Dr. Smith");
    }

    [SkippableFact]
    public async Task SaveAsync_Upserts_ExistingDocument()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = $"2024-06-02T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey, therapist: "Dr. Jones"));
        await repo.SaveAsync(BuildRecord(rowKey, therapist: "Dr. Smith-Updated"));

        var result = await repo.GetByClientIdAndDateAsync(_clientId, rowKey);
        result!.TherapistName.Should().Be("Dr. Smith-Updated");
    }

    // ── GetByClientIdAndDate ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetByClientIdAndDateAsync_ReturnsNull_WhenNotFound()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var result = await repo.GetByClientIdAndDateAsync(_clientId, "2099-01-01T00-00-00Z");
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task GetByClientIdAndDateAsync_Returns_CorrectDocument()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = $"2024-07-15T09-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey, setting: "Home Health", payer: "Medicaid"));

        var result = await repo.GetByClientIdAndDateAsync(_clientId, rowKey);
        result.Should().NotBeNull();
        result!.Setting.Should().Be("Home Health");
        result.Payer.Should().Be("Medicaid");
        result.SoapNote.Subjective.Should().Be("Patient reports mild pain.");
    }

    // ── GetByClientId (unpaged) ───────────────────────────────────────────────

    [SkippableFact]
    public async Task GetByClientIdAsync_ReturnsAllDocuments_ForClient()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2024-08-01T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2024-08-02T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2024-08-03T10-00-00Z"));

        var results = await repo.GetByClientIdAsync(_clientId);
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.ClientId.Should().Be(_clientId));
    }

    [SkippableFact]
    public async Task GetByClientIdAsync_ReturnsEmpty_ForUnknownClient()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var results = await repo.GetByClientIdAsync($"unknown-{Guid.NewGuid():N}");
        results.Should().BeEmpty();
    }

    // ── GetByClientIdPaged ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_ReturnsFirstPage()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        for (int i = 1; i <= 5; i++)
            await repo.SaveAsync(BuildRecord($"2024-09-{i:D2}T10-00-00Z"));

        var page1 = await repo.GetByClientIdPagedAsync(_clientId, pageSize: 3, continuationToken: null);
        page1.Items.Should().HaveCount(3);
        page1.HasMore.Should().BeTrue();
        page1.ContinuationToken.Should().NotBeNullOrEmpty();
    }

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_ReturnsLastPage_WithNoMore()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        for (int i = 1; i <= 4; i++)
            await repo.SaveAsync(BuildRecord($"2024-10-{i:D2}T10-00-00Z"));

        var page1 = await repo.GetByClientIdPagedAsync(_clientId, pageSize: 3, continuationToken: null);
        page1.HasMore.Should().BeTrue();

        var page2 = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 3, continuationToken: page1.ContinuationToken);
        page2.Items.Should().HaveCount(1);
        page2.HasMore.Should().BeFalse();
        page2.ContinuationToken.Should().BeNull();
    }

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_ReturnsCorrectTotalItems_AcrossAllPages()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        const int total    = 7;
        const int pageSize = 3;
        for (int i = 1; i <= total; i++)
            await repo.SaveAsync(BuildRecord($"2024-11-{i:D2}T10-00-00Z"));

        var allItems          = new List<SessionResponse>();
        string? continuation  = null;

        do
        {
            var page     = await repo.GetByClientIdPagedAsync(_clientId, pageSize, continuation);
            allItems.AddRange(page.Items);
            continuation = page.ContinuationToken;
        } while (continuation is not null);

        allItems.Should().HaveCount(total);
    }

    // ── Filter options ────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_FiltersByDiscipline()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2024-12-01T10-00-00Z", discipline: "PT"));
        await repo.SaveAsync(BuildRecord("2024-12-02T10-00-00Z", discipline: "OT"));
        await repo.SaveAsync(BuildRecord("2024-12-03T10-00-00Z", discipline: "PT"));

        var page = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 10, continuationToken: null,
            options: new SessionQueryOptions { Discipline = "PT" });

        page.Items.Should().HaveCount(2);
        page.Items.Should().AllSatisfy(r => r.Discipline.Should().Be("PT"));
    }

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_FiltersByTherapist()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-01-01T10-00-00Z", therapist: "Dr. Smith"));
        await repo.SaveAsync(BuildRecord("2025-01-02T10-00-00Z", therapist: "Dr. Jones"));

        var page = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 10, continuationToken: null,
            options: new SessionQueryOptions { Therapist = "Dr. Jones" });

        page.Items.Should().HaveCount(1);
        page.Items[0].TherapistName.Should().Be("Dr. Jones");
    }

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_FiltersByPayer()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-02-01T10-00-00Z", payer: "Medicare"));
        await repo.SaveAsync(BuildRecord("2025-02-02T10-00-00Z", payer: "Medicaid"));
        await repo.SaveAsync(BuildRecord("2025-02-03T10-00-00Z", payer: "Medicare"));

        var page = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 10, continuationToken: null,
            options: new SessionQueryOptions { Payer = "Medicare" });

        page.Items.Should().HaveCount(2);
        page.Items.Should().AllSatisfy(r => r.Payer.Should().Be("Medicare"));
    }

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_FiltersByDateRange()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-03-01T10-00-00Z"));  // included
        await repo.SaveAsync(BuildRecord("2025-03-15T10-00-00Z"));  // included
        await repo.SaveAsync(BuildRecord("2025-04-01T10-00-00Z"));  // excluded

        var page = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 10, continuationToken: null,
            options: new SessionQueryOptions
            {
                DateFrom = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
                DateTo   = new DateTimeOffset(2025, 3, 31, 23, 59, 59, TimeSpan.Zero)
            });

        page.Items.Should().HaveCount(2);
    }

    // ── Sort options ──────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_SortsAscending_BySessionDate()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-05-03T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-05-01T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-05-02T10-00-00Z"));

        var page = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 10, continuationToken: null,
            options: new SessionQueryOptions { SortBy = "sessionDate", SortOrder = "asc" });

        var ids = page.Items.Select(r => r.SessionDate).ToList();
        ids.Should().BeInAscendingOrder();
    }

    [SkippableFact]
    public async Task GetByClientIdPagedAsync_SortsDescending_ByDefault()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-06-01T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-06-02T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-06-03T10-00-00Z"));

        var page = await repo.GetByClientIdPagedAsync(
            _clientId, pageSize: 10, continuationToken: null);

        var ids = page.Items.Select(r => r.SessionDate).ToList();
        ids.Should().BeInDescendingOrder();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task DeleteAsync_Removes_ExistingDocument()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = $"2025-07-01T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey));
        var deleted = await repo.DeleteAsync(_clientId, rowKey);

        deleted.Should().BeTrue();
        var result = await repo.GetByClientIdAndDateAsync(_clientId, rowKey);
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_ReturnsFalse_WhenDocumentNotFound()
    {
        cosmos.SkipIfUnavailable();
        var repo    = CreateRepository();
        var deleted = await repo.DeleteAsync(_clientId, "2099-01-01T00-00-00Z");
        deleted.Should().BeFalse();
    }

    // ── SoapNote PII redaction round-trip ─────────────────────────────────────

    [SkippableFact]
    public async Task SaveAsync_And_GetByDate_Restore_PiiPlaceholders()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = $"2025-08-01T10-00-00Z";

        var redactionMap = new Dictionary<string, string>
        {
            ["[PII_1]"] = "John Doe",
            ["[PII_2]"] = "555-1234"
        };

        var soap = new SoapNote(
            Subjective: "[PII_1] reports knee pain.",
            Objective:  "Phone: [PII_2]",
            Assessment: "Improving.",
            Plan:       "Follow up next week."
        );

        var record = new SessionRecord
        {
            PartitionKey           = _clientId,
            RowKey                 = rowKey,
            TherapistName          = "Dr. Smith",
            Discipline             = "PT",
            Setting                = "Outpatient",
            Payer                  = "Medicare",
            SessionDurationMinutes = 60,
            RedactionMapJson       = JsonSerializer.Serialize(redactionMap),
            SoapNoteJson           = JsonSerializer.Serialize(soap),
            CptCodesJson           = "[]",
            IcdCodesJson           = "[]",
            CreatedAt              = DateTimeOffset.UtcNow
        };

        await repo.SaveAsync(record);
        var result = await repo.GetByClientIdAndDateAsync(_clientId, rowKey);

        result.Should().NotBeNull();
        result!.SoapNote.Subjective.Should().Be("John Doe reports knee pain.");
        result.SoapNote.Objective.Should().Be("Phone: 555-1234");
    }
}
