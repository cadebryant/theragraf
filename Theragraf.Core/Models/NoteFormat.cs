namespace Theragraf.Core.Models;

public enum NoteFormat
{
    /// <summary>
    /// Standard SOAP format (Subjective / Objective / Assessment / Plan).
    /// Default for OT, PT, and SLP disciplines.
    /// </summary>
    Soap,

    /// <summary>
    /// DAP format (Data / Assessment / Plan).
    /// Clinical standard for Psychotherapy and mental-health practitioners.
    /// Stored using the same <see cref="SoapNote"/> shape:
    ///   Data          → <see cref="SoapNote.Subjective"/>
    ///   Assessment    → <see cref="SoapNote.Assessment"/>
    ///   Plan          → <see cref="SoapNote.Plan"/>
    ///   Objective     → empty string (not used)
    /// </summary>
    Dap,
}
