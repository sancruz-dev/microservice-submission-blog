using ContentSubmission.Domain.Exceptions;

namespace ContentSubmission.Domain;

/// <summary>
/// Valid state transitions for a submission's lifecycle. Failure/retry is deliberately
/// NOT modeled here as extra statuses (e.g. FAILED, RETRYING): those describe individual
/// processing attempts (see docs/architecture.md), not the submission's own lifecycle.
/// A submission only ever moves forward through this graph, or into the terminal
/// Rejected state.
/// </summary>
public sealed class Submission
{
    private static readonly IReadOnlyDictionary<SubmissionStatus, SubmissionStatus[]> AllowedTransitions =
        new Dictionary<SubmissionStatus, SubmissionStatus[]>
        {
            [SubmissionStatus.Received] = [SubmissionStatus.Validating],
            [SubmissionStatus.Validating] = [SubmissionStatus.Validated, SubmissionStatus.Rejected],
            [SubmissionStatus.Validated] = [SubmissionStatus.UnderReview],
            [SubmissionStatus.UnderReview] = [SubmissionStatus.Approved, SubmissionStatus.Rejected],
            [SubmissionStatus.Approved] = [SubmissionStatus.Publishing],
            [SubmissionStatus.Publishing] = [SubmissionStatus.Published],
            [SubmissionStatus.Published] = [],
            [SubmissionStatus.Rejected] = [],
        };

    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public SubmissionAuthor Author { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public SubmissionLevel Level { get; private set; }
    public Slug Slug { get; private set; } = null!;
    public IReadOnlyList<string> Tags { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public SubmissionStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Number of the GitHub Issue (in the private curation repository) that represents
    /// this submission during human review - see docs/decisions/ADR-003. Null until
    /// <see cref="SendForReview"/> creates that Issue and the number is known.
    /// </summary>
    public int? GitHubIssueNumber { get; private set; }

    /// <summary>
    /// Number of the Pull Request (in the public blog repository) that publishes
    /// this submission's content - see docs ADR-003/architecture.md. Null until
    /// <see cref="StartPublishing"/> creates it right after approval.
    /// </summary>
    public int? GitHubPullRequestNumber { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Reserved for EF Core materialization - every property is then set via
    /// reflection, never left in this default state for real use. All public
    /// construction goes through <see cref="Create"/>.
    /// </summary>
    private Submission()
    {
    }

    private Submission(
        Guid id,
        string title,
        string description,
        SubmissionAuthor author,
        string category,
        SubmissionLevel level,
        Slug slug,
        IReadOnlyList<string> tags,
        string body,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        Author = author;
        Category = category;
        Level = level;
        Slug = slug;
        Tags = tags;
        Body = body;
        Status = SubmissionStatus.Received;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Submission Create(
        string? title,
        string? description,
        SubmissionAuthor author,
        string? category,
        SubmissionLevel level,
        Slug slug,
        IEnumerable<string>? tags,
        string? body,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty.", nameof(description));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body cannot be empty.", nameof(body));
        }

        var normalizedTags = (tags ?? [])
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Submission(
            Guid.NewGuid(),
            title.Trim(),
            description.Trim(),
            author,
            category.Trim(),
            level,
            slug,
            normalizedTags,
            body.Trim(),
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public void MarkAsValidating() => TransitionTo(SubmissionStatus.Validating);

    public void MarkAsValidated() => TransitionTo(SubmissionStatus.Validated);

    public void SendForReview(int gitHubIssueNumber)
    {
        if (gitHubIssueNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gitHubIssueNumber), "GitHub issue number must be positive.");
        }

        TransitionTo(SubmissionStatus.UnderReview);
        GitHubIssueNumber = gitHubIssueNumber;
    }

    public void Approve() => TransitionTo(SubmissionStatus.Approved);

    public void StartPublishing(int gitHubPullRequestNumber)
    {
        if (gitHubPullRequestNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gitHubPullRequestNumber), "GitHub pull request number must be positive.");
        }

        TransitionTo(SubmissionStatus.Publishing);
        GitHubPullRequestNumber = gitHubPullRequestNumber;
    }

    public void MarkAsPublished() => TransitionTo(SubmissionStatus.Published);

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A rejection reason is required.", nameof(reason));
        }

        TransitionTo(SubmissionStatus.Rejected);
        RejectionReason = reason.Trim();
    }

    private void TransitionTo(SubmissionStatus target)
    {
        if (!AllowedTransitions[Status].Contains(target))
        {
            throw new InvalidSubmissionTransitionException(Status, target);
        }

        Status = target;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
