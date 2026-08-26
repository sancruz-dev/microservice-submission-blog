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

    public Guid Id { get; }
    public string Title { get; }
    public string Description { get; }
    public SubmissionAuthor Author { get; }
    public string Category { get; }
    public SubmissionLevel Level { get; }
    public Slug Slug { get; }
    public IReadOnlyList<string> Tags { get; }
    public SubmissionStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Submission(
        Guid id,
        string title,
        string description,
        SubmissionAuthor author,
        string category,
        SubmissionLevel level,
        Slug slug,
        IReadOnlyList<string> tags,
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
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public void MarkAsValidating() => TransitionTo(SubmissionStatus.Validating);

    public void MarkAsValidated() => TransitionTo(SubmissionStatus.Validated);

    public void SendForReview() => TransitionTo(SubmissionStatus.UnderReview);

    public void Approve() => TransitionTo(SubmissionStatus.Approved);

    public void StartPublishing() => TransitionTo(SubmissionStatus.Publishing);

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
