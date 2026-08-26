namespace ContentSubmission.Domain.Tests;

public class SlugTests
{
    [Theory]
    [InlineData("how-rabbitmq-works")]
    [InlineData("a")]
    [InlineData("post-123")]
    public void Accepts_valid_kebab_case_slugs(string candidate)
    {
        var slug = Slug.Create(candidate);

        Assert.Equal(candidate, slug.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Has-Uppercase")]
    [InlineData("has_underscore")]
    [InlineData("has space")]
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphen")]
    // Path traversal / filesystem-escape attempts - these must never reach a file path.
    [InlineData("../../etc/passwd")]
    [InlineData("..%2f..%2fetc")]
    [InlineData("posts/../../secrets")]
    [InlineData("posts/nested")]
    [InlineData("C:\\windows\\system32")]
    public void Rejects_invalid_or_unsafe_slugs(string? candidate)
    {
        Assert.Throws<ArgumentException>(() => Slug.Create(candidate));
    }
}
