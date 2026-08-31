using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentSubmission.Infrastructure.Persistence;

public sealed class EfSubmissionRepository(ContentSubmissionDbContext dbContext) : ISubmissionRepository
{
    public async Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        dbContext.Submissions.Add(submission);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // No explicit dbContext.Submissions.Update(submission) call: the submission
    // passed in was read from this same (scoped) DbContext, so EF Core is
    // already tracking it and its changes - this just flushes them.
    public Task UpdateAsync(Submission submission, CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Submissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Submission?> GetByGitHubIssueNumberAsync(int gitHubIssueNumber, CancellationToken cancellationToken = default) =>
        dbContext.Submissions.FirstOrDefaultAsync(s => s.GitHubIssueNumber == gitHubIssueNumber, cancellationToken);

    public Task<Submission?> GetByGitHubPullRequestNumberAsync(int gitHubPullRequestNumber, CancellationToken cancellationToken = default) =>
        dbContext.Submissions.FirstOrDefaultAsync(s => s.GitHubPullRequestNumber == gitHubPullRequestNumber, cancellationToken);

    public async Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Submissions.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
}
