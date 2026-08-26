namespace ContentSubmission.Application.Submissions;

/// <summary>
/// Everything the API layer has at the point of creation. Title, description,
/// slug, author name, category, level and tags all come from the uploaded
/// file's own frontmatter (RawMdx), not from separate form fields - the
/// frontmatter is the single source of truth for what gets published, so it
/// isn't duplicated as parallel request fields that could disagree with it.
/// AuthorEmail is the exception: it's the submitter's contact address for the
/// review workflow, which has no place in a file that may end up published.
/// </summary>
public sealed record CreateSubmissionInput(string RawMdx, string? AuthorEmail);
