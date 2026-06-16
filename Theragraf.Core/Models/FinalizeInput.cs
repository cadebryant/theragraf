namespace Theragraf.Core.Models;

public record FinalizeInput(
    SoapNote Note,
    IReadOnlyDictionary<string, string> RedactionMap,
    NoteFormat NoteFormat = NoteFormat.Soap
);
