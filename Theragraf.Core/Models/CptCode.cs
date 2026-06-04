namespace Theragraf.Core.Models;

public record CptCode(
    string Code,
    string Description,
    string Rationale,
    int BillableUnits = 1
);
