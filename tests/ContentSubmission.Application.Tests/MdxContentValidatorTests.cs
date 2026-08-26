using ContentSubmission.Application.Submissions.Mdx;

namespace ContentSubmission.Application.Tests;

public class MdxContentValidatorTests
{
    [Fact]
    public void Accepts_plain_markdown_content()
    {
        var errors = MdxContentValidator.Validate("## Heading\n\nSome **text** with a [link](https://example.com).");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_empty_body(string? body)
    {
        var errors = MdxContentValidator.Validate(body);

        Assert.Contains(errors, e => e.Contains("cannot be empty"));
    }

    [Fact]
    public void Rejects_body_over_the_length_limit()
    {
        var oversized = new string('a', MdxContentValidator.MaxBodyLengthChars + 1);

        var errors = MdxContentValidator.Validate(oversized);

        Assert.Contains(errors, e => e.Contains("cannot be longer than"));
    }

    [Theory]
    [InlineData("import Foo from 'bar';\n\nHello")]
    [InlineData("export const x = 1;\n\nHello")]
    public void Rejects_import_and_export_statements(string body)
    {
        var errors = MdxContentValidator.Validate(body);

        Assert.Contains(errors, e => e.Contains("import"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "script")]
    [InlineData("<iframe src='evil.com'></iframe>", "iframe")]
    [InlineData("[click me](javascript:alert(1))", "javascript")]
    [InlineData("<img src=\"x.png\" onerror=\"alert(1)\">", "event handler")]
    public void Rejects_dangerous_markup(string body, string expectedFragment)
    {
        var errors = MdxContentValidator.Validate(body);

        Assert.Contains(errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Accepts_the_blog_s_existing_relative_image_path_convention()
    {
        // sancruzblog-nextjs posts reference images as ../img/posts/<x>/<file> from
        // within posts/ - this is a legitimate existing pattern, not a traversal
        // risk (nothing in this service reads a file from this path).
        var errors = MdxContentValidator.Validate("![screenshot](../img/posts/a/screenshot.png)");

        Assert.Empty(errors);
    }
}
