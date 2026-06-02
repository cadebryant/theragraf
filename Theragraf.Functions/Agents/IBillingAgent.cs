namespace Theragraf.Functions.Agents;

using Theragraf.Core.Models;

public interface IBillingAgent
{
    Task<IReadOnlyList<CptCode>> SuggestCptCodesAsync(SoapNote note);
}
