using System.Text.RegularExpressions;

namespace ContentSubmission.Domain;

public sealed partial record SubmissionAuthor
{
    public string Name { get; }
    public string Email { get; }

    private SubmissionAuthor(string name, string email)
    {
        Name = name;
        Email = email;
    }

    public static SubmissionAuthor Create(string? name, string? email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Author name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email) || !SimpleEmailPattern().IsMatch(email))
        {
            throw new ArgumentException("Author email is missing or invalid.", nameof(email));
        }

        return new SubmissionAuthor(name.Trim(), email.Trim());
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex SimpleEmailPattern();
}
