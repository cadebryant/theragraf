namespace Theragraf.Core.Models;

/// <summary>
/// Classifies the type of organization associated with a hosted tenant.
/// </summary>
public enum TenantOrganizationType
{
    /// <summary>Single therapist operating independently.</summary>
    SoloPractitioner,

    /// <summary>Multi-therapist clinic or practice group.</summary>
    GroupPractice,

    /// <summary>University or college clinical program.</summary>
    AcademicProgram,

    Other
}
