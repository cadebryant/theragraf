using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

namespace Theragraf.Functions.Activities;

public class SoapActivity(ISoapAgent soapAgent)
{
    [Function(nameof(SoapActivity))]
    public async Task<SoapNote> Run([ActivityTrigger] ObservationResult input)
    {
        return await soapAgent.GenerateSoapNoteAsync(input);
    }
}