namespace ContentSubmission.Api.Contracts;

public sealed record CreateSubmissionRequest(
    string? Title,
    string? Description,
    string? AuthorName,
    string? AuthorEmail,
    string? Category,
    string? Level,
    string? Slug,
    List<string>? Tags);
