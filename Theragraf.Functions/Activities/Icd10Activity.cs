namespace Theragraf.Functions.Activities;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

public class Icd10Activity(IIcd10Agent icd10Agent, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<Icd10Activity>();

    [Function(nameof(Icd10Activity))]
    public async Task<IReadOnlyList<IcdCode>> Run([ActivityTrigger] Icd10ActivityInput input)
    {
        _logger.LogInformation("Icd10Activity started discipline={Discipline}", input.Discipline);
        try
        {
            var codes = await icd10Agent.SuggestIcdCodesAsync(input.Note, input.Discipline);
            _logger.LogInformation("Icd10Activity completed icdCodeCount={Count}", codes.Count);
            return codes;
        }
        catch (Exception ex) when (ex is not AgentException)
        {
            _logger.LogError(ex, "Icd10Activity failed discipline={Discipline}", input.Discipline);
            throw new AgentException("ICD-10", ex.Message, ex);
        }
    }
}

public record Icd10ActivityInput(SoapNote Note, TherapyDiscipline Discipline);
