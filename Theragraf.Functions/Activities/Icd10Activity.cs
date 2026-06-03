namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

public class Icd10Activity(IIcd10Agent icd10Agent)
{
    [Function(nameof(Icd10Activity))]
    public async Task<IReadOnlyList<IcdCode>> Run([ActivityTrigger] Icd10ActivityInput input)
    {
        return await icd10Agent.SuggestIcdCodesAsync(input.Note, input.Discipline);
    }
}

public record Icd10ActivityInput(SoapNote Note, TherapyDiscipline Discipline);
