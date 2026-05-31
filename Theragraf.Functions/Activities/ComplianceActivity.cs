using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

namespace Theragraf.Functions.Activities;

public class ComplianceActivity(IComplianceAgent complianceAgent)
{
    [Function(nameof(ComplianceActivity))]
    public async Task<SoapNote> Run([ActivityTrigger] SoapNote input)
    {
        var result = await complianceAgent.ValidateAsync(input);
        return result.ValidatedNote;
    }
}
