using ContentSubmission.Domain;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ContentSubmission.Application.Submissions.Mdx;

/// <summary>
/// Builds the final .mdx file content for an approved submission, ready to
/// commit into the blog repo - the write-side counterpart to
/// MdxDocumentParser. Adds "date", the one field FrontMatterData deliberately
/// doesn't carry (see its own doc comment): assigning a publish date is a
/// curation-time decision, made here, not something the submitter should
/// guess at upload time.
/// </summary>
public static class MdxDocumentBuilder
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Named (not anonymous) so the "Date" field can carry a
    /// [YamlMember(ScalarStyle = ...)] attribute - anonymous types can't take
    /// attributes. Quoting matters here: an unquoted "yyyy-MM-dd" scalar is a
    /// valid YAML 1.1 timestamp, so gray-matter's YAML parser (js-yaml) would
    /// turn it into a JS Date instead of a string, and Next.js's
    /// getStaticProps then fails to JSON-serialize that Date into page props.
    /// </summary>
    private sealed class FrontMatter
    {
        public required string Title { get; init; }

        public required string Description { get; init; }

        [YamlMember(ScalarStyle = ScalarStyle.SingleQuoted)]
        public required string Date { get; init; }

        public required string Slug { get; init; }

        public required string Author { get; init; }

        public required string Category { get; init; }

        public required string Level { get; init; }

        public required IReadOnlyList<string> Tags { get; init; }
    }

    public static string Build(Submission submission, DateTimeOffset? publishedAt = null)
    {
        var frontMatter = new FrontMatter
        {
            Title = submission.Title,
            Description = submission.Description,
            Date = (publishedAt ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd"),
            Slug = submission.Slug.Value,
            Author = submission.Author.Name,
            Category = submission.Category,
            Level = submission.Level.ToString(),
            Tags = submission.Tags,
        };

        var yaml = Serializer.Serialize(frontMatter);
        return $"---\n{yaml}---\n\n{submission.Body}\n";
    }
}
