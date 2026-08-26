namespace ContentSubmission.Application.Exceptions;

/// <summary>
/// Raised when an uploaded submission fails one or more validation checks
/// (frontmatter structure, required fields, field formats, or MDX content
/// safety rules). Carries every error found, not just the first one, so a
/// submitter can fix everything in one pass instead of one round-trip per
/// mistake.
/// </summary>
public sealed class InvalidSubmissionContentException(IReadOnlyList<string> errors)
    : Exception($"Submission content is invalid: {string.Join(" | ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
