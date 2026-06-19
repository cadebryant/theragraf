using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;

namespace Theragraf.Functions.Activities;

public class FinalizerActivity
{
    [Function(nameof(FinalizerActivity))]
    public Task<FinalizeResult> Run([ActivityTrigger] FinalizeInput input)
    {
        var note = input.Note;
        var map = input.RedactionMap;

        var restored = new SoapNote(
            Subjective: Restore(note.Subjective, map),
            Objective:  Restore(note.Objective,  map),
            Assessment: Restore(note.Assessment, map),
            Plan:       Restore(note.Plan,        map)
        );

        return Task.FromResult(new FinalizeResult(restored, Array.Empty<CptCode>(), Array.Empty<IcdCode>(), input.NoteFormat));
    }

    private static string Restore(string text, IReadOnlyDictionary<string, string> map)
    {
        foreach (var (placeholder, original) in map)
            text = text.Replace(placeholder, original);

        return text;
    }
}
