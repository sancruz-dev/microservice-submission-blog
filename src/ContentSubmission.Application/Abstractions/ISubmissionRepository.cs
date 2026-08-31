using ContentSubmission.Domain;

namespace ContentSubmission.Application.Abstractions;

public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes made to a submission previously read from this repository
    /// (e.g. via GetByGitHubIssueNumberAsync) - not a separate insert.
    /// </summary>
    Task UpdateAsync(Submission submission, CancellationToken cancellationToken = default);

    Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Submission?> GetByGitHubIssueNumberAsync(int gitHubIssueNumber, CancellationToken cancellationToken = default);

    Task<Submission?> GetByGitHubPullRequestNumberAsync(int gitHubPullRequestNumber, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default);
}
