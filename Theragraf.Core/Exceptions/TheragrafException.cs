namespace Theragraf.Core.Exceptions;

/// <summary>
/// Base exception for all domain-specific failures in the Theragraf pipeline.
/// Carry a user-safe <see cref="UserMessage"/> suitable for returning in HTTP responses
/// or orchestration output — never include PII, transcript content, or secrets.
/// </summary>
public class TheragrafException : Exception
{
    public string UserMessage { get; }

    public TheragrafException(string userMessage, string? technicalDetail = null, Exception? inner = null)
        : base(technicalDetail ?? userMessage, inner)
    {
        UserMessage = userMessage;
    }
}

/// <summary>Raised when PII redaction or the Azure AI Language service fails.</summary>
public class IngestionException : TheragrafException
{
    public IngestionException(string technicalDetail, Exception? inner = null)
        : base("An error occurred while processing the session transcript.", technicalDetail, inner) { }
}

/// <summary>Raised when a Semantic Kernel agent fails to produce a valid response.</summary>
public class AgentException : TheragrafException
{
    public string AgentName { get; }

    public AgentException(string agentName, string technicalDetail, Exception? inner = null)
        : base($"The {agentName} agent was unable to complete its task.", technicalDetail, inner)
    {
        AgentName = agentName;
    }
}

/// <summary>Raised when persisting a session record to storage fails.</summary>
public class PersistenceException : TheragrafException
{
    public PersistenceException(string technicalDetail, Exception? inner = null)
        : base("An error occurred while saving the session record.", technicalDetail, inner) { }
}
