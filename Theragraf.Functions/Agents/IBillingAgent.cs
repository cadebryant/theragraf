namespace Theragraf.Functions.Agents;

using Theragraf.Core.Models;

public interface IBillingAgent
{
    Task<IReadOnlyList<CptCode>> SuggestCptCodesAsync(
        SoapNote note,
        TherapyDiscipline discipline,
        int? sessionDurationMinutes,
        ClinicalSetting setting = ClinicalSetting.Outpatient,
        PayerType payer = PayerType.Medicare);
}
