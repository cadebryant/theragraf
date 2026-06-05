namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;
using Theragraf.Core.Services;

public class BillingAgent(Kernel kernel, ICmsUnitCalculator unitCalculator) : BaseAgent(kernel), IBillingAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<TherapyDiscipline, string> CptCodeLists =
        new Dictionary<TherapyDiscipline, string>
        {
            [TherapyDiscipline.OccupationalTherapy] =
                "97165 (OT evaluation, low complexity), " +
                "97166 (OT evaluation, moderate complexity), " +
                "97167 (OT evaluation, high complexity), " +
                "97168 (OT re-evaluation), " +
                "97110 (therapeutic exercises), " +
                "97112 (neuromuscular reeducation), " +
                "97530 (therapeutic activities), " +
                "97535 (self-care/home management training), " +
                "97750 (physical performance test), " +
                "97760 (orthotic management and training, initial), " +
                "97761 (prosthetic training), " +
                "97150 (therapeutic procedure, group)",

            [TherapyDiscipline.PhysicalTherapy] =
                "97001 (PT evaluation, low complexity), " +
                "97002 (PT evaluation, moderate complexity), " +
                "97003 (PT evaluation, high complexity), " +
                "97004 (PT re-evaluation), " +
                "97010 (hot/cold packs), " +
                "97012 (mechanical traction), " +
                "97014 (electrical stimulation, unattended), " +
                "97016 (vasopneumatic devices), " +
                "97018 (paraffin bath), " +
                "97022 (whirlpool), " +
                "97024 (diathermy), " +
                "97026 (infrared), " +
                "97028 (ultraviolet), " +
                "97032 (electrical stimulation, attended), " +
                "97033 (iontophoresis), " +
                "97034 (contrast baths), " +
                "97035 (ultrasound), " +
                "97036 (Hubbard tank), " +
                "97039 (unlisted therapeutic procedure), " +
                "97110 (therapeutic exercises), " +
                "97112 (neuromuscular reeducation), " +
                "97116 (gait training), " +
                "97129 (therapeutic interventions, initial 15 min), " +
                "97130 (therapeutic interventions, each additional 15 min), " +
                "97150 (therapeutic procedure, group), " +
                "97530 (therapeutic activities), " +
                "97542 (wheelchair management), " +
                "97750 (physical performance test), " +
                "97760 (orthotic management, initial), " +
                "97761 (prosthetic training), " +
                "97762 (orthotic/prosthetic checkout)",

            [TherapyDiscipline.Psychotherapy] =
                "90791 (psychiatric diagnostic evaluation), " +
                "90792 (psychiatric diagnostic evaluation with medical services), " +
                "90832 (psychotherapy, 30 min), " +
                "90833 (psychotherapy add-on, 30 min with E/M), " +
                "90834 (psychotherapy, 45 min), " +
                "90836 (psychotherapy add-on, 45 min with E/M), " +
                "90837 (psychotherapy, 60 min), " +
                "90838 (psychotherapy add-on, 60 min with E/M), " +
                "90839 (psychotherapy for crisis, initial 60 min), " +
                "90840 (psychotherapy for crisis, each additional 30 min), " +
                "90845 (psychoanalysis), " +
                "90846 (family psychotherapy without patient), " +
                "90847 (family psychotherapy with patient), " +
                "90849 (multiple-family group psychotherapy), " +
                "90853 (group psychotherapy), " +
                "90863 (pharmacologic management add-on), " +
                "90875 (individual biofeedback, 30 min), " +
                "96130 (psychological testing, first hour), " +
                "96131 (psychological testing, each additional hour), " +
                "96132 (neuropsychological testing, first hour), " +
                "96133 (neuropsychological testing, each additional hour)"
        };

    public async Task<IReadOnlyList<CptCode>> SuggestCptCodesAsync(
        SoapNote note,
        TherapyDiscipline discipline,
        int? sessionDurationMinutes,
        ClinicalSetting setting = ClinicalSetting.Outpatient,
        PayerType payer = PayerType.Medicare)
    {
        var function = Kernel.Plugins.GetFunction("BillingAgent", "BillingAgent");
        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var cptList = CptCodeLists[discipline];
        var timedGuidance = BuildTimedGuidance(sessionDurationMinutes, setting, payer);
        var arguments = new KernelArguments
        {
            ["input"] = soapJson,
            ["cptCodeList"] = cptList,
            ["discipline"] = discipline.ToString(),
            ["timedGuidance"] = timedGuidance,
            ["setting"] = setting.ToString(),
            ["payer"] = payer.ToString()
        };
        var result = await Kernel.InvokeAsync(function, arguments);

        var response = JsonSerializer.Deserialize<BillingResponse>(StripMarkdownCodeFence(result.ToString()), JsonOptions)!;

        // Validate/clamp the LLM-suggested units with the deterministic 8-minute rule engine
        // so a hallucinated unit count can never propagate to a claim.
        var validated = response.SuggestedCptCodes
            .Select(c => c with
            {
                BillableUnits = unitCalculator.ClampUnits(c.Code, c.BillableUnits, sessionDurationMinutes)
            })
            .ToList();

        return validated;
    }

    private static string BuildTimedGuidance(int? durationMinutes, ClinicalSetting setting, PayerType payer)
    {
        // School-based and Early Intervention services are not billed to insurance via CPT timed units.
        if (setting is ClinicalSetting.SchoolBased or ClinicalSetting.EarlyIntervention)
            return
                $"Setting: {setting}. Payer: {payer}. " +
                "This setting does not use insurance-based CPT timed unit billing. " +
                "Services are funded through school district or state program budgets (IDEA/504 or Early Intervention). " +
                "Do not suggest timed billing units. Instead, note that documentation serves IEP/IFSP compliance, and only include CPT codes if the specific program requires them for Medicaid school-based claiming.";

        // SNF Part A uses Medicare per-diem; timed CPT codes are not separately billable under Part A.
        if (setting is ClinicalSetting.SkilledNursingFacility && payer is PayerType.Medicare)
            return
                $"Setting: {setting}. Payer: {payer}. " +
                "Under Medicare Part A, SNF therapy is reimbursed via per-diem rates (PDPM). " +
                "Individual CPT timed units are NOT separately billable — do not suggest timed billing units. " +
                "Focus on evaluation codes and note total treatment minutes for PDPM classification. " +
                "If the payer is Medicare Advantage or commercial, standard CPT billing may apply — verify the plan policy.";

        // Inpatient acute hospital: CPT codes are typically bundled in the DRG; timed units not separately billed.
        if (setting is ClinicalSetting.Inpatient)
            return
                $"Setting: {setting}. Payer: {payer}. " +
                "Inpatient acute hospital therapy is typically bundled in the DRG payment. " +
                "Timed CPT codes are generally not separately billable in this setting. " +
                "Include evaluation codes where appropriate and document total treatment minutes for resource tracking.";

        // Telehealth: standard timed rules apply but GT/95 modifier is required by most payers.
        if (setting is ClinicalSetting.Telehealth)
        {
            var modifierNote = payer switch
            {
                PayerType.Medicare or PayerType.MedicareAdvantage =>
                    "Append modifier GT (synchronous telehealth) or 95 to each applicable code per payer policy.",
                PayerType.Medicaid =>
                    "Telehealth coverage and modifiers vary by state Medicaid program. Verify state-specific policy before billing.",
                _ =>
                    "Verify telehealth coverage with the specific plan; append modifier 95 or GT as required by the payer."
            };
            return BuildCmsTimedGuidanceText(durationMinutes, setting, payer) +
                   $" TELEHEALTH NOTE: {modifierNote}";
        }

        // Home Health: Medicare covers under a benefit period (not timed CPT units per visit for Part A).
        if (setting is ClinicalSetting.HomeHealth && payer is PayerType.Medicare)
            return
                $"Setting: {setting}. Payer: {payer}. " +
                "Medicare Home Health is reimbursed under PDGM (episode-based). " +
                "Individual timed CPT units are not separately billable under Medicare Part A home health. " +
                "Document visit type and functional goals. If billing Part B outpatient therapy in the home, standard CPT timed rules apply.";

        // Standard outpatient or commercial/other: CMS 8-minute rule applies.
        return BuildCmsTimedGuidanceText(durationMinutes, setting, payer);
    }

    private static string BuildCmsTimedGuidanceText(int? durationMinutes, ClinicalSetting setting, PayerType payer)
    {
        var contextPrefix = $"Setting: {setting}. Payer: {payer}. ";

        if (durationMinutes is null)
            return contextPrefix +
                "Session duration was not provided. Do not suggest billing units for timed codes; note in each timed-code rationale that units could not be calculated.";

        var minutes = durationMinutes.Value;
        var maxUnits = (int)Math.Floor((minutes + 7) / 15.0);

        var payerNote = payer switch
        {
            PayerType.MedicareAdvantage =>
                " Note: Medicare Advantage plans use the same CPT codes as traditional Medicare but may impose their own annual therapy caps or prior-authorization requirements — verify plan policy.",
            PayerType.Medicaid =>
                " Note: Medicaid covered codes and rates are state-specific; verify your state fee schedule before billing.",
            PayerType.Commercial =>
                " Note: Commercial plan benefits, visit caps, and prior-auth rules vary by plan — verify coverage before billing.",
            PayerType.WorkersCompensation =>
                " Note: Workers' Compensation uses state-regulated fee schedules and may require specific CPT codes or modifiers — verify the applicable state schedule.",
            PayerType.SelfPay =>
                " Note: Self-pay sessions have no payer-imposed CPT restrictions or unit caps.",
            _ => string.Empty
        };

        return contextPrefix +
            $"Apply the CMS 8-minute rule: each timed-code unit requires at least 8 minutes of direct skilled service. " +
            $"A partial unit of 8–22 minutes = 1 unit; 23–37 min = 2 units; 38–52 min = 3 units; 53–67 min = 4 units. " +
            $"Session duration: {minutes} minutes. " +
            $"The maximum total billable units across all timed codes for this session is approximately {maxUnits}. " +
            $"Include recommended billing units in the rationale for each timed code. " +
            $"Untimed codes (e.g. evaluations, self-care training 97535) are billed once per session regardless of duration." +
            payerNote;
    }

    private record BillingResponse(IReadOnlyList<CptCode> SuggestedCptCodes);
}
