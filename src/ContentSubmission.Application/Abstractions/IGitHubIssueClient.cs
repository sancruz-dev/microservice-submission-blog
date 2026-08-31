using ContentSubmission.Domain;

namespace ContentSubmission.Application.Abstractions;

/// <summary>
/// Creates the GitHub Issue that represents a submission during human curation
/// (see docs/decisions/ADR-003). Returns the Issue's repo-scoped number, which
/// is what SendForReview persists and what the closing webhook later reports
/// back - not GitHub's internal, globally-unique Issue id.
/// </summary>
public interface IGitHubIssueClient
{
    Task<int> CreateIssueAsync(Submission submission, CancellationToken cancellationToken = default);
}
