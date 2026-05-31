namespace Theragraf.Functions.Agents;

using Theragraf.Core.Models;

public interface IComplianceAgent
{
    Task<ComplianceResult> ValidateAsync(SoapNote note);
}
