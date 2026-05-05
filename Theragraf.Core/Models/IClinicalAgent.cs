namespace Theragraf.Core.Models;

public interface IClinicalAgent
{
    Task<string> ProcessAsync(string input);
}
