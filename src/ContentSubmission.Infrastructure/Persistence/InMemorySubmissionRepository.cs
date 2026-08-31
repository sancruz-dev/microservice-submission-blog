using System.Collections.Concurrent;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;

namespace ContentSubmission.Infrastructure.Persistence;

/// <summary>
/// Real persistence is EfSubmissionRepository (SQL Server) as of Phase 4 - this
/// class is kept on purpose as a fast, no-database test double for
/// ContentSubmission.Api.Tests, which exercise HTTP/validation behavior, not
/// persistence itself.
/// </summary>
public sealed class InMemorySubmissionRepository : ISubmissionRepository
{
    private readonly ConcurrentDictionary<Guid, Submission> _submissions = new();

    public Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        _submissions[submission.Id] = submission;
        return Task.CompletedTask;
    }

    // The instance held here is the same reference the caller mutated, so this
    // is a no-op in practice - kept for interface symmetry with EfSubmissionRepository.
    public Task UpdateAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        _submissions[submission.Id] = submission;
        return Task.CompletedTask;
    }

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _submissions.TryGetValue(id, out var submission);
        return Task.FromResult(submission);
    }

    public Task<Submission?> GetByGitHubIssueNumberAsync(int gitHubIssueNumber, CancellationToken cancellationToken = default)
    {
        var submission = _submissions.Values.FirstOrDefault(s => s.GitHubIssueNumber == gitHubIssueNumber);
        return Task.FromResult(submission);
    }

    public Task<Submission?> GetByGitHubPullRequestNumberAsync(int gitHubPullRequestNumber, CancellationToken cancellationToken = default)
    {
        var submission = _submissions.Values.FirstOrDefault(s => s.GitHubPullRequestNumber == gitHubPullRequestNumber);
        return Task.FromResult(submission);
    }

    public Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Submission> all = [.. _submissions.Values];
        return Task.FromResult(all);
    }
}
