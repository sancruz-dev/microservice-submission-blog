using ContentSubmission.Domain;

namespace ContentSubmission.Application.Abstractions;

public interface ISubmissionRepository
{
    Task AddAsync(Submission submission, CancellationToken cancellationToken = default);

    Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default);
}
