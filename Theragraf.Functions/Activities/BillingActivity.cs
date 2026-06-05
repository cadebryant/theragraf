namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

public class BillingActivity(IBillingAgent billingAgent)
{
    [Function(nameof(BillingActivity))]
    public async Task<IReadOnlyList<CptCode>> Run([ActivityTrigger] BillingActivityInput input)
    {
        return await billingAgent.SuggestCptCodesAsync(input.Note, input.Discipline, input.SessionDurationMinutes, input.Setting, input.Payer);
    }
}

public record BillingActivityInput(
    SoapNote Note,
    TherapyDiscipline Discipline,
    int? SessionDurationMinutes,
    ClinicalSetting Setting = ClinicalSetting.Outpatient,
    PayerType Payer = PayerType.Medicare);
