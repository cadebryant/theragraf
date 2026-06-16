namespace Theragraf.Functions.Agents;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Theragraf.Core.Models;

public class SoapAgent(Kernel kernel, ILoggerFactory loggerFactory)
    : BaseAgent(kernel, loggerFactory.CreateLogger<SoapAgent>()), ISoapAgent
{
    public async Task<SoapNote> GenerateSoapNoteAsync(ObservationResult input)
    {
        var instructions = input.NoteFormat == NoteFormat.Dap
            ? """
              Generate a DAP note (Data / Assessment / Plan). DAP is the standard format for mental health and psychotherapy documentation.

              Field definitions:
              - Data: Everything observable and reported during the session — client's stated concerns, mood, affect, behavior, thought content, and the therapist's direct observations. Combine what would be "Subjective" and "Objective" in SOAP into a single Data section.
              - Assessment: Clinical interpretation, diagnostic formulation, progress toward treatment goals, and response to interventions.
              - Plan: Specific next steps — therapeutic interventions for the next session, homework assignments, referrals, follow-up schedule.

              Set "Objective" to an empty string in the JSON response — it is not used in DAP format.
              """
            : $"""
              Generate a SOAP note (Subjective / Objective / Assessment / Plan).

              Use the following discipline-specific clinical lens when writing each section:

              - OccupationalTherapy: Focus on occupational performance, activities of daily living (ADLs/IADLs), fine/gross motor skills, sensory processing, cognitive function, and functional independence. Use occupation-based language. Assessment should reference functional deficits and occupational roles. Plan should target meaningful activities and environmental modifications.

              - PhysicalTherapy: Focus on range of motion, muscle strength (MMT grades), pain levels, posture, gait, balance, and functional mobility. Use measurable clinical findings. Assessment should reference movement impairments and activity limitations. Plan should specify therapeutic exercises, manual therapy, and functional mobility goals.

              - SpeechLanguagePathology: Focus on communication, articulation, language comprehension/expression, voice, fluency, feeding, and swallowing. Reference standardized assessment findings where mentioned. Assessment should address communicative participation. Plan should specify speech/language targets, AAC strategies, or dysphagia management as appropriate.

              - Psychotherapy: Focus on the client's reported affect, cognition, and behavior. Objective section should include therapist observations (mood, affect, thought process, insight). Assessment should include clinical formulation or diagnosis reference. Plan should specify therapeutic modality, coping strategies, and follow-up.

              Discipline for this session: {input.Discipline}
              """;

        var raw = await InvokePluginAsync("SoapAgent", "SoapAgent",
            new KernelArguments
            {
                ["input"] = input.RedactedTranscript,
                ["discipline"] = input.Discipline.ToString(),
                ["noteFormatInstructions"] = instructions
            });
        return JsonSerializer.Deserialize<SoapNote>(StripMarkdownCodeFence(raw))!;
    }
}
