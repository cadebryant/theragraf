using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Theragraf.Core.Models;

public record TranscriptInput(
    string RawTranscript,
    string TherapistName,
    string ClientId,
    DateTimeOffset SessionDate,
    TherapyDiscipline Discipline = TherapyDiscipline.OccupationalTherapy,
    int? SessionDurationMinutes = null,
    ClinicalSetting Setting = ClinicalSetting.Outpatient,
    PayerType Payer = PayerType.Medicare
);
