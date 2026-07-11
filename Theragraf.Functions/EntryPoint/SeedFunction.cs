namespace Theragraf.Functions.EntryPoint;

using System.Net;
using System.Text.Json;
using Bogus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Models;
using Theragraf.Core.Services;
using Theragraf.Functions.Helpers;
using Theragraf.Functions.Logging;
using CosmosDatabase = Microsoft.Azure.Cosmos.Database;

/// <summary>
/// Demo data management endpoints.
/// All operations are gated by <c>Demo:TherapistName</c> being non-empty in configuration.
/// Set that value to enable seeding; leave it blank (default) to disable in production.
/// </summary>
public class SeedFunction(
    ISessionRepository  sessionRepository,
    IClientRepository   clientRepository,
    IGoalRepository     goalRepository,
    CosmosClient        cosmosClient,
    IConfiguration      config,
    ILoggerFactory      loggerFactory,
    IAuditLogger        auditLogger)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SeedFunction>();
    private static readonly JsonSerializerOptions JsonOptions = JsonConfig.Web;

    // ── Fixed therapist / provider identities ─────────────────────────────────

    private const string TherapistOtId = "seed-therapist-ot-001";
    private const string TherapistPtId = "seed-therapist-pt-001";
    private const string ProviderId    = "seed-provider-riverstone-001";

    // ── CPT code pool ─────────────────────────────────────────────────────────

    private static readonly (string Code, string Description, bool IsTimed, ClinicalSpecialty Specialty)[] CptPool =
    [
        ("97110", "Therapeutic exercises",                              true,  ClinicalSpecialty.PT),
        ("97112", "Neuromuscular re-education",                         true,  ClinicalSpecialty.PT),
        ("97116", "Gait training",                                      true,  ClinicalSpecialty.PT),
        ("97140", "Manual therapy techniques",                          true,  ClinicalSpecialty.PT),
        ("97150", "Therapeutic exercises – group",                      false, ClinicalSpecialty.PT),
        ("97010", "Application of a modality – hot/cold packs",         false, ClinicalSpecialty.PT),
        ("97530", "Therapeutic activities",                             true,  ClinicalSpecialty.OT),
        ("97535", "Self-care / home management training",               false, ClinicalSpecialty.OT),
        ("97760", "Orthotic management and training – initial",         false, ClinicalSpecialty.OT),
        ("97165", "Occupational therapy evaluation – low complexity",   false, ClinicalSpecialty.OT),
        ("92507", "Speech/language treatment",                          true,  ClinicalSpecialty.SLP),
        ("92526", "Treatment of swallowing dysfunction",                true,  ClinicalSpecialty.SLP),
        ("90791", "Psychiatric diagnostic evaluation",                  false, ClinicalSpecialty.Psych),
        ("90837", "Psychotherapy, 60 min",                              false, ClinicalSpecialty.Psych),
    ];

    // ── ICD-10 code pool ──────────────────────────────────────────────────────

    private static readonly (string Code, string Description, ClinicalSpecialty Specialty)[] IcdPool =
    [
        ("M54.5",   "Low back pain",                                                                      ClinicalSpecialty.PT),
        ("M25.511", "Pain in right shoulder",                                                             ClinicalSpecialty.PT),
        ("S93.401A","Sprain of unspecified ligament of right ankle, initial encounter",                   ClinicalSpecialty.PT),
        ("M47.816", "Spondylosis without myelopathy or radiculopathy, lumbar region",                    ClinicalSpecialty.PT),
        ("I69.351", "Hemiplegia following cerebral infarction affecting right dominant side",             ClinicalSpecialty.PT),
        ("Z96.641", "Presence of right artificial knee joint",                                            ClinicalSpecialty.PT),
        ("G80.1",   "Spastic diplegic cerebral palsy",                                                    ClinicalSpecialty.OT),
        ("F84.0",   "Autistic disorder",                                                                  ClinicalSpecialty.OT),
        ("R26.89",  "Other abnormalities of gait and mobility",                                          ClinicalSpecialty.OT),
        ("M79.3",   "Panniculitis",                                                                       ClinicalSpecialty.OT),
        ("F80.2",   "Mixed receptive-expressive language disorder",                                       ClinicalSpecialty.SLP),
        ("R13.10",  "Dysphagia, unspecified",                                                             ClinicalSpecialty.SLP),
        ("R47.89",  "Other speech disturbances",                                                          ClinicalSpecialty.SLP),
        ("F41.1",   "Generalized anxiety disorder",                                                       ClinicalSpecialty.Psych),
        ("F32.1",   "Major depressive disorder, single episode, moderate",                               ClinicalSpecialty.Psych),
        ("F43.10",  "Post-traumatic stress disorder, unspecified",                                        ClinicalSpecialty.Psych),
    ];

    // ── SOAP note fragments (by specialty) ───────────────────────────────────

    private static readonly Dictionary<ClinicalSpecialty, (string[] S, string[] O, string[] A, string[] P)> SoapFragments = new()
    {
        [ClinicalSpecialty.PT] = (
            S: ["Patient reports moderate pain (4/10) at the affected region, improved since last visit.",
                "Patient denies new complaints. Notes difficulty with overhead reaching during ADL tasks.",
                "Patient reports compliance with HEP 5 of 7 days. Describes soreness after exercise.",
                "Patient states fatigue limits tolerance for prolonged standing. No new injuries reported.",
                "Patient expresses motivation to return to prior level of community ambulation."],
            O: ["ROM: shoulder flexion 140°, abduction 120°, ER 45°. Strength 4/5 throughout UE.",
                "Ambulates 50 ft with standard walker, supervision required. Gait pattern steady.",
                "MMT: hip flexion 4-/5, knee extension 4/5, dorsiflexion 3+/5 bilaterally.",
                "Balance: single-leg stance 8 s right / 6 s left. Tinetti score 22/28.",
                "Step length symmetrical. Trendelenburg sign absent. Patellar tracking within normal limits."],
            A: ["Patient progressing toward functional goals. Pain limiting participation in higher-level tasks.",
                "Strength and endurance improved. Continues to require moderate assist for transfers.",
                "Goals partially met. Patient requires cueing for safety during dynamic balance tasks.",
                "Tolerating skilled PT well. Short-term goals expected to be met within 2 weeks.",
                "Functional deficits persist but trajectory is positive. LTG on track for 6-week milestone."],
            P: ["Continue skilled PT 3× week. Progress HEP to include resistance band exercises.",
                "Advance gait training to outdoor surfaces and stair navigation next session.",
                "Schedule re-evaluation in 2 weeks. Discuss potential discharge planning with care team.",
                "Focus on dynamic standing tolerance and UE weight-bearing next session.",
                "Initiate eccentric strengthening protocol. Reassess pain scale at start of next visit."]
        ),
        [ClinicalSpecialty.OT] = (
            S: ["Patient reports difficulty donning shirt independently due to limited shoulder ROM.",
                "Caregiver reports patient is requiring increased assistance with meal preparation.",
                "Patient notes hand fatigue during fine motor tasks lasting more than 10 minutes.",
                "Patient states improved confidence with transfers since last session.",
                "Patient reports participating in leisure activity (painting) with modified equipment."],
            O: ["Fine motor: nine-hole peg test 32 s dominant hand (norm: 18 s). Pinch grip 6 lb.",
                "ADL assessment: upper body dressing with minimal assist, lower body with moderate assist.",
                "Sensory testing intact for light touch and proprioception bilateral UE.",
                "Coordination: finger opposition WNL; rapid alternating movements mildly impaired left.",
                "Standardized AMPS performed; motor score 1.1 (indicating difficulty with skilled tasks)."],
            A: ["Patient demonstrates improved engagement in ADL tasks. Fine motor deficits persist.",
                "Functional gains noted in meal prep and grooming. Continue current plan of care.",
                "Patient limited by pain and reduced grip strength. Adaptive equipment trial recommended.",
                "Goals partially met; splinting program initiated to address resting hand position.",
                "Patient tolerating OT well. Generalization of strategies to home environment is goal."],
            P: ["Continue OT 2× week. Introduce adaptive equipment for kitchen safety tasks.",
                "Progress to bilateral UE coordination activities and IADL re-integration.",
                "Provide written HEP for fine motor exercises. Reassess grip strength at next visit.",
                "Trial dynamic wrist splint for typing activities. Educate on joint protection principles.",
                "Coordinate with PT for transfer training. Address lower-body dressing next session."]
        ),
        [ClinicalSpecialty.SLP] = (
            S: ["Patient reports difficulty swallowing solid foods since last visit.",
                "Patient's spouse notes improved word-finding in conversational speech at home.",
                "Patient describes frustration with word retrieval in high-distraction environments.",
                "Patient reports compliance with oral motor exercises 6 of 7 days.",
                "Patient expresses increased confidence when speaking with unfamiliar listeners."],
            O: ["Modified barium swallow: silent aspiration observed with thin liquids; safe with nectar-thick.",
                "Standardized aphasia battery: naming 68%, repetition 72%, comprehension 80%.",
                "Conversational sample: 4.2 utterances/minute; semantic paraphasia rate 12%.",
                "Oral motor: lip closure intact; tongue lateralization mildly reduced. Palatal elevation WNL.",
                "Ranchos Los Amigos Level V — confused but appropriate. Follows 2-step commands."],
            A: ["Dysphagia management progressing. Modified texture diet maintained for safety.",
                "Expressive aphasia improving; word-finding strategies effective in structured tasks.",
                "Patient demonstrates improved phrase length and reduced communication breakdowns.",
                "Goals partially met; receptive language continues to lag expressive improvement.",
                "Patient tolerating SLP well. Functional communication improving in daily interactions."],
            P: ["Continue SLP 3× week. Advance diet texture per swallowing reassessment results.",
                "Progress to less structured naming tasks and multistep discourse production.",
                "Provide AAC trial for high-demand situations; caregiver training scheduled.",
                "Reassess modified diet level next visit. Educate family on safe feeding strategies.",
                "Focus on pragmatic language skills and real-world conversation practice."]
        ),
        [ClinicalSpecialty.Psych] = (
            S: ["Patient reports sleep disturbance 4 of 7 nights. Endorses moderate anxiety (GAD-7: 12).",
                "Patient denies current suicidal ideation. Reports improved mood since medication adjustment.",
                "Patient describes avoidance behaviors related to crowded places. PHQ-9 score: 14.",
                "Patient identifies work stress as primary trigger. Engaged in session and motivated.",
                "Patient reports practicing diaphragmatic breathing daily. Notes reduced resting anxiety."],
            O: ["Mental status: alert, oriented × 4. Affect congruent with reported mood. Speech fluent.",
                "Thought process linear and goal-directed. No evidence of psychosis or delusional ideation.",
                "Patient demonstrates understanding of cognitive distortions and can identify own patterns.",
                "Administered PHQ-9: score 14 (moderate depression). GAD-7: score 10 (moderate anxiety).",
                "Patient engaged in session. Eye contact appropriate. No behavioral safety concerns."],
            A: ["Patient making progress with CBT techniques. Anxiety symptoms partially reduced.",
                "Therapeutic alliance strong. Patient showing improved insight into emotional triggers.",
                "PHQ-9 trending downward from baseline. Continue current therapeutic approach.",
                "Avoidance behaviors persist; graded exposure hierarchy in development.",
                "Patient tolerating psychotherapy well. Goals on track for 12-session treatment plan."],
            P: ["Continue weekly psychotherapy. Introduce behavioral activation component next session.",
                "Begin graded exposure hierarchy for identified avoidance behaviors.",
                "Assign thought record homework. Review at next session. Reassess PHQ-9 in 4 weeks.",
                "Coordinate with prescribing physician regarding medication efficacy. Continue CBT.",
                "Focus next session on interpersonal effectiveness skills and boundary-setting."]
        ),
    };

    // ── Goal templates by specialty ───────────────────────────────────────────

    private static readonly Dictionary<ClinicalSpecialty, (string Title, string Description)[]> GoalTemplates = new()
    {
        [ClinicalSpecialty.PT] =
        [
            ("Improve ambulation endurance",         "Patient will ambulate 150 ft on level surfaces with no assistive device and without rest breaks."),
            ("Restore shoulder ROM",                  "Patient will achieve shoulder flexion ≥ 160° and abduction ≥ 150° to perform overhead ADL tasks independently."),
            ("Increase lower extremity strength",     "Patient will demonstrate 4+/5 MMT strength in hip flexors and knee extensors bilaterally for safe stair negotiation."),
            ("Improve dynamic balance",               "Patient will achieve a Tinetti score ≥ 24/28 to reduce fall risk during community ambulation."),
            ("Return to prior stair negotiation",     "Patient will ascend/descend 12 steps using handrail with supervision only, no step-to pattern."),
            ("Reduce pain with functional activity",  "Patient will report pain ≤ 2/10 during ADL tasks requiring prolonged standing > 20 minutes."),
        ],
        [ClinicalSpecialty.OT] =
        [
            ("Independent upper body dressing",       "Patient will don/doff shirt, bra, and jacket independently using adaptive techniques within 10 minutes."),
            ("Improve fine motor coordination",       "Patient will complete nine-hole peg test in ≤ 22 s dominant hand to support return to keyboard work."),
            ("Safe meal preparation",                 "Patient will prepare a simple 3-step meal independently using adaptive equipment with no safety hazards."),
            ("Return to community IADL",              "Patient will complete a simulated grocery shopping task independently including money management."),
            ("Reduce ADL dependence",                 "Patient will require no more than supervision for all morning self-care tasks (grooming, hygiene, dressing)."),
            ("Hand strengthening for functional grip","Patient will achieve dominant-hand grip strength ≥ 18 lb to manage household objects safely."),
        ],
        [ClinicalSpecialty.SLP] =
        [
            ("Improve functional communication",      "Patient will initiate and maintain a 10-turn conversation on a familiar topic with ≤ 2 communication breakdowns."),
            ("Safe oral intake – nectar thick",       "Patient will safely consume nectar-thick liquids with no overt signs of aspiration per clinical swallowing exam."),
            ("Word-finding accuracy",                 "Patient will name 80% of pictured objects from a 20-item set within 10 s per item with no cues."),
            ("Improve phrase length",                 "Patient will produce 5-word utterances to express basic needs in structured clinical tasks."),
            ("Caregiver-supported communication",     "Caregiver will accurately implement 3 communication support strategies as observed in a 15-minute interaction."),
        ],
        [ClinicalSpecialty.Psych] =
        [
            ("Reduce anxiety symptoms",               "Patient will score ≤ 8 on GAD-7 at 4-week reassessment, consistent with mild anxiety severity."),
            ("Improve sleep hygiene",                 "Patient will report ≥ 6 hours uninterrupted sleep on ≥ 5 of 7 nights without PRN sleep medication."),
            ("Develop coping skill repertoire",       "Patient will identify and demonstrate 3 adaptive coping strategies to apply when anxiety escalates above 6/10."),
            ("Reduce PHQ-9 score",                    "Patient will score ≤ 9 on PHQ-9 at 6-week reassessment, indicating minimal depressive symptoms."),
            ("Decrease avoidance behaviors",          "Patient will complete 2 graded exposure tasks per week without retreat, per self-monitored log."),
        ],
    };

    // ── Progress note fragments ───────────────────────────────────────────────

    private static readonly string[] ProgressNoteFragments =
    [
        "Patient demonstrated improved consistency with this goal during today's session.",
        "Goal partially met; deficits persist in high-demand or novel contexts.",
        "Patient required minimal cuing to achieve target behavior during structured tasks.",
        "Carryover to naturalistic settings noted by caregiver report.",
        "Patient expressed confidence in ability to maintain gains after discharge.",
        "Performance declined slightly due to pain flare; plan of care adjusted accordingly.",
        "Objective measures support continued skilled intervention to meet this goal.",
        "Patient independently applied learned strategy without prompting.",
    ];

    // ── Client profiles ───────────────────────────────────────────────────────

    private record ClientSeedProfile(
        string ClientId, string FirstName, string LastName,
        string DateOfBirth, BiologicalSex Sex,
        ClinicalSpecialty Specialty,
        string PriorDiagnoses, string FunctionalLimitations,
        ClinicalSetting Setting, PayerType Payer);

    private static readonly ClientSeedProfile[] ClientPool =
    [
        new("client-alice-morgan",  "Alice",  "Morgan",  "1952-03-14", BiologicalSex.Female, ClinicalSpecialty.PT,    "CVA 2022, L hemiplegia",                             "Limited ROM L shoulder, impaired gait",                ClinicalSetting.Outpatient,             PayerType.Medicare),
        new("client-ben-okafor",    "Ben",    "Okafor",  "1988-07-22", BiologicalSex.Male,   ClinicalSpecialty.PT,    "L ACL repair 3 months post-op",                      "Quad weakness, limited knee extension",                ClinicalSetting.Outpatient,             PayerType.Commercial),
        new("client-cara-santos",   "Cara",   "Santos",  "1975-11-05", BiologicalSex.Female, ClinicalSpecialty.OT,    "R wrist fracture, 8 weeks post-op",                  "Limited grip strength, fine motor deficits",           ClinicalSetting.Outpatient,             PayerType.Commercial),
        new("client-david-chen",    "David",  "Chen",    "1960-08-30", BiologicalSex.Male,   ClinicalSpecialty.PT,    "Total knee replacement, R side",                     "Gait deviation, limited stair negotiation",            ClinicalSetting.Inpatient,              PayerType.MedicareAdvantage),
        new("client-emma-wright",   "Emma",   "Wright",  "2005-02-17", BiologicalSex.Female, ClinicalSpecialty.OT,    "Autism spectrum disorder, Level 1",                  "Sensory processing difficulties, fine motor lag",      ClinicalSetting.SchoolBased,            PayerType.SchoolDistrict),
        new("client-felix-nowak",   "Felix",  "Nowak",   "1945-06-09", BiologicalSex.Male,   ClinicalSpecialty.PT,    "Parkinson's disease, Stage II",                      "Shuffling gait, postural instability",                 ClinicalSetting.Outpatient,             PayerType.Medicare),
        new("client-grace-patel",   "Grace",  "Patel",   "1990-04-25", BiologicalSex.Female, ClinicalSpecialty.SLP,   "Laryngeal cancer post-radiation therapy",             "Dysphagia, voice fatigue",                             ClinicalSetting.Outpatient,             PayerType.Commercial),
        new("client-hiro-tanaka",   "Hiro",   "Tanaka",  "1978-09-13", BiologicalSex.Male,   ClinicalSpecialty.Psych, "Generalized anxiety disorder, PTSD (combat)",        "Avoidance behaviors, sleep disturbance",               ClinicalSetting.Outpatient,             PayerType.Commercial),
        new("client-isla-berg",     "Isla",   "Berg",    "1935-12-01", BiologicalSex.Female, ClinicalSpecialty.PT,    "Hip fracture, ORIF 6 weeks post-op",                 "Non-weight bearing R LE, transfer dependency",         ClinicalSetting.SkilledNursingFacility, PayerType.Medicare),
        new("client-jasper-diallo", "Jasper", "Diallo",  "2019-05-20", BiologicalSex.Male,   ClinicalSpecialty.OT,    "Cerebral palsy, spastic diplegia",                   "Bilateral UE coordination deficits, ADL dependency",   ClinicalSetting.EarlyIntervention,      PayerType.Medicaid),
        new("client-karen-lee",     "Karen",  "Lee",     "1968-01-30", BiologicalSex.Female, ClinicalSpecialty.PT,    "Chronic low back pain, L4–L5 disc herniation",       "Limited lumbar ROM, pain with prolonged sitting",      ClinicalSetting.Outpatient,             PayerType.WorkersCompensation),
        new("client-leo-martinez",  "Leo",    "Martinez","1955-10-18", BiologicalSex.Male,   ClinicalSpecialty.SLP,   "Broca's aphasia post-CVA",                           "Severely limited verbal output, word-finding errors",  ClinicalSetting.Inpatient,              PayerType.Medicare),
        new("client-mia-johnson",   "Mia",    "Johnson", "1993-07-04", BiologicalSex.Female, ClinicalSpecialty.Psych, "Major depressive disorder, recurrent episode",       "Anergia, social withdrawal, sleep hypersomnia",        ClinicalSetting.Outpatient,             PayerType.Commercial),
        new("client-noah-wilson",   "Noah",   "Wilson",  "2008-03-11", BiologicalSex.Male,   ClinicalSpecialty.SLP,   "Mixed receptive-expressive language disorder",       "Limited utterance length, poor vocabulary",            ClinicalSetting.SchoolBased,            PayerType.SchoolDistrict),
        new("client-olivia-harris", "Olivia", "Harris",  "1972-08-27", BiologicalSex.Female, ClinicalSpecialty.OT,    "Multiple sclerosis, relapsing-remitting",             "Fatigue-related ADL difficulty, heat sensitivity",     ClinicalSetting.Telehealth,             PayerType.MedicareAdvantage),
        new("client-paul-garcia",   "Paul",   "Garcia",  "1980-05-15", BiologicalSex.Male,   ClinicalSpecialty.PT,    "Rotator cuff repair, R shoulder 2 months post-op",   "Limited shoulder abduction, pain with overhead tasks", ClinicalSetting.Outpatient,             PayerType.Commercial),
        new("client-quinn-white",   "Quinn",  "White",   "1998-12-22", BiologicalSex.Female, ClinicalSpecialty.Psych, "Panic disorder with agoraphobia",                    "Avoidance of public spaces, frequent panic episodes",  ClinicalSetting.Telehealth,             PayerType.Commercial),
        new("client-rose-thompson", "Rose",   "Thompson","1942-04-07", BiologicalSex.Female, ClinicalSpecialty.OT,    "R CVA, R hemiplegia",                               "ADL dependency, right UE neglect",                     ClinicalSetting.Inpatient,              PayerType.Medicare),
        new("client-sam-clark",     "Sam",    "Clark",   "2001-09-16", BiologicalSex.Male,   ClinicalSpecialty.SLP,   "Traumatic brain injury, moderate severity",           "Cognitive-communication deficits, memory impairment",  ClinicalSetting.Inpatient,              PayerType.Commercial),
        new("client-tina-scott",    "Tina",   "Scott",   "1965-02-28", BiologicalSex.Female, ClinicalSpecialty.PT,    "Bilateral knee OA, awaiting TKR",                   "Pain with stairs and prolonged ambulation",            ClinicalSetting.HomeHealth,             PayerType.Medicare),
    ];

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/seed
    /// Wipes all existing records from all containers, then generates 20 comprehensive
    /// synthetic clients with sessions, goals, therapist profiles, and a provider.
    /// Requires <c>Demo:TherapistName</c> to be set in configuration.
    /// </summary>
    [Function("SeedData")]
    public async Task<HttpResponseData> Seed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seed")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>("Auth:Disabled") && !ClaimsHelper.IsAuthenticated(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication is required.", cancellationToken);
            return unauth;
        }

        var demoTherapist = config["Demo:TherapistName"];
        if (string.IsNullOrWhiteSpace(demoTherapist))
        {
            var disabled = req.CreateResponse(HttpStatusCode.Forbidden);
            await disabled.WriteStringAsync("Demo seeding is not enabled on this deployment.", cancellationToken);
            return disabled;
        }

        var database = cosmosClient.GetDatabase(config["CosmosDb:DatabaseName"] ?? "theragraf");

        // ── 1. Wipe all containers ────────────────────────────────────────────
        var wipeCounts = await WipeAllContainersAsync(database, cancellationToken);

        // ── 2. Seed provider ──────────────────────────────────────────────────
        var providersContainer = database.GetContainer(config["CosmosDb:ProvidersContainerName"] ?? "providers");
        await SeedProviderAsync(providersContainer, cancellationToken);

        // ── 3. Seed therapist profiles ────────────────────────────────────────
        var profilesContainer = database.GetContainer(config["CosmosDb:TherapistProfilesContainerName"] ?? "therapist-profiles");
        await SeedTherapistProfilesAsync(profilesContainer, demoTherapist, cancellationToken);

        // ── 4. Seed clients, sessions, and goals ──────────────────────────────
        var random        = new Randomizer();
        int totalSessions = 0;
        int totalGoals    = 0;

        foreach (var client in ClientPool)
        {
            // Assign therapist based on specialty: OT therapist covers OT/SLP/Psych, PT covers PT.
            var therapistName = demoTherapist;

            // Client demographics.
            await clientRepository.UpsertAsync(client.ClientId, new UpsertClientDemographicsRequest(
                DateOfBirth:           client.DateOfBirth,
                Sex:                   client.Sex,
                PriorDiagnoses:        client.PriorDiagnoses,
                FunctionalLimitations: client.FunctionalLimitations
            ), cancellationToken);

            // Sessions — 3 to 8 per client spread across the past 12 months.
            var sessionCount = random.Int(3, 8);
            var sessions     = BuildSessions(client, therapistName, sessionCount, random);
            foreach (var session in sessions)
            {
                await sessionRepository.SaveAsync(session, cancellationToken);
                totalSessions++;
            }

            // Goals — 1 to 4 per client in varied states.
            var templates  = GoalTemplates[client.Specialty];
            var goalCount  = random.Int(1, Math.Min(4, templates.Length));
            var chosen     = random.ListItems(templates.ToList(), goalCount);
            var statuses   = new[] { GoalStatus.Active, GoalStatus.Active, GoalStatus.Met, GoalStatus.Discontinued };

            foreach (var (title, description) in chosen)
            {
                var targetDate = DateTimeOffset.UtcNow.AddDays(random.Int(14, 90));
                var created    = await goalRepository.CreateAsync(
                    client.ClientId,
                    new CreateGoalRequest(title, description, targetDate),
                    cancellationToken);

                // Add 1–3 progress notes; final note may change status.
                var noteCount = random.Int(1, 3);
                var finalStatus = random.ArrayElement(statuses);
                for (int n = 0; n < noteCount; n++)
                {
                    await goalRepository.UpdateAsync(client.ClientId, created.GoalId, new UpdateGoalRequest(
                        Title:        null,
                        Description:  null,
                        Status:       n == noteCount - 1 ? finalStatus : GoalStatus.Active,
                        TargetDate:   null,
                        ProgressNote: random.ArrayElement(ProgressNoteFragments)
                    ), cancellationToken);
                }

                totalGoals++;
            }
        }

        _logger.LogInformation(
            "Seed complete: {Clients} clients, {Sessions} sessions, {Goals} goals, 2 therapist profiles, 1 provider",
            ClientPool.Length, totalSessions, totalGoals);
        auditLogger.Log(AuditEvent.Success(ClaimsHelper.GetTherapistIdentity(req, config) ?? "app", AuditAction.Write, "SeedData",
            detail: $"Seeded {ClientPool.Length} clients, {totalSessions} sessions, {totalGoals} goals"));

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(new
        {
            clients           = ClientPool.Length,
            sessions          = totalSessions,
            goals             = totalGoals,
            therapistProfiles = 2,
            providers         = 1,
            wiped             = wipeCounts,
        }, JsonOptions), cancellationToken);
        return ok;
    }

    /// <summary>
    /// DELETE /api/seed
    /// Wipes all documents from every data container.
    /// Requires <c>Demo:TherapistName</c> to be set in configuration.
    /// </summary>
    [Function("DeleteSeedData")]
    public async Task<HttpResponseData> DeleteSeed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "seed")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>("Auth:Disabled") && !ClaimsHelper.IsAuthenticated(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication is required.", cancellationToken);
            return unauth;
        }

        var demoTherapist = config["Demo:TherapistName"];
        if (string.IsNullOrWhiteSpace(demoTherapist))
        {
            var disabled = req.CreateResponse(HttpStatusCode.Forbidden);
            await disabled.WriteStringAsync("Demo seeding is not enabled on this deployment.", cancellationToken);
            return disabled;
        }

        var database   = cosmosClient.GetDatabase(config["CosmosDb:DatabaseName"] ?? "theragraf");
        var wipeCounts = await WipeAllContainersAsync(database, cancellationToken);

        _logger.LogInformation("Delete seed complete. Wiped: {Counts}", JsonSerializer.Serialize(wipeCounts));
        auditLogger.Log(AuditEvent.Success(ClaimsHelper.GetTherapistIdentity(req, config) ?? "app", AuditAction.Delete, "SeedData",
            detail: "Wiped all records from all data containers"));

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(new { wiped = wipeCounts }, JsonOptions), cancellationToken);
        return ok;
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task SeedProviderAsync(Container container, CancellationToken ct)
    {
        var doc = new ProviderDocument
        {
            Id              = ProviderId,
            ProviderId      = ProviderId,
            TenantId        = "seed-tenant",
            PracticeName    = "Riverstone Rehabilitation",
            OrganizationNpi = "1234567890",
            AddressLine1    = "4200 Wellness Blvd",
            AddressLine2    = "Suite 300",
            City            = "Austin",
            State           = "TX",
            Zip             = "78701",
            Phone           = "5124445678",
            CreatedAt       = DateTimeOffset.UtcNow,
            UpdatedAt       = DateTimeOffset.UtcNow,
        };
        await container.UpsertItemAsync(doc, new PartitionKey(doc.TenantId), cancellationToken: ct);
    }

    private async Task SeedTherapistProfilesAsync(Container container, string demoTherapistName, CancellationToken ct)
    {
        var now         = DateTimeOffset.UtcNow;
        var nameParts   = demoTherapistName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName   = nameParts.Length > 0 ? nameParts[0] : "Demo";
        var lastName    = nameParts.Length > 1 ? nameParts[1] : "Therapist";

        var otProfile = new TherapistProfileDocument
        {
            Id            = TherapistOtId,
            TherapistId   = TherapistOtId,
            TenantId      = "seed-tenant",
            FirstName     = firstName,
            LastName      = lastName,
            Credentials   = "OTR/L",
            Discipline    = TherapyDiscipline.OccupationalTherapy,
            IndividualNpi = "1122334455",
            ProviderId    = ProviderId,
            CreatedAt     = now,
            UpdatedAt     = now,
        };

        var ptProfile = new TherapistProfileDocument
        {
            Id            = TherapistPtId,
            TherapistId   = TherapistPtId,
            TenantId      = "seed-tenant",
            FirstName     = "Jordan",
            LastName      = "Rivera",
            Credentials   = "PT, DPT",
            Discipline    = TherapyDiscipline.PhysicalTherapy,
            IndividualNpi = "5566778899",
            ProviderId    = ProviderId,
            CreatedAt     = now,
            UpdatedAt     = now,
        };

        await container.UpsertItemAsync(otProfile, new PartitionKey(otProfile.TenantId), cancellationToken: ct);
        await container.UpsertItemAsync(ptProfile, new PartitionKey(ptProfile.TenantId), cancellationToken: ct);
    }

    private static IReadOnlyList<SessionRecord> BuildSessions(
        ClientSeedProfile client, string therapistName, int count, Randomizer random)
    {
        var fragments = SoapFragments[client.Specialty];
        var cpts      = CptPool.Where(c => c.Specialty == client.Specialty).ToArray();
        var icds      = IcdPool.Where(c => c.Specialty == client.Specialty).ToArray();
        var results   = new List<SessionRecord>(count);
        var seen      = new HashSet<string>();

        for (int i = 0; i < count; i++)
        {
            // Space sessions evenly over the past year with slight jitter.
            var daysBack    = (int)((365.0 / count) * (count - i)) + random.Int(-3, 3);
            var sessionDate = DateTime.UtcNow.AddDays(-daysBack).Date;
            var rowKey      = sessionDate.ToString("yyyy-MM-ddT00-00-00Z");

            if (!seen.Add($"{client.ClientId}|{rowKey}"))
                continue;

            var chosenCpts = random.ListItems(cpts.ToList(), random.Int(1, Math.Min(3, cpts.Length)));
            var chosenIcds = random.ListItems(icds.ToList(), random.Int(1, Math.Min(3, icds.Length)));

            results.Add(new SessionRecord
            {
                TherapistName          = therapistName,
                PartitionKey           = client.ClientId,
                Discipline             = client.Specialty.ToString(),
                Setting                = client.Setting.ToString(),
                Payer                  = client.Payer.ToString(),
                SessionDurationMinutes = random.ArrayElement([30, 38, 45, 53, 60, 68, 90]),
                CreatedAt              = new DateTimeOffset(sessionDate, TimeSpan.Zero),
                RowKey                 = rowKey,
                IsSynthetic            = true,
                SoapNoteJson = JsonSerializer.Serialize(new SoapNote(
                    Subjective: random.ArrayElement(fragments.S),
                    Objective:  random.ArrayElement(fragments.O),
                    Assessment: random.ArrayElement(fragments.A),
                    Plan:       random.ArrayElement(fragments.P)
                )),
                CptCodesJson = JsonSerializer.Serialize(chosenCpts.Select(c => new CptCode(
                    c.Code, c.Description,
                    Rationale:     "Selected based on documented skilled intervention and clinical presentation.",
                    BillableUnits: c.IsTimed ? random.Int(1, 4) : 1
                )).ToList()),
                IcdCodesJson = JsonSerializer.Serialize(chosenIcds.Select(c => new IcdCode(
                    c.Code, c.Description,
                    Rationale: "Diagnosis supported by clinical presentation documented in session."
                )).ToList()),
                RedactionMapJson = "{}",
            });
        }

        return results;
    }

    // ── Wipe helper ───────────────────────────────────────────────────────────

    private async Task<Dictionary<string, int>> WipeAllContainersAsync(
        CosmosDatabase database, CancellationToken ct)
    {
        var counts = new Dictionary<string, int>();

        // (containerName, partitionKeyField)
        var targets = new[]
        {
            (config["CosmosDb:ContainerName"]                 ?? "sessions",          "clientId"),
            (config["CosmosDb:GoalsContainerName"]             ?? "goals",             "clientId"),
            (config["CosmosDb:ClientsContainerName"]           ?? "clients",           "clientId"),
            (config["CosmosDb:TherapistProfilesContainerName"] ?? "therapist-profiles","tenantId"),
            (config["CosmosDb:ProvidersContainerName"]         ?? "providers",         "tenantId"),
        };

        foreach (var (name, pkField) in targets)
        {
            int deleted = 0;
            try
            {
                var container = database.GetContainer(name);
                // Project only id + partition key — minimal RU cost.
                var query = new QueryDefinition($"SELECT c.id, c[\"{pkField}\"] AS pk FROM c");
                using var iter = container.GetItemQueryIterator<JsonElement>(query);

                while (iter.HasMoreResults)
                {
                    var page = await iter.ReadNextAsync(ct);
                    var tasks = page
                        .Where(d => d.TryGetProperty("id", out _) && d.TryGetProperty("pk", out _))
                        .Select(d =>
                        {
                            var id = d.GetProperty("id").GetString()!;
                            var pk = d.GetProperty("pk").GetString()!;
                            return container
                                .DeleteItemAsync<JsonElement>(id, new PartitionKey(pk), cancellationToken: ct)
                                .ContinueWith(_ => { }, TaskContinuationOptions.None);
                        });
                    await Task.WhenAll(tasks);
                    deleted += page.Count;
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Container doesn't exist yet — nothing to wipe.
            }

            counts[name] = deleted;
        }

        return counts;
    }

    // ── Retroactive migration ─────────────────────────────────────────────────

    /// <summary>
    /// PATCH /api/seed/mark-synthetic
    /// One-time operation to mark all existing Cosmos DB records as synthetic.
    /// Requires <c>Demo:TherapistName</c> to be set in configuration.
    /// </summary>
    [Function("MarkAllSynthetic")]
    public async Task<HttpResponseData> MarkAllSynthetic(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "seed/mark-synthetic")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>("Auth:Disabled") && !ClaimsHelper.IsAuthenticated(req))
        {
            var unauth = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauth.WriteStringAsync("Authentication is required.", cancellationToken);
            return unauth;
        }

        var demoTherapist = config["Demo:TherapistName"];
        if (string.IsNullOrWhiteSpace(demoTherapist))
        {
            var disabled = req.CreateResponse(HttpStatusCode.Forbidden);
            await disabled.WriteStringAsync("Demo seeding is not enabled on this deployment.", cancellationToken);
            return disabled;
        }

        var dbName            = config["CosmosDb:DatabaseName"] ?? "theragraf";
        var sessionsContainer = config["CosmosDb:ContainerName"] ?? "sessions";
        var clientsContainer  = config["CosmosDb:ClientsContainerName"] ?? "clients";
        var goalsContainer    = config["CosmosDb:GoalsContainerName"] ?? "goals";
        var database          = cosmosClient.GetDatabase(dbName);

        int sessionsUpdated = 0, clientsUpdated = 0, goalsUpdated = 0;

        {
            var container = database.GetContainer(sessionsContainer);
            using var iterator = container.GetItemQueryIterator<SessionDocument>(
                new QueryDefinition("SELECT * FROM c WHERE NOT IS_DEFINED(c.isSynthetic) OR c.isSynthetic = false"));
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var doc in page)
                {
                    doc.IsSynthetic = true;
                    await container.ReplaceItemAsync(doc, doc.Id, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
                    sessionsUpdated++;
                }
            }
        }

        {
            var container = database.GetContainer(clientsContainer);
            using var iterator = container.GetItemQueryIterator<ClientDocument>(
                new QueryDefinition("SELECT * FROM c WHERE NOT IS_DEFINED(c.isSynthetic) OR c.isSynthetic = false"));
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var doc in page)
                {
                    doc.IsSynthetic = true;
                    await container.ReplaceItemAsync(doc, doc.Id, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
                    clientsUpdated++;
                }
            }
        }

        {
            var container = database.GetContainer(goalsContainer);
            using var iterator = container.GetItemQueryIterator<GoalDocument>(
                new QueryDefinition("SELECT * FROM c WHERE NOT IS_DEFINED(c.isSynthetic) OR c.isSynthetic = false"));
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                foreach (var doc in page)
                {
                    doc.IsSynthetic = true;
                    await container.ReplaceItemAsync(doc, doc.Id, new PartitionKey(doc.ClientId), cancellationToken: cancellationToken);
                    goalsUpdated++;
                }
            }
        }

        _logger.LogInformation(
            "Marked {Sessions} sessions, {Clients} clients, and {Goals} goals as synthetic",
            sessionsUpdated, clientsUpdated, goalsUpdated);
        auditLogger.Log(AuditEvent.Success(ClaimsHelper.GetTherapistIdentity(req, config) ?? "app", AuditAction.Write, "SeedMigration",
            detail: $"Marked {sessionsUpdated + clientsUpdated + goalsUpdated} records as synthetic"));

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(new
        {
            sessionsUpdated,
            clientsUpdated,
            goalsUpdated,
            totalUpdated = sessionsUpdated + clientsUpdated + goalsUpdated
        }, JsonOptions), cancellationToken);
        return ok;
    }
}
