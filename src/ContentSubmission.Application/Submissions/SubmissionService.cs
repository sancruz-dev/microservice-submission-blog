using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;

namespace ContentSubmission.Application.Submissions;

public sealed class SubmissionService(ISubmissionRepository repository)
{
    public async Task<Submission> CreateAsync(CreateSubmissionInput input, CancellationToken cancellationToken = default)
    {
        var author = SubmissionAuthor.Create(input.AuthorName, input.AuthorEmail);
        var slug = Slug.Create(input.Slug);

        var submission = Submission.Create(
            input.Title,
            input.Description,
            author,
            input.Category,
            input.Level,
            slug,
            input.Tags);

        await repository.AddAsync(submission, cancellationToken);

        return submission;
    }

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default) =>
    repository.GetAllAsync(cancellationToken);

}
