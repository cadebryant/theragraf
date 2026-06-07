using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;
using Theragraf.Functions.Logging;

namespace Theragraf.Functions.Activities;

public class SoapActivity(ISoapAgent soapAgent, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SoapActivity>();

    [Function(nameof(SoapActivity))]
    public async Task<SoapNote> Run([ActivityTrigger] ObservationResult input)
    {
        _logger.LogInformation("SoapActivity started for client={ClientId}",
            LogSanitizer.ClientId(input.ClientId));
        try
        {
            var result = await soapAgent.GenerateSoapNoteAsync(input);
            _logger.LogInformation("SoapActivity completed for client={ClientId}",
                LogSanitizer.ClientId(input.ClientId));
            return result;
        }
        catch (Exception ex) when (ex is not AgentException)
        {
            _logger.LogError(ex, "SoapActivity failed for client={ClientId}",
                LogSanitizer.ClientId(input.ClientId));
            throw new AgentException("SOAP", ex.Message, ex);
        }
    }
}