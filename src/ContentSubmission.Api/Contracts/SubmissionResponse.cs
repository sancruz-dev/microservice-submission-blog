using ContentSubmission.Domain;

namespace ContentSubmission.Api.Contracts;

public sealed record SubmissionResponse(
    Guid Id,
    string Status,
    string Title,
    string Description,
    string AuthorName,
    string AuthorEmail,
    string Category,
    string Level,
    string Slug,
    IReadOnlyList<string> Tags,
    string Body,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static SubmissionResponse FromDomain(Submission submission) => new(
        submission.Id,
        submission.Status.ToString(),
        submission.Title,
        submission.Description,
        submission.Author.Name,
        submission.Author.Email,
        submission.Category,
        submission.Level.ToString(),
        submission.Slug.Value,
        submission.Tags,
        submission.Body,
        submission.RejectionReason,
        submission.CreatedAt,
        submission.UpdatedAt);
}
