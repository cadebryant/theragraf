namespace Theragraf.Core.Models;

public record ObservationResult(
    string RedactedTranscript,
    IReadOnlyDictionary<string, string> RedactionMap, // e.g. "[PATIENT_1]" → "John Smith"
    string TherapistName,
    string ClientId,
    DateTimeOffset SessionDate,
    TherapyDiscipline Discipline = TherapyDiscipline.OccupationalTherapy,
    NoteFormat NoteFormat = NoteFormat.Soap
);