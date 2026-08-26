using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Exceptions;
using ContentSubmission.Application.Submissions.Mdx;
using ContentSubmission.Domain;

namespace ContentSubmission.Application.Submissions;

public sealed class SubmissionService(ISubmissionRepository repository)
{
    public async Task<Submission> CreateAsync(CreateSubmissionInput input, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var parsed = MdxDocumentParser.Parse(input.RawMdx);
        errors.AddRange(parsed.Errors);

        Slug? slug = null;
        SubmissionAuthor? author = null;
        SubmissionLevel? level = null;

        if (parsed.FrontMatter is { } frontMatter)
        {
            if (string.IsNullOrWhiteSpace(frontMatter.Title))
            {
                errors.Add("Frontmatter field 'title' is required.");
            }

            if (string.IsNullOrWhiteSpace(frontMatter.Description))
            {
                errors.Add("Frontmatter field 'description' is required.");
            }

            if (string.IsNullOrWhiteSpace(frontMatter.Category))
            {
                errors.Add("Frontmatter field 'category' is required.");
            }

            if (string.IsNullOrWhiteSpace(frontMatter.Slug))
            {
                errors.Add("Frontmatter field 'slug' is required.");
            }
            else
            {
                try
                {
                    slug = Slug.Create(frontMatter.Slug);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Frontmatter field 'slug' is invalid: {CleanMessage(ex)}");
                }
            }

            if (string.IsNullOrWhiteSpace(frontMatter.Level))
            {
                errors.Add("Frontmatter field 'level' is required.");
            }
            else if (Enum.TryParse<SubmissionLevel>(frontMatter.Level, ignoreCase: true, out var parsedLevel))
            {
                level = parsedLevel;
            }
            else
            {
                var allowedLevels = string.Join(", ", Enum.GetNames<SubmissionLevel>());
                errors.Add($"Frontmatter field 'level' must be one of: {allowedLevels}.");
            }

            try
            {
                author = SubmissionAuthor.Create(frontMatter.Author, input.AuthorEmail);
            }
            catch (ArgumentException ex)
            {
                errors.Add(CleanMessage(ex));
            }

            errors.AddRange(MdxContentValidator.Validate(parsed.Body));
        }

        if (errors.Count > 0)
        {
            throw new InvalidSubmissionContentException(errors);
        }

        var submission = Submission.Create(
            parsed.FrontMatter!.Title,
            parsed.FrontMatter.Description,
            author!,
            parsed.FrontMatter.Category,
            level!.Value,
            slug!,
            parsed.FrontMatter.Tags,
            parsed.Body);

        await repository.AddAsync(submission, cancellationToken);

        return submission;
    }

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    /// <summary>
    /// ArgumentException.Message appends "(Parameter 'x')" to whatever message was
    /// passed in - redundant here since the message is already going into an
    /// already-labelled list of errors.
    /// </summary>
    private static string CleanMessage(ArgumentException ex) =>
        ex.Message.Split(" (Parameter ", 2)[0];
}
