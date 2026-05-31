namespace Theragraf.Core.Models;

public record ComplianceResult(
    SoapNote ValidatedNote,
    bool IsCompliant,
    IReadOnlyList<string> Issues
);
