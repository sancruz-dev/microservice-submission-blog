namespace ContentSubmission.Domain.Tests;

public class SubmissionAuthorTests
{
    [Fact]
    public void Accepts_valid_name_and_email()
    {
        var author = SubmissionAuthor.Create("Jane Doe", "jane@example.com");

        Assert.Equal("Jane Doe", author.Name);
        Assert.Equal("jane@example.com", author.Email);
    }

    [Theory]
    [InlineData(null, "jane@example.com")]
    [InlineData("", "jane@example.com")]
    [InlineData("Jane Doe", null)]
    [InlineData("Jane Doe", "")]
    [InlineData("Jane Doe", "not-an-email")]
    [InlineData("Jane Doe", "jane@")]
    [InlineData("Jane Doe", "@example.com")]
    public void Rejects_missing_or_invalid_fields(string? name, string? email)
    {
        Assert.Throws<ArgumentException>(() => SubmissionAuthor.Create(name, email));
    }
}
