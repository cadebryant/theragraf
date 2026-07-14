namespace Theragraf.Core.Models;

/// <summary>
/// Aggregated goal-progress statistics for a single client.
/// Returned by GET /api/goals/stats/client/{clientId}.
/// </summary>
public record ClientGoalStats(
    string ClientId,
    int    TotalGoals,
    int    ActiveGoals,
    int    MetGoals,
    int    NotMetGoals,
    int    DiscontinuedGoals,
    int    OverdueGoals,
    double MetRate,
    bool   IsSynthetic
);

/// <summary>
/// Aggregated goal-progress statistics across all clients for a therapist.
/// Returned by GET /api/goals/stats/therapist/{therapistName}.
/// </summary>
public record TherapistGoalStats(
    string TherapistName,
    int    TotalGoals,
    int    ActiveGoals,
    int    MetGoals,
    int    NotMetGoals,
    int    DiscontinuedGoals,
    int    OverdueGoals,
    int    ClientsWithGoals,
    double MetRate
);
