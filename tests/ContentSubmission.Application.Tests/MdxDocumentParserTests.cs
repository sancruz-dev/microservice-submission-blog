using ContentSubmission.Application.Submissions.Mdx;

namespace ContentSubmission.Application.Tests;

public class MdxDocumentParserTests
{
    private const string ValidDocument = """
        ---
        title: How RabbitMQ works
        description: An introduction to messaging.
        slug: how-rabbitmq-works
        author: Jane Doe
        category: Backend
        level: Intermediate
        tags:
          - rabbitmq
          - messaging
        ---

        RabbitMQ is a message broker.
        """;

    [Fact]
    public void Parses_frontmatter_and_body_from_a_valid_document()
    {
        var result = MdxDocumentParser.Parse(ValidDocument);

        Assert.True(result.Success);
        Assert.Equal("How RabbitMQ works", result.FrontMatter!.Title);
        Assert.Equal("how-rabbitmq-works", result.FrontMatter.Slug);
        Assert.Equal(["rabbitmq", "messaging"], result.FrontMatter.Tags);
        Assert.Equal("RabbitMQ is a message broker.", result.Body);
    }

    [Fact]
    public void Fails_when_document_does_not_start_with_a_frontmatter_delimiter()
    {
        var result = MdxDocumentParser.Parse("# Just a heading, no frontmatter");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("must start with a YAML frontmatter block"));
    }

    [Fact]
    public void Fails_when_frontmatter_is_not_closed()
    {
        var result = MdxDocumentParser.Parse("---\ntitle: Unclosed\n\nBody without a closing delimiter.");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("missing its closing"));
    }

    [Fact]
    public void Fails_on_malformed_yaml()
    {
        const string malformed = "---\ntitle: [unclosed list\n---\n\nBody.";

        var result = MdxDocumentParser.Parse(malformed);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("not valid YAML"));
    }
}
