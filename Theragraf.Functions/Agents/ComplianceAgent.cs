namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class ComplianceAgent(Kernel kernel, ILoggerFactory loggerFactory)
    : BaseAgent(kernel, loggerFactory.CreateLogger<ComplianceAgent>()), IComplianceAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ComplianceResult> ValidateAsync(SoapNote note, NoteFormat noteFormat = NoteFormat.Soap)
    {
        var instructions = noteFormat == NoteFormat.Dap
            ? """
              Review the following DAP note (Data / Assessment / Plan) and evaluate it against these criteria:
              - Data: Must include the client's reported symptoms, concerns, or history AND the therapist's direct observations (mood, affect, behavior, thought content). Vague or missing Data sections are non-compliant.
              - Assessment: Must include a clinical interpretation, diagnostic formulation, or progress-toward-goals statement. Vague or missing assessments are non-compliant.
              - Plan: Must include specific, actionable next steps — interventions, homework, referrals, or follow-up schedule.

              Note: The "Objective" field will be empty — this is expected and correct for DAP format. Do NOT flag an empty Objective as non-compliant.
              """
            : """
              Review the following SOAP note (Subjective / Objective / Assessment / Plan) and evaluate it against these criteria:
              - Subjective: Must include the patient's reported symptoms, concerns, or history in their own words.
              - Objective: Must include measurable, observable findings (e.g., behavior, affect, mood ratings, therapist observations).
              - Assessment: Must include a clinical interpretation, diagnosis reference, or formulation. Vague or missing assessments are non-compliant.
              - Plan: Must include a specific, actionable treatment plan (e.g., interventions, follow-up schedule, referrals).
              """;

        var soapJson = JsonSerializer.Serialize(note, JsonOptions);
        var raw = await InvokePluginAsync("ComplianceAgent", "ComplianceAgent",
            new KernelArguments
            {
                ["input"] = soapJson,
                ["noteFormatInstructions"] = instructions
            });
        return JsonSerializer.Deserialize<ComplianceResult>(StripMarkdownCodeFence(raw), JsonOptions)!;
    }
}
