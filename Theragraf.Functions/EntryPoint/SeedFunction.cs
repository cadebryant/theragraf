namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Bogus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;

/// <summary>
/// Demo data management endpoints.
/// All operations are gated by <c>Demo:TherapistName</c> being non-empty in configuration.
/// Set that value to enable seeding; leave it blank (default) to disable in production.
/// </summary>
public class SeedFunction(
    ISessionRepository repository,
    IConfiguration     config,
    ILoggerFactory     loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SeedFunction>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    // ── CPT code pool ─────────────────────────────────────────────────────────

    private static readonly (string Code, string Description, bool IsTimed)[] CptPool =
    [
        ("97110", "Therapeutic exercises",                            true),
        ("97112", "Neuromuscular re-education",                       true),
        ("97116", "Gait training",                                    true),
        ("97140", "Manual therapy techniques",                        true),
        ("97150", "Therapeutic exercises – group",                    false),
        ("97530", "Therapeutic activities",                           true),
        ("97535", "Self-care / home management training",             false),
        ("97760", "Orthotic management and training – initial",       false),
        ("97010", "Application of a modality – hot/cold packs",       false),
        ("90791", "Psychiatric diagnostic evaluation",                false),
        ("97165", "Occupational therapy evaluation – low complexity", false),
    ];

    // ── ICD-10 code pool ──────────────────────────────────────────────────────

    private static readonly (string Code, string Description)[] IcdPool =
    [
        ("M54.5",  "Low back pain"),
        ("M79.3",  "Panniculitis"),
        ("S93.401A","Sprain of unspecified ligament of right ankle, initial encounter"),
        ("G80.1",  "Spastic diplegic cerebral palsy"),
        ("F84.0",  "Autistic disorder"),
        ("M47.816","Spondylosis without myelopathy or radiculopathy, lumbar region"),
        ("I69.351","Hemiplegia and hemiparesis following cerebral infarction affecting right dominant side"),
        ("F41.1",  "Generalized anxiety disorder"),
        ("M25.511","Pain in right shoulder"),
        ("R26.89", "Other abnormalities of gait and mobility"),
        ("Z96.641","Presence of right artificial knee joint"),
        ("S82.001A","Fracture of unspecified part of right patella, initial encounter"),
    ];

    // ── Realistic SOAP sentence fragments ────────────────────────────────────

    private static readonly string[] SubjectivePhrases =
    [
        "Patient reports moderate pain (4/10) in the affected region, improved from last visit.",
        "Patient denies new complaints. Reports difficulty with ADL tasks requiring overhead reach.",
        "Patient notes increased fatigue during functional mobility tasks.",
        "Patient reports compliance with home exercise program (HEP) 5 of 7 days.",
        "Patient expresses motivation to return to prior level of function.",
    ];

    private static readonly string[] ObjectivePhrases =
    [
        "ROM measured: shoulder flexion 140°, abduction 120°, ER 45°. Strength 4/5 throughout.",
        "Patient ambulates 50 ft with standard walker, supervision required. Gait steady.",
        "MMT: hip flexion 4-/5, knee extension 4/5, dorsiflexion 3+/5 bilaterally.",
        "Balance assessed: single-leg stance 8 sec right, 6 sec left. Tinetti score 22/28.",
        "Fine motor coordination: nine-hole peg test 32 sec dominant hand (normative: 18 sec).",
    ];

    private static readonly string[] AssessmentPhrases =
    [
        "Patient is progressing toward functional goals. Pain limiting participation in higher-level tasks.",
        "Patient demonstrates improved strength and endurance. Continues to require moderate assist for transfers.",
        "Goals partially met. Patient requires cueing for safety during dynamic balance tasks.",
        "Patient tolerating skilled therapy well. Short-term goals expected to be met within 2 weeks.",
        "Functional deficits persist but trajectory is positive. LTG on track for 6-week milestone.",
    ];

    private static readonly string[] PlanPhrases =
    [
        "Continue skilled PT 3× week. Progress HEP to include resistance band exercises.",
        "Advance gait training to outdoor surfaces and stair navigation next session.",
        "Initiate IADL training; coordinate with OT for ADL reintegration goals.",
        "Schedule re-evaluation in 2 weeks. Discuss potential discharge planning with care team.",
        "Focus next session on dynamic standing tolerance and upper-extremity weight-bearing.",
    ];

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/seed?count=N
    /// Generates N fake session records (default 50, max 200) stored under the demo therapist name.
    /// Requires <c>Demo:TherapistName</c> to be set in configuration.
    /// </summary>
    [Function("SeedData")]
    public async Task<HttpResponseData> Seed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var demoTherapist = config["Demo:TherapistName"];
        if (string.IsNullOrWhiteSpace(demoTherapist))
        {
            var disabled = req.CreateResponse(HttpStatusCode.Forbidden);
            await disabled.WriteStringAsync("Demo seeding is not enabled on this deployment.", cancellationToken);
            return disabled;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        if (!int.TryParse(query["count"], out var count) || count < 1)
            count = 50;
        if (count > 200) count = 200;

        var records = BuildFakeRecords(demoTherapist, count);

        var tasks = records.Select(r => repository.SaveAsync(r, cancellationToken));
        await Task.WhenAll(tasks);

        _logger.LogInformation("Seeded {Count} demo records for therapist '{TherapistName}'", count, demoTherapist);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(
            JsonSerializer.Serialize(new { seeded = count, therapistName = demoTherapist }, JsonOptions),
            cancellationToken);
        return ok;
    }

    /// <summary>
    /// DELETE /api/seed
    /// Deletes all demo session records by iterating each known demo client's sessions.
    /// Requires <c>Demo:TherapistName</c> to be set in configuration.
    /// </summary>
    [Function("DeleteSeedData")]
    public async Task<HttpResponseData> DeleteSeed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "seed")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var demoTherapist = config["Demo:TherapistName"];
        if (string.IsNullOrWhiteSpace(demoTherapist))
        {
            var disabled = req.CreateResponse(HttpStatusCode.Forbidden);
            await disabled.WriteStringAsync("Demo seeding is not enabled on this deployment.", cancellationToken);
            return disabled;
        }

        // Pull the caseload for the demo therapist, then delete every session for each client.
        var caseload  = await repository.GetCaseloadAsync(demoTherapist, cancellationToken);
        int deleted   = 0;

        foreach (var client in caseload.Clients)
        {
            string? token = null;
            do
            {
                var page = await repository.GetByClientIdPagedAsync(
                    client.ClientId, 100, token,
                    new SessionQueryOptions(Therapist: demoTherapist),
                    cancellationToken);

                foreach (var session in page.Items)
                {
                    if (await repository.DeleteAsync(client.ClientId, session.SessionDate, cancellationToken))
                        deleted++;
                }

                token = page.HasMore ? page.ContinuationToken : null;
            }
            while (token is not null);
        }

        _logger.LogInformation("Deleted {Count} demo records for therapist '{TherapistName}'", deleted, demoTherapist);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(
            JsonSerializer.Serialize(new { deleted, therapistName = demoTherapist }, JsonOptions),
            cancellationToken);
        return ok;
    }

    // ── Fake record generation ─────────────────────────────────────────────────

    private static IReadOnlyList<SessionRecord> BuildFakeRecords(string therapistName, int count)
    {
        var random = new Randomizer();

        // Fixed pool of demo clients so the caseload looks like a real practice.
        var clientIds = new[]
        {
            "client-alice-morgan",   "client-ben-okafor",    "client-cara-santos",
            "client-david-chen",     "client-emma-wright",   "client-felix-nowak",
            "client-grace-patel",    "client-hiro-tanaka",   "client-isla-berg",
            "client-jasper-diallo",
        };

        var disciplines = Enum.GetValues<ClinicalSpecialty>();
        var settings    = Enum.GetValues<ClinicalSetting>();
        var payers      = Enum.GetValues<PayerType>();

        var faker = new Faker<SessionRecord>()
            .RuleFor(r => r.TherapistName,          _ => therapistName)
            .RuleFor(r => r.PartitionKey,            f => f.PickRandom(clientIds))
            .RuleFor(r => r.Discipline,              f => f.PickRandom(disciplines).ToString())
            .RuleFor(r => r.Setting,                 f => f.PickRandom(settings).ToString())
            .RuleFor(r => r.Payer,                   f => f.PickRandom(payers).ToString())
            .RuleFor(r => r.SessionDurationMinutes,  f => f.PickRandom(30, 38, 45, 53, 60, 68, 90))
            .RuleFor(r => r.CreatedAt,               f => f.Date.RecentOffset(365).ToUniversalTime())
            .RuleFor(r => r.SoapNoteJson,            f => JsonSerializer.Serialize(new SoapNote(
                Subjective: f.PickRandom(SubjectivePhrases),
                Objective:  f.PickRandom(ObjectivePhrases),
                Assessment: f.PickRandom(AssessmentPhrases),
                Plan:       f.PickRandom(PlanPhrases)
            )))
            .RuleFor(r => r.CptCodesJson, f =>
            {
                var chosen = f.PickRandom(CptPool, f.Random.Int(1, 3)).ToList();
                var codes  = chosen.Select(c => new CptCode(
                    c.Code, c.Description,
                    Rationale:    $"Selected based on documented skilled intervention.",
                    BillableUnits: c.IsTimed ? f.Random.Int(1, 4) : 1
                )).ToList();
                return JsonSerializer.Serialize(codes);
            })
            .RuleFor(r => r.IcdCodesJson, f =>
            {
                var chosen = f.PickRandom(IcdPool, f.Random.Int(1, 3)).ToList();
                var codes  = chosen.Select(c => new IcdCode(
                    c.Code, c.Description,
                    Rationale: $"Diagnosis supported by clinical presentation documented in session."
                )).ToList();
                return JsonSerializer.Serialize(codes);
            })
            .RuleFor(r => r.RedactionMapJson, _ => "{}")
            .RuleFor(r => r.IsSynthetic, _ => true)
            .FinishWith((f, r) =>
            {
                // RowKey is derived from CreatedAt — unique per client per minute.
                r.RowKey = r.CreatedAt.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ");
            });

        // Generate with deduplication: one session per (client, date) pair.
        var seen    = new HashSet<string>();
        var results = new List<SessionRecord>(count);
        var attempts = 0;

        while (results.Count < count && attempts < count * 10)
        {
            attempts++;
            var record = faker.Generate();
            var key    = $"{record.PartitionKey}|{record.RowKey}";
            if (seen.Add(key))
                results.Add(record);
        }

        return results;
    }
}
