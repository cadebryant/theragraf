using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Theragraf.Core.Exceptions;
using Theragraf.Core.Models;
using Theragraf.Functions.Agents;

namespace Theragraf.Functions.Activities;

public record ComplianceActivityInput(SoapNote Note, NoteFormat NoteFormat = NoteFormat.Soap);

public class ComplianceActivity(IComplianceAgent complianceAgent, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ComplianceActivity>();

    [Function(nameof(ComplianceActivity))]
    public async Task<SoapNote> Run([ActivityTrigger] ComplianceActivityInput input)
    {
        _logger.LogInformation("ComplianceActivity started");
        try
        {
            var result = await complianceAgent.ValidateAsync(input.Note, input.NoteFormat);
            _logger.LogInformation("ComplianceActivity completed isCompliant={IsCompliant}",
                result.IsCompliant);
            return result.ValidatedNote;
        }
        catch (Exception ex) when (ex is not AgentException)
        {
            _logger.LogError(ex, "ComplianceActivity failed");
            throw new AgentException("Compliance", ex.Message, ex);
        }
    }
}
