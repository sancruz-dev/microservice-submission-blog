using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Exceptions;
using ContentSubmission.Application.Submissions.Mdx;
using ContentSubmission.Domain;

namespace ContentSubmission.Application.Submissions;

public sealed class SubmissionService(
    ISubmissionRepository repository,
    IGitHubIssueClient gitHubIssueClient,
    IGitHubPullRequestClient gitHubPullRequestClient,
    SubmissionMetrics metrics)
{
    public async Task<Submission> CreateAsync(CreateSubmissionInput input, CancellationToken cancellationToken = default)
    {
        metrics.SubmissionReceived();

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
            metrics.SubmissionRejected();
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

        // Content was already fully validated above, so there's no separate
        // async validation step to wait for - Validating/Validated happen back
        // to back, right here.
        //
        // Persisted as Validated *before* calling GitHub (ADR-006), not after:
        // the previous order created the Issue first and only then saved to
        // the database, so a transient database failure (see EnableRetryOnFailure
        // above, and its Fase 8 incident) left an orphaned Issue in a
        // third-party system with no way to roll it back. Saving first means
        // the same kind of failure instead leaves a recoverable row in *our*
        // database - visible, queryable, safe to retry - rather than untracked
        // GitHub state. It doesn't eliminate the partial-failure window (only
        // an outbox/saga would), it just moves it somewhere we control.
        submission.MarkAsValidating();
        submission.MarkAsValidated();

        await repository.AddAsync(submission, cancellationToken);

        var issueNumber = await gitHubIssueClient.CreateIssueAsync(submission, cancellationToken);
        submission.SendForReview(issueNumber);

        await repository.UpdateAsync(submission, cancellationToken);

        return submission;
    }

    public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Applies the curator's decision from the GitHub Issues webhook (see
    /// docs/decisions/ADR-003) - "completed" approves and immediately continues
    /// into publishing (opens the Pull Request, same one-request-no-queue
    /// philosophy as CreateAsync), "not_planned" rejects. Deliberately a no-op,
    /// not an error, when the issue number is unknown or the submission has
    /// already moved past UnderReview: GitHub redelivers webhooks on
    /// timeout/failure, and this keeps a duplicate delivery from throwing
    /// InvalidSubmissionTransitionException instead of just being silently
    /// idempotent.
    /// </summary>
    public async Task HandleGitHubIssueClosedAsync(
        int gitHubIssueNumber,
        string? stateReason,
        CancellationToken cancellationToken = default)
    {
        var submission = await repository.GetByGitHubIssueNumberAsync(gitHubIssueNumber, cancellationToken);

        if (submission is null || submission.Status != SubmissionStatus.UnderReview)
        {
            return;
        }

        switch (stateReason)
        {
            case "completed":
                submission.Approve();
                var pullRequestNumber = await gitHubPullRequestClient.CreatePullRequestAsync(submission, cancellationToken);
                submission.StartPublishing(pullRequestNumber);
                break;
            case "not_planned":
                submission.Reject($"Rejected via GitHub Issue #{gitHubIssueNumber} (closed as not planned).");
                break;
            default:
                // Unrecognized/missing state_reason - don't guess which way to go.
                return;
        }

        await repository.UpdateAsync(submission, cancellationToken);
    }

    /// <summary>
    /// Applies the "merged" half of publishing, from the blog repo's
    /// pull_request webhook. Only a true merge (not a plain close) reaches
    /// Published - closing a PR without merging leaves the submission in
    /// Publishing, for a human to deal with manually (no automated retry/
    /// re-open exists yet). Same idempotent-no-op shape as
    /// HandleGitHubIssueClosedAsync, for the same reason (webhook redelivery).
    /// </summary>
    public async Task HandlePullRequestClosedAsync(
        int gitHubPullRequestNumber,
        bool merged,
        CancellationToken cancellationToken = default)
    {
        if (!merged)
        {
            return;
        }

        var submission = await repository.GetByGitHubPullRequestNumberAsync(gitHubPullRequestNumber, cancellationToken);

        if (submission is null || submission.Status != SubmissionStatus.Publishing)
        {
            return;
        }

        submission.MarkAsPublished();
        await repository.UpdateAsync(submission, cancellationToken);
    }

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
