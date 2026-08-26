namespace ContentSubmission.Application.Submissions.Mdx;

/// <summary>
/// Raw frontmatter fields as deserialized from YAML, before any validation.
/// Every field is nullable/optional at this stage - required-field checks
/// happen in <see cref="MdxSubmissionParser"/>, not here, so YamlDotNet can
/// deserialize a partially-filled or malformed document without throwing.
///
/// Deliberately does NOT include a "date" field: assigning a publish date is
/// a curation-time decision (Phase 9/10, when the PR is actually created),
/// not something a submitter should guess at upload time.
/// </summary>
public sealed class FrontMatterData
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public string? Author { get; set; }
    public string? Category { get; set; }
    public string? Level { get; set; }
    public List<string>? Tags { get; set; }
}
