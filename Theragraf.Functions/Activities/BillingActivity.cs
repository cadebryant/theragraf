namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

public class BillingActivity(IBillingAgent billingAgent, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<BillingActivity>();

    [Function(nameof(BillingActivity))]
    public async Task<IReadOnlyList<CptCode>> Run([ActivityTrigger] BillingActivityInput input)
    {
        _logger.LogInformation("BillingActivity started discipline={Discipline} setting={Setting} payer={Payer}",
            input.Discipline, input.Setting, input.Payer);
        try
        {
            var codes = await billingAgent.SuggestCptCodesAsync(
                input.Note, input.Discipline, input.SessionDurationMinutes, input.Setting, input.Payer);
            _logger.LogInformation("BillingActivity completed cptCodeCount={Count}",
                codes.Count);
            return codes;
        }
        catch (Exception ex) when (ex is not AgentException)
        {
            _logger.LogError(ex, "BillingActivity failed discipline={Discipline}", input.Discipline);
            throw new AgentException("Billing", ex.Message, ex);
        }
    }
}

public record BillingActivityInput(
    SoapNote Note,
    TherapyDiscipline Discipline,
    int? SessionDurationMinutes,
    ClinicalSetting Setting = ClinicalSetting.Outpatient,
    PayerType Payer = PayerType.Medicare);
