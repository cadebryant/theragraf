namespace Theragraf.Core.Models;

/// <summary>
/// Aggregated statistics for a single therapist across their entire caseload.
/// Returned by GET /api/stats/therapist/{therapistName}.
/// </summary>
public record TherapistStats(
    string                              TherapistName,
    int                                 TotalSessions,
    int                                 TotalClients,
    double                              AverageSessionDurationMinutes,
    int                                 TotalBillableUnits,
    IReadOnlyDictionary<string, int>    SessionsByDiscipline,
    IReadOnlyDictionary<string, int>    SessionsBySetting,
    IReadOnlyDictionary<string, int>    SessionsByPayer,
    IReadOnlyList<CodeFrequency>        TopCptCodes,
    IReadOnlyList<CodeFrequency>        TopIcdCodes
);

/// <summary>
/// Aggregated statistics for a single client across all their sessions.
/// Returned by GET /api/stats/client/{clientId}.
/// </summary>
public record ClientStats(
    string                              ClientId,
    int                                 TotalSessions,
    double                              AverageSessionDurationMinutes,
    int                                 TotalBillableUnits,
    DateTimeOffset?                     FirstSessionDate,
    DateTimeOffset?                     LastSessionDate,
    IReadOnlyDictionary<string, int>    SessionsByTherapist,
    IReadOnlyDictionary<string, int>    SessionsByDiscipline,
    IReadOnlyDictionary<string, int>    SessionsBySetting,
    IReadOnlyDictionary<string, int>    SessionsByPayer,
    IReadOnlyList<CodeFrequency>        TopCptCodes,
    IReadOnlyList<CodeFrequency>        TopIcdCodes
);

/// <summary>A billing code with its frequency count across the aggregated sessions.</summary>
public record CodeFrequency(
    string Code,
    string Description,
    int    Count,
    int    TotalBillableUnits
);
