using ContentSubmission.Domain;
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

    public static string Build(Submission submission, DateTimeOffset? publishedAt = null)
    {
        var frontMatter = new
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
