using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;

namespace ContentSubmission.Infrastructure.GitHub;

/// <summary>
/// Test double for ContentSubmission.Api.Tests, same role as
/// FakeGitHubIssueClient: no real branch/commit/PR against the GitHub API on
/// every test run.
/// </summary>
public sealed class FakeGitHubPullRequestClient : IGitHubPullRequestClient
{
    private int _nextPullRequestNumber = 100;

    public Task<int> CreatePullRequestAsync(Submission submission, CancellationToken cancellationToken = default) =>
        Task.FromResult(Interlocked.Increment(ref _nextPullRequestNumber));
}
