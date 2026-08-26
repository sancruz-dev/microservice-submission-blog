using System.Collections.Concurrent;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;

namespace ContentSubmission.Infrastructure.Persistence;

/// <summary>
/// Placeholder repository for Phase 2. Data does not survive a restart and is not
/// shared across instances - real persistence (EF Core + a database) is Phase 4's
/// job. This exists so the API and domain lifecycle can be built and tested now
/// without prematurely locking in a schema.
/// </summary>
public sealed class InMemorySubmissionRepository : ISubmissionRepository
{
    private readonly ConcurrentDictionary<Guid, Submission> _submissions = new();

    public Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        _submissions[submission.Id] = submission;
        return Task.CompletedTask;
    }

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _submissions.TryGetValue(id, out var submission);
        return Task.FromResult(submission);
    }
}
