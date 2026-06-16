namespace Theragraf.Core.Services;

using Theragraf.Core.Models;

/// <summary>
/// Persistence operations for client treatment goals.
/// The implementing class is responsible for Cosmos Container: goals, PartitionKey: /clientId.
/// </summary>
public interface IGoalRepository
{
    /// <summary>Returns all goals for <paramref name="clientId"/>, newest first.</summary>
    Task<IReadOnlyList<GoalResponse>> GetByClientIdAsync(
        string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single goal, or <see langword="null"/> if not found or not owned by this client.
    /// </summary>
    Task<GoalResponse?> GetByIdAsync(
        string clientId, string goalId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new goal and returns the persisted record.</summary>
    Task<GoalResponse> CreateAsync(
        string clientId, CreateGoalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a partial update to an existing goal.
    /// Returns the updated <see cref="GoalResponse"/>, or <see langword="null"/> if not found.
    /// </summary>
    Task<GoalResponse?> UpdateAsync(
        string            clientId,
        string            goalId,
        UpdateGoalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a goal. Returns <see langword="true"/> on success, <see langword="false"/>
    /// if the goal did not exist.
    /// </summary>
    Task<bool> DeleteAsync(
        string clientId, string goalId, CancellationToken cancellationToken = default);
}
