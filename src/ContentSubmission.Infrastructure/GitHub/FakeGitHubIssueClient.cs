using System.Collections.Concurrent;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;

namespace ContentSubmission.Infrastructure.GitHub;

/// <summary>
/// Test double for ContentSubmission.Api.Tests, same role as
/// InMemorySubmissionRepository: these tests exercise HTTP/validation behavior
/// and must not make real calls to the GitHub API (which would need a live
/// token and would create real Issues on every test run).
/// </summary>
public sealed class FakeGitHubIssueClient : IGitHubIssueClient
{
    private int _nextIssueNumber = 1;

    public Task<int> CreateIssueAsync(Submission submission, CancellationToken cancellationToken = default) =>
        Task.FromResult(Interlocked.Increment(ref _nextIssueNumber));
}
