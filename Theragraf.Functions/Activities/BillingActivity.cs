namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

public class BillingActivity(IBillingAgent billingAgent)
{
    [Function(nameof(BillingActivity))]
    public async Task<IReadOnlyList<CptCode>> Run([ActivityTrigger] SoapNote note)
    {
        return await billingAgent.SuggestCptCodesAsync(note);
    }
}
