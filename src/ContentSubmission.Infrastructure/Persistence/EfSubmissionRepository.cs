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

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Submissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Submissions.OrderByDescending(s => s.CreatedAt).ToListAsync(cancellationToken);
}
