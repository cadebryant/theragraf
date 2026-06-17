namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Services;

public class Icd10Agent(
    Kernel kernel,
    ILoggerFactory loggerFactory,
    IPromptInputHardeningService promptInputHardeningService)
    : BaseAgent(kernel, loggerFactory.CreateLogger<Icd10Agent>()), IIcd10Agent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<TherapyDiscipline, string> IcdCodeLists =
        new Dictionary<TherapyDiscipline, string>
        {
            [TherapyDiscipline.OccupationalTherapy] =
                // Fine motor / upper extremity
                "M62.81 (muscle weakness), " +
                "M79.3 (panniculitis), " +
                "R27.8 (other lack of coordination), " +
                "R29.3 (abnormal posture), " +
                "Z87.39 (personal history of musculoskeletal disorder), " +
                // Neurological
                "G80.0 (spastic quadriplegic cerebral palsy), " +
                "G80.1 (spastic diplegic cerebral palsy), " +
                "G80.2 (spastic hemiplegic cerebral palsy), " +
                "G80.8 (other cerebral palsy), " +
                "G35 (multiple sclerosis), " +
                "G20 (Parkinson's disease), " +
                "I69.351 (hemiplegia following cerebral infarction), " +
                // Developmental / pediatric
                "F82 (specific developmental disorder of motor function / developmental coordination disorder), " +
                "F84.0 (autistic disorder), " +
                "F84.5 (Asperger syndrome), " +
                "F88 (other disorders of psychological development), " +
                "F89 (unspecified disorder of psychological development), " +
                "F90.0 (ADHD, predominantly inattentive), " +
                "F90.1 (ADHD, predominantly hyperactive-impulsive), " +
                "F90.2 (ADHD, combined), " +
                "F81.0 (specific reading disorder / dyslexia), " +
                "F81.2 (mathematics disorder / dyscalculia), " +
                "F81.81 (disorder of written expression / dysgraphia), " +
                // Sensory processing
                "R20.2 (paraesthesia of skin), " +
                "R20.8 (other disturbances of skin sensation), " +
                // ADL / functional
                "Z74.09 (other reduced mobility), " +
                "Z73.6 (limitation of activities due to disability), " +
                // Hand / wrist
                "M65.4 (radial styloid tenosynovitis / de Quervain), " +
                "G56.00 (carpal tunnel syndrome, unspecified upper limb), " +
                "M72.0 (palmar fascial fibromatosis / Dupuytren), " +
                // Burns / wounds
                "T14.0 (open wound of unspecified body region), " +
                "L89.90 (pressure ulcer, unspecified site)",

            [TherapyDiscipline.PhysicalTherapy] =
                // Spine
                "M54.5 (low back pain), " +
                "M54.2 (cervicalgia), " +
                "M51.16 (intervertebral disc degeneration, lumbar), " +
                "M47.816 (spondylosis with radiculopathy, lumbar), " +
                "M47.812 (spondylosis with radiculopathy, cervical), " +
                // Lower extremity
                "M17.11 (primary osteoarthritis, right knee), " +
                "M17.12 (primary osteoarthritis, left knee), " +
                "M16.11 (primary osteoarthritis, right hip), " +
                "M16.12 (primary osteoarthritis, left hip), " +
                "M25.361 (stiffness of right knee), " +
                "M25.362 (stiffness of left knee), " +
                "S83.006A (unspecified tear of medial meniscus, initial encounter), " +
                "M76.50 (patellar tendinitis, unspecified knee), " +
                // Upper extremity
                "M75.1 (rotator cuff syndrome), " +
                "M75.0 (adhesive capsulitis of shoulder), " +
                "M77.10 (lateral epicondylitis, unspecified elbow), " +
                // Neurological
                "G35 (multiple sclerosis), " +
                "G20 (Parkinson's disease), " +
                "I69.351 (hemiplegia following cerebral infarction), " +
                "G80.0 (spastic quadriplegic cerebral palsy), " +
                "G57.00 (sciatic nerve lesion, unspecified lower limb), " +
                // Balance / gait
                "R26.0 (ataxic gait), " +
                "R26.81 (unsteadiness on feet), " +
                "H81.10 (benign paroxysmal positional vertigo, unspecified ear), " +
                // Post-surgical
                "Z96.641 (presence of right artificial knee joint), " +
                "Z96.642 (presence of left artificial knee joint), " +
                "Z96.641 (presence of right artificial hip joint), " +
                // Strength / conditioning
                "M62.81 (muscle weakness), " +
                "R53.1 (weakness), " +
                "Z87.39 (personal history of musculoskeletal disorder)",

            [TherapyDiscipline.Psychotherapy] =
                // Anxiety
                "F41.1 (generalized anxiety disorder), " +
                "F41.0 (panic disorder without agoraphobia), " +
                "F40.10 (social anxiety disorder, unspecified), " +
                "F41.9 (anxiety disorder, unspecified), " +
                // Depressive
                "F32.0 (major depressive disorder, single episode, mild), " +
                "F32.1 (major depressive disorder, single episode, moderate), " +
                "F32.2 (major depressive disorder, single episode, severe without psychosis), " +
                "F33.0 (major depressive disorder, recurrent, mild), " +
                "F33.1 (major depressive disorder, recurrent, moderate), " +
                "F34.1 (dysthymic disorder / persistent depressive disorder), " +
                // Trauma
                "F43.10 (post-traumatic stress disorder, unspecified), " +
                "F43.11 (PTSD, acute), " +
                "F43.12 (PTSD, chronic), " +
                "F43.20 (adjustment disorder, unspecified), " +
                "F43.21 (adjustment disorder with depressed mood), " +
                "F43.22 (adjustment disorder with anxiety), " +
                "F43.23 (adjustment disorder with mixed anxiety and depressed mood), " +
                // OCD / related
                "F42.2 (mixed obsessional thoughts and acts / OCD), " +
                "F45.1 (undifferentiated somatoform disorder), " +
                // Bipolar
                "F31.0 (bipolar I, current episode hypomanic), " +
                "F31.10 (bipolar I, current episode manic, unspecified), " +
                "F31.81 (bipolar II disorder), " +
                // Personality
                "F60.3 (borderline personality disorder), " +
                "F60.9 (personality disorder, unspecified), " +
                // Neurodevelopmental
                "F90.0 (ADHD, predominantly inattentive), " +
                "F90.1 (ADHD, predominantly hyperactive-impulsive), " +
                "F90.2 (ADHD, combined), " +
                "F84.0 (autistic disorder), " +
                // Sleep / other
                "G47.00 (insomnia, unspecified), " +
                "F51.01 (primary insomnia), " +
                "Z63.0 (problems in relationship with spouse or partner), " +
                "Z60.0 (problems of adjustment to life-cycle transitions), " +
                "Z65.8 (other specified problems related to psychosocial circumstances)",

            [TherapyDiscipline.SpeechLanguagePathology] =
                // Language disorders
                "F80.0 (phonological disorder), " +
                "F80.1 (expressive language disorder), " +
                "F80.2 (mixed receptive-expressive language disorder), " +
                "F80.4 (speech and language development delay due to hearing loss), " +
                "F80.81 (childhood onset fluency disorder / stuttering), " +
                "F80.82 (social pragmatic communication disorder), " +
                "F80.89 (other developmental disorders of speech and language), " +
                "F80.9 (developmental disorder of speech and language, unspecified), " +
                // Aphasia / acquired language
                "R47.01 (aphasia), " +
                "R47.02 (dysphasia), " +
                "I69.320 (aphasia following cerebral infarction), " +
                "I69.321 (dysphasia following cerebral infarction), " +
                // Voice / resonance
                "R49.0 (dysphonia), " +
                "R49.1 (aphonia), " +
                "R49.21 (hypernasality), " +
                "R49.22 (hyponasality), " +
                "J38.3 (other diseases of vocal cords — vocal nodules/polyps), " +
                // Articulation / fluency / motor speech
                "R47.1 (dysarthria and anarthria), " +
                "R47.81 (slurred speech), " +
                "F98.5 (adult onset fluency disorder), " +
                // Dysphagia / feeding
                "R13.10 (dysphagia, unspecified), " +
                "R13.11 (dysphagia, oral phase), " +
                "R13.12 (dysphagia, oropharyngeal phase), " +
                "R13.13 (dysphagia, pharyngeal phase), " +
                "R13.14 (dysphagia, pharyngoesophageal phase), " +
                "R13.19 (other dysphagia), " +
                // Neurological
                "G35 (multiple sclerosis), " +
                "G20 (Parkinson's disease), " +
                "I69.391 (other sequelae of cerebral infarction — communication deficits), " +
                "G80.0 (spastic quadriplegic cerebral palsy), " +
                "G80.1 (spastic diplegic cerebral palsy), " +
                // Cognitive-communication
                "F06.8 (other specified mental disorders due to known physiological condition), " +
                "R41.3 (other amnesia / memory impairment), " +
                "R41.840 (attention and concentration deficit), " +
                // Hearing
                "H90.3 (sensorineural hearing loss, bilateral), " +
                "H90.5 (unspecified sensorineural hearing loss), " +
                "H91.90 (unspecified hearing loss, unspecified ear), " +
                // Developmental
                "F84.0 (autistic disorder — communication component), " +
                "F84.5 (Asperger syndrome — communication component), " +
                "F81.0 (specific reading disorder / dyslexia)"
        };

    public async Task<IReadOnlyList<IcdCode>> SuggestIcdCodesAsync(
        SoapNote note, TherapyDiscipline discipline,
        ClientDemographicsSummary? demographics = null)
    {
        var function = Kernel.Plugins.GetFunction("Icd10Agent", "Icd10Agent");
        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var icdList  = IcdCodeLists[discipline];

        if (demographics is not null &&
            !promptInputHardeningService.TrySanitize(demographics, out demographics, out var hardeningError))
        {
            throw new AgentException("Icd10Agent", hardeningError ?? "Demographics content failed validation.");
        }

        // Build a concise, non-PII demographics context string for the prompt.
        // Only age range (not exact age) and sex are included to minimise re-identification risk.
        var demographicsContext = BuildDemographicsContext(demographics);

        var arguments = new KernelArguments
        {
            ["input"]               = soapJson,
            ["icdCodeList"]         = icdList,
            ["discipline"]          = discipline.ToString(),
            ["demographicsContext"] = demographicsContext,
        };
        var result = await Kernel.InvokeAsync(function, arguments);

        var response = JsonSerializer.Deserialize<Icd10Response>(StripMarkdownCodeFence(result.ToString()), JsonOptions)!;
        return response.SuggestedIcdCodes;
    }

    /// <summary>
    /// Returns a short plain-text demographics context string suitable for inclusion in a
    /// clinical AI prompt.  Only age range (not exact age) and sex are forwarded to reduce
    /// re-identification risk.  Prior diagnoses and functional limitations are included as
    /// they are clinical, not identifying, context.
    /// </summary>
    private static string BuildDemographicsContext(ClientDemographicsSummary? d)
    {
        if (d is null) return "No demographic context available.";

        var parts = new List<string>();

        if (d.AgeYears.HasValue)
        {
            var age = d.AgeYears.Value;
            var range = age switch
            {
                < 3   => "infant (0-2 yrs)",
                < 6   => "preschool age (3-5 yrs)",
                < 13  => "school age (6-12 yrs)",
                < 18  => "adolescent (13-17 yrs)",
                < 26  => "young adult (18-25 yrs)",
                < 40  => "adult (26-39 yrs)",
                < 65  => "middle-aged adult (40-64 yrs)",
                < 80  => "older adult (65-79 yrs)",
                _     => "elderly adult (80+ yrs)",
            };
            parts.Add($"Age range: {range}");
        }

        if (d.Sex != BiologicalSex.NotSpecified)
            parts.Add($"Sex: {d.Sex}");

        if (!string.IsNullOrWhiteSpace(d.PriorDiagnoses))
            parts.Add($"Prior diagnoses/history: {d.PriorDiagnoses}");

        if (!string.IsNullOrWhiteSpace(d.FunctionalLimitations))
            parts.Add($"Functional limitations: {d.FunctionalLimitations}");

        return parts.Count > 0
            ? string.Join("; ", parts) + "."
            : "No demographic context available.";
    }

    private record Icd10Response(IReadOnlyList<IcdCode> SuggestedIcdCodes);
}
