namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class BillingAgent(Kernel kernel) : BaseAgent(kernel), IBillingAgent
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

    public async Task<IReadOnlyList<CptCode>> SuggestCptCodesAsync(SoapNote note, TherapyDiscipline discipline, int? sessionDurationMinutes)
    {
        var function = Kernel.Plugins.GetFunction("BillingAgent", "BillingAgent");
        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var cptList = CptCodeLists[discipline];
        var timedGuidance = BuildTimedGuidance(sessionDurationMinutes);
        var arguments = new KernelArguments
        {
            ["input"] = soapJson,
            ["cptCodeList"] = cptList,
            ["discipline"] = discipline.ToString(),
            ["timedGuidance"] = timedGuidance
        };
        var result = await Kernel.InvokeAsync(function, arguments);

        var response = JsonSerializer.Deserialize<BillingResponse>(result.ToString(), JsonOptions)!;
        return response.SuggestedCptCodes;
    }

    private static string BuildTimedGuidance(int? durationMinutes)
    {
        if (durationMinutes is null)
            return "Session duration was not provided. Do not suggest billing units for timed codes; note in each timed-code rationale that units could not be calculated.";

        var minutes = durationMinutes.Value;

        // CMS 8-minute rule: a timed unit requires at least 8 minutes of that service.
        // Each additional unit requires at least 8 minutes; a partial unit >= 8 min rounds up.
        // Total billable units across all timed codes must not exceed units derivable from total time.
        var maxUnits = (int)Math.Floor((minutes + 7) / 15.0); // conservative: floor of (minutes / 15)

        return
            $"Session duration: {minutes} minutes. " +
            $"Apply the CMS 8-minute rule: each timed-code unit requires at least 8 minutes of direct skilled service. " +
            $"A partial unit of 8–22 minutes = 1 unit; 23–37 min = 2 units; 38–52 min = 3 units; 53–67 min = 4 units. " +
            $"The maximum total billable units across all timed codes for this session is approximately {maxUnits}. " +
            $"Include recommended billing units in the rationale for each timed code. " +
            $"Untimed codes (e.g. evaluations, self-care training 97535) are billed once per session regardless of duration.";
    }

    private record BillingResponse(IReadOnlyList<CptCode> SuggestedCptCodes);
}
