using ContentSubmission.Domain;

namespace ContentSubmission.Application.Abstractions;

/// <summary>
/// Publishes an approved submission by creating a branch, committing its MDX
/// file and opening a Pull Request in the public blog repository (see
/// docs ADR-003/architecture.md). Returns the PR's repo-scoped number, which
/// StartPublishing persists and which the "merged" webhook later reports back.
/// </summary>
public interface IGitHubPullRequestClient
{
    Task<int> CreatePullRequestAsync(Submission submission, CancellationToken cancellationToken = default);
}
