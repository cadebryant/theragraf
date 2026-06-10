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
        new(cosmos.Client, CosmosFixture.DatabaseName, CosmosFixture.ContainerName, new NullRedactionMapEncryption());

    // -- Helpers ---------------------------------------------------------------

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
        string plan        = "Continue current plan.",
        string? clientId   = null)
    {
        var soap = new SoapNote(subjective, objective, assessment, plan);
        var cptCodes = new List<CptCode> { new("97110", "Therapeutic exercise", "Standard exercise") };
        var icdCodes = new List<IcdCode> { new("M54.5", "Low back pain", "Primary diagnosis") };

        return new SessionRecord
        {
            PartitionKey           = clientId ?? _clientId,
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

    // -- Save ------------------------------------------------------------------

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

    // -- GetByClientIdAndDate --------------------------------------------------

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

    // -- GetByClientId (unpaged) -----------------------------------------------

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

    // -- GetByClientIdPaged ----------------------------------------------------

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

    // -- Filter options --------------------------------------------------------

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

    // -- Sort options ----------------------------------------------------------

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

    // -- Delete ----------------------------------------------------------------

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

    // -- SoapNote PII redaction round-trip -------------------------------------

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

    // -- Update ----------------------------------------------------------------

    [SkippableFact]
    public async Task UpdateAsync_ReturnsNull_WhenDocumentNotFound()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var result = await repo.UpdateAsync(
            _clientId, "2099-01-01T00-00-00Z",
            soapNoteUpdate:  null,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        null,
            icdCodes:        null);

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task UpdateAsync_UpdatesSoapNote()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-01T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey, subjective: "Original subjective."));

        var updatedNote = new SoapNoteUpdate(
            Subjective: "Updated subjective.",
            Objective:  "ROM measured at 90 degrees.",
            Assessment: "Progressing well.",
            Plan:       "Continue current plan."
        );

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  updatedNote,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        null,
            icdCodes:        null);

        result.Should().NotBeNull();
        result!.SoapNote.Subjective.Should().Be("Updated subjective.");
    }

    [SkippableFact]
    public async Task UpdateAsync_PreservesUnchangedFields_WhenOnlySoapNoteUpdated()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-02T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey, therapist: "Dr. Smith", payer: "Medicare", duration: 45));

        var updatedNote = new SoapNoteUpdate("New S.", "New O.", "New A.", "New P.");

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  updatedNote,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        null,
            icdCodes:        null);

        result.Should().NotBeNull();
        result!.TherapistName.Should().Be("Dr. Smith");
        result.Payer.Should().Be("Medicare");
        result.SessionDurationMinutes.Should().Be(45);
    }

    [SkippableFact]
    public async Task UpdateAsync_UpdatesCptCodes()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-03T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey));

        var newCptCodes = new List<CptCode>
        {
            new("97530", "Therapeutic Activities", "Functional task training", 2),
            new("97110", "Therapeutic Exercise",   "Strengthening program",    3)
        };

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  null,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        newCptCodes,
            icdCodes:        null);

        result.Should().NotBeNull();
        result!.SuggestedCptCodes.Should().HaveCount(2);
        result.SuggestedCptCodes[0].Code.Should().Be("97530");
        result.SuggestedCptCodes[1].Code.Should().Be("97110");
    }

    [SkippableFact]
    public async Task UpdateAsync_UpdatesIcdCodes()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-04T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey));

        var newIcdCodes = new List<IcdCode>
        {
            new("M79.3", "Panniculitis", "Secondary diagnosis"),
            new("Z96.641", "Hip replacement status", "Surgical history")
        };

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  null,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        null,
            icdCodes:        newIcdCodes);

        result.Should().NotBeNull();
        result!.SuggestedIcdCodes.Should().HaveCount(2);
        result.SuggestedIcdCodes[0].Code.Should().Be("M79.3");
        result.SuggestedIcdCodes[1].Code.Should().Be("Z96.641");
    }

    [SkippableFact]
    public async Task UpdateAsync_PersistedDocument_ReflectsChanges_OnSubsequentRead()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-05T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey, subjective: "Original."));

        var updatedNote = new SoapNoteUpdate("Corrected by therapist.", "O.", "A.", "P.");
        await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  updatedNote,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        null,
            icdCodes:        null);

        // Independent read-back confirms the document was persisted
        var readBack = await repo.GetByClientIdAndDateAsync(_clientId, rowKey);
        readBack.Should().NotBeNull();
        readBack!.SoapNote.Subjective.Should().Be("Corrected by therapist.");
    }

    [SkippableFact]
    public async Task UpdateAsync_WithRedactionMap_RestoresPiiInResponse()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-06T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey));

        // Simulate re-redacted note with placeholders + updated map
        var redactedNote = new SoapNoteUpdate(
            Subjective: "[PII_1] reports reduced pain.",
            Objective:  "Grip strength measured.",
            Assessment: "Good progress.",
            Plan:       "Continue HEP."
        );
        var newMap = new Dictionary<string, string> { ["[PII_1]"] = "Jane Smith" };

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  redactedNote,
            newRedactionMap: newMap,
            cptCodes:        null,
            icdCodes:        null);

        // MapToResponse should decrypt and restore PII before returning
        result.Should().NotBeNull();
        result!.SoapNote.Subjective.Should().Be("Jane Smith reports reduced pain.");
    }

    [SkippableFact]
    public async Task UpdateAsync_CodesOnly_DoesNotChangeSoapNote()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-07T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey, subjective: "Original SOAP."));

        var newCptCodes = new List<CptCode> { new("97150", "Group Therapy", "Peer support group", 1) };

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  null,  // no SOAP change
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        newCptCodes,
            icdCodes:        null);

        result.Should().NotBeNull();
        result!.SoapNote.Subjective.Should().Be("Original SOAP.");
        result.SuggestedCptCodes.Should().HaveCount(1);
        result.SuggestedCptCodes[0].Code.Should().Be("97150");
    }

    [SkippableFact]
    public async Task UpdateAsync_PartialSoapNoteUpdate_PreservesOmittedFields()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var rowKey = "2025-09-08T10-00-00Z";

        await repo.SaveAsync(BuildRecord(rowKey,
            subjective: "Original S.",
            objective:  "Original O.",
            assessment: "Original A.",
            plan:       "Original P."));

        // Only update subjective and plan; leave objective and assessment null (omitted).
        var partialUpdate = new SoapNoteUpdate(
            Subjective: "Updated S.",
            Objective:  null,
            Assessment: null,
            Plan:       "Updated P."
        );

        var result = await repo.UpdateAsync(
            _clientId, rowKey,
            soapNoteUpdate:  partialUpdate,
            newRedactionMap: new Dictionary<string, string>(),
            cptCodes:        null,
            icdCodes:        null);

        result.Should().NotBeNull();
        result!.SoapNote.Subjective.Should().Be("Updated S.",   "provided field must be updated");
        result.SoapNote.Plan.Should().Be("Updated P.",          "provided field must be updated");
        result.SoapNote.Objective.Should().Be("Original O.",   "omitted field must be preserved");
        result.SoapNote.Assessment.Should().Be("Original A.",  "omitted field must be preserved");
    }

    // -- GetTherapistStatsAsync ------------------------------------------------

    [SkippableFact]
    public async Task GetTherapistStatsAsync_ReturnsZeroedStats_WhenNoSessionsExist()
    {
        cosmos.SkipIfUnavailable();
        var repo   = CreateRepository();
        var stats  = await repo.GetTherapistStatsAsync($"Dr. Nobody-{Guid.NewGuid():N}");

        stats.TotalSessions.Should().Be(0);
        stats.TotalClients.Should().Be(0);
        stats.TotalBillableUnits.Should().Be(0);
        stats.SessionsByDiscipline.Should().BeEmpty();
        stats.TopCptCodes.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task GetTherapistStatsAsync_CountsTotalSessionsAndClients()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var therapist = $"Dr. Stats-{Guid.NewGuid():N}";

        // Two sessions for the same client, one for a different client
        var clientA = $"stats-client-a-{Guid.NewGuid():N}";
        var clientB = $"stats-client-b-{Guid.NewGuid():N}";

        await repo.SaveAsync(BuildRecord("2025-10-01T10-00-00Z", therapist: therapist, clientId: clientA));
        await repo.SaveAsync(BuildRecord("2025-10-02T10-00-00Z", therapist: therapist, clientId: clientA));
        await repo.SaveAsync(BuildRecord("2025-10-03T10-00-00Z", therapist: therapist, clientId: clientB));

        var stats = await repo.GetTherapistStatsAsync(therapist);

        stats.TotalSessions.Should().Be(3);
        stats.TotalClients.Should().Be(2);
    }

    [SkippableFact]
    public async Task GetTherapistStatsAsync_BreaksDownSessionsByDiscipline()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var therapist = $"Dr. Discipline-{Guid.NewGuid():N}";

        await repo.SaveAsync(BuildRecord("2025-10-04T10-00-00Z", therapist: therapist, discipline: "PT"));
        await repo.SaveAsync(BuildRecord("2025-10-05T10-00-00Z", therapist: therapist, discipline: "OT"));
        await repo.SaveAsync(BuildRecord("2025-10-06T10-00-00Z", therapist: therapist, discipline: "PT"));

        var stats = await repo.GetTherapistStatsAsync(therapist);

        stats.SessionsByDiscipline.Should().ContainKey("PT").WhoseValue.Should().Be(2);
        stats.SessionsByDiscipline.Should().ContainKey("OT").WhoseValue.Should().Be(1);
    }

    [SkippableFact]
    public async Task GetTherapistStatsAsync_BreaksDownSessionsByPayer()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var therapist = $"Dr. Payer-{Guid.NewGuid():N}";

        await repo.SaveAsync(BuildRecord("2025-10-07T10-00-00Z", therapist: therapist, payer: "Medicare"));
        await repo.SaveAsync(BuildRecord("2025-10-08T10-00-00Z", therapist: therapist, payer: "Medicaid"));
        await repo.SaveAsync(BuildRecord("2025-10-09T10-00-00Z", therapist: therapist, payer: "Medicare"));

        var stats = await repo.GetTherapistStatsAsync(therapist);

        stats.SessionsByPayer.Should().ContainKey("Medicare").WhoseValue.Should().Be(2);
        stats.SessionsByPayer.Should().ContainKey("Medicaid").WhoseValue.Should().Be(1);
    }

    [SkippableFact]
    public async Task GetTherapistStatsAsync_SumsAverageSessionDuration()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var therapist = $"Dr. Duration-{Guid.NewGuid():N}";

        await repo.SaveAsync(BuildRecord("2025-10-10T10-00-00Z", therapist: therapist, duration: 30));
        await repo.SaveAsync(BuildRecord("2025-10-11T10-00-00Z", therapist: therapist, duration: 60));

        var stats = await repo.GetTherapistStatsAsync(therapist);

        stats.AverageSessionDurationMinutes.Should().Be(45.0);
    }

    [SkippableFact]
    public async Task GetTherapistStatsAsync_SumsTotalBillableUnits()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var therapist = $"Dr. Units-{Guid.NewGuid():N}";

        // Each BuildRecord includes 1 CPT code with BillableUnits = 1
        await repo.SaveAsync(BuildRecord("2025-10-12T10-00-00Z", therapist: therapist));
        await repo.SaveAsync(BuildRecord("2025-10-13T10-00-00Z", therapist: therapist));

        var stats = await repo.GetTherapistStatsAsync(therapist);

        stats.TotalBillableUnits.Should().Be(2);
    }

    [SkippableFact]
    public async Task GetTherapistStatsAsync_AggregatesTopCptCodes()
    {
        cosmos.SkipIfUnavailable();
        var repo     = CreateRepository();
        var therapist = $"Dr. Cpt-{Guid.NewGuid():N}";

        await repo.SaveAsync(BuildRecord("2025-10-14T10-00-00Z", therapist: therapist));
        await repo.SaveAsync(BuildRecord("2025-10-15T10-00-00Z", therapist: therapist));

        var stats = await repo.GetTherapistStatsAsync(therapist);

        stats.TopCptCodes.Should().NotBeEmpty();
        stats.TopCptCodes[0].Code.Should().Be("97110"); // Both BuildRecord sessions use 97110
        stats.TopCptCodes[0].Count.Should().Be(2);
    }

    // -- GetClientStatsAsync ---------------------------------------------------

    [SkippableFact]
    public async Task GetClientStatsAsync_ReturnsZeroedStats_WhenNoSessionsExist()
    {
        cosmos.SkipIfUnavailable();
        var repo  = CreateRepository();
        var stats = await repo.GetClientStatsAsync($"unknown-client-{Guid.NewGuid():N}");

        stats.TotalSessions.Should().Be(0);
        stats.TotalBillableUnits.Should().Be(0);
        stats.FirstSessionDate.Should().BeNull();
        stats.LastSessionDate.Should().BeNull();
        stats.SessionsByTherapist.Should().BeEmpty();
        stats.TopCptCodes.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task GetClientStatsAsync_CountsTotalSessions()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-11-01T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-11-02T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-11-03T10-00-00Z"));

        var stats = await repo.GetClientStatsAsync(_clientId);

        stats.TotalSessions.Should().Be(3);
        stats.ClientId.Should().Be(_clientId);
    }

    [SkippableFact]
    public async Task GetClientStatsAsync_TracksFirstAndLastSessionDate()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-11-04T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-11-06T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-11-05T10-00-00Z"));

        var stats = await repo.GetClientStatsAsync(_clientId);

        stats.FirstSessionDate.Should().NotBeNull();
        stats.LastSessionDate.Should().NotBeNull();
        stats.FirstSessionDate!.Value.Date.Should().Be(new DateTime(2025, 11, 4));
        stats.LastSessionDate!.Value.Date.Should().Be(new DateTime(2025, 11, 6));
    }

    [SkippableFact]
    public async Task GetClientStatsAsync_BreaksDownSessionsByTherapist()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-11-07T10-00-00Z", therapist: "Dr. Smith"));
        await repo.SaveAsync(BuildRecord("2025-11-08T10-00-00Z", therapist: "Dr. Jones"));
        await repo.SaveAsync(BuildRecord("2025-11-09T10-00-00Z", therapist: "Dr. Smith"));

        var stats = await repo.GetClientStatsAsync(_clientId);

        stats.SessionsByTherapist.Should().ContainKey("Dr. Smith").WhoseValue.Should().Be(2);
        stats.SessionsByTherapist.Should().ContainKey("Dr. Jones").WhoseValue.Should().Be(1);
    }

    [SkippableFact]
    public async Task GetClientStatsAsync_SumsAverageSessionDuration()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-11-10T10-00-00Z", duration: 30));
        await repo.SaveAsync(BuildRecord("2025-11-11T10-00-00Z", duration: 60));
        await repo.SaveAsync(BuildRecord("2025-11-12T10-00-00Z", duration: 45));

        var stats = await repo.GetClientStatsAsync(_clientId);

        stats.AverageSessionDurationMinutes.Should().Be(45.0);
    }

    [SkippableFact]
    public async Task GetClientStatsAsync_AggregatesTopIcdCodes()
    {
        cosmos.SkipIfUnavailable();
        var repo = CreateRepository();

        await repo.SaveAsync(BuildRecord("2025-11-13T10-00-00Z"));
        await repo.SaveAsync(BuildRecord("2025-11-14T10-00-00Z"));

        var stats = await repo.GetClientStatsAsync(_clientId);

        stats.TopIcdCodes.Should().NotBeEmpty();
        stats.TopIcdCodes[0].Code.Should().Be("M54.5"); // Both BuildRecord sessions use M54.5
        stats.TopIcdCodes[0].Count.Should().Be(2);
    }
}
