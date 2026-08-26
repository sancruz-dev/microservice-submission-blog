using System.Text.RegularExpressions;

namespace ContentSubmission.Application.Submissions.Mdx;

/// <summary>
/// Structural/content checks on the MDX body (frontmatter already stripped).
///
/// Deliberately does NOT attempt to parse MDX/JSX into an AST - the blog
/// already has an authoritative MDX toolchain (next-mdx-remote, exercised by
/// its own CI build), and re-implementing an MDX compiler here just to
/// duplicate that check would be exactly the kind of "technology for its own
/// sake" this project is trying to avoid. What's checked here is what this
/// service is actually in a position to know before the content is even
/// stored: things that are cheap to catch early and would otherwise waste a
/// curator's time or a CI run.
/// </summary>
public static partial class MdxContentValidator
{
    public const int MaxBodyLengthChars = 200_000;

    public static IReadOnlyList<string> Validate(string? body)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(body))
        {
            errors.Add("Content body cannot be empty.");
            return errors;
        }

        if (body.Length > MaxBodyLengthChars)
        {
            errors.Add($"Content body cannot be longer than {MaxBodyLengthChars} characters.");
        }

        // next-mdx-remote serializes content that isn't bundled by webpack, so it
        // cannot resolve import/export statements - see sancruzblog-nextjs's own
        // notes on this in components/ComponentsForMDX.js. Content relying on
        // these would compile but fail at render time, so it's rejected up front.
        if (ImportOrExportPattern().IsMatch(body))
        {
            errors.Add("Content must not contain 'import'/'export' statements " +
                        "(unsupported by next-mdx-remote's rendering model).");
        }

        foreach (var (pattern, description) in DangerousPatterns)
        {
            if (pattern.IsMatch(body))
            {
                errors.Add($"Content contains disallowed markup: {description}.");
            }
        }

        return errors;
    }

    private static readonly (Regex Pattern, string Description)[] DangerousPatterns =
    [
        (ScriptTagPattern(), "<script> tag"),
        (IframeTagPattern(), "<iframe> tag"),
        (JavascriptUriPattern(), "javascript: URI"),
        (EventHandlerAttributePattern(), "inline event handler attribute (e.g. onerror=, onload=)"),
    ];

    [GeneratedRegex(@"^\s*(import|export)\s", RegexOptions.Multiline)]
    private static partial Regex ImportOrExportPattern();

    [GeneratedRegex(@"<script\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagPattern();

    [GeneratedRegex(@"<iframe\b", RegexOptions.IgnoreCase)]
    private static partial Regex IframeTagPattern();

    [GeneratedRegex(@"javascript\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUriPattern();

    [GeneratedRegex(@"\son\w+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerAttributePattern();
}
