using ContentSubmission.Domain;

namespace ContentSubmission.Application.Submissions;

public sealed record CreateSubmissionInput(
    string? Title,
    string? Description,
    string? AuthorName,
    string? AuthorEmail,
    string? Category,
    SubmissionLevel Level,
    string? Slug,
    IReadOnlyList<string>? Tags);
