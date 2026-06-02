namespace Theragraf.Core.Models;

public record FinalizeResult(
    SoapNote RestoredNote,
    IReadOnlyList<CptCode> SuggestedCptCodes
);
