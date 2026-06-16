using System.ComponentModel.DataAnnotations;

namespace Theragraf.Core.Models;

public record TranscriptInput(
    [property: Required]
    string RawTranscript,

    [property: Required]
    string TherapistName,

    [property: Required]
    string ClientId,

    [property: Required]
    DateTimeOffset SessionDate,

    TherapyDiscipline Discipline = TherapyDiscipline.OccupationalTherapy,

    [property: Range(1, 480)]
    int? SessionDurationMinutes = null,

    ClinicalSetting Setting = ClinicalSetting.Outpatient,
    PayerType Payer = PayerType.Medicare,

    /// <summary>
    /// Note format to generate. Defaults to <see cref="NoteFormat.Soap"/> for all disciplines
    /// except <see cref="TherapyDiscipline.Psychotherapy"/>, where <see cref="NoteFormat.Dap"/>
    /// is the clinical standard and should be the frontend default.
    /// </summary>
    NoteFormat NoteFormat = NoteFormat.Soap,

    /// <summary>
    /// Optional non-PII demographic context forwarded by the frontend.
    /// When present, passed to the ICD-10 agent to improve code precision.
    /// Never contains raw DOB — only computed age in years.
    /// </summary>
    ClientDemographicsSummary? Demographics = null
);
