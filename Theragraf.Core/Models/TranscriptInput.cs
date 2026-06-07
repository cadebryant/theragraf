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
    PayerType Payer = PayerType.Medicare
);
