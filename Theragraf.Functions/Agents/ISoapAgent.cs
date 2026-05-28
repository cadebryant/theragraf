namespace Theragraf.Functions.Agents;

using Theragraf.Core.Models;

public interface ISoapAgent
{
    Task<SoapNote> GenerateSoapNoteAsync(ObservationResult input);
}
