using System.Text.RegularExpressions;

namespace ContentSubmission.Domain;

/// <summary>
/// A validated, filesystem-safe slug. This is a domain invariant, not just formatting:
/// the slug will eventually become part of a file path and a git branch name
/// (docs/security.md), so it is validated at construction time rather than trusted
/// wherever it happens to be used later.
/// </summary>
public sealed partial record Slug
{
    private const int MaxLength = 100;

    public string Value { get; }

    private Slug(string value) => Value = value;

    public static Slug Create(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("Slug cannot be empty.", nameof(candidate));
        }

        var trimmed = candidate.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Slug cannot be longer than {MaxLength} characters.", nameof(candidate));
        }

        if (!ValidSlugPattern().IsMatch(trimmed))
        {
            throw new ArgumentException(
                "Slug must be lowercase kebab-case (letters, digits and single hyphens only, " +
                "no leading/trailing hyphen).",
                nameof(candidate));
        }

        return new Slug(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidSlugPattern();
}
