namespace Theragraf.Functions.Agents;

using Theragraf.Core.Models;

public interface IIcd10Agent
{
    Task<IReadOnlyList<IcdCode>> SuggestIcdCodesAsync(SoapNote note, TherapyDiscipline discipline);
}
