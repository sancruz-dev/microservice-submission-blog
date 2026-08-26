using ContentSubmission.Domain.Exceptions;

namespace ContentSubmission.Domain.Tests;

public class SubmissionTests
{
    private static Submission CreateValidSubmission() => Submission.Create(
        title: "How RabbitMQ works",
        description: "An introduction to messaging.",
        author: SubmissionAuthor.Create("Jane Doe", "jane@example.com"),
        category: "Backend",
        level: SubmissionLevel.Intermediate,
        slug: Slug.Create("how-rabbitmq-works"),
        tags: ["rabbitmq", "messaging"]);

    [Fact]
    public void Create_sets_initial_status_to_received()
    {
        var submission = CreateValidSubmission();

        Assert.Equal(SubmissionStatus.Received, submission.Status);
        Assert.Null(submission.RejectionReason);
    }

    [Fact]
    public void Create_deduplicates_and_trims_tags_case_insensitively()
    {
        var submission = Submission.Create(
            "Title",
            "Description",
            SubmissionAuthor.Create("Jane Doe", "jane@example.com"),
            "Backend",
            SubmissionLevel.Beginner,
            Slug.Create("title"),
            [" rabbitmq ", "RabbitMQ", "messaging"]);

        Assert.Equal(["rabbitmq", "messaging"], submission.Tags);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_title(string? title)
    {
        Assert.Throws<ArgumentException>(() => Submission.Create(
            title,
            "Description",
            SubmissionAuthor.Create("Jane Doe", "jane@example.com"),
            "Backend",
            SubmissionLevel.Beginner,
            Slug.Create("slug"),
            []));
    }

    [Fact]
    public void Full_happy_path_reaches_published()
    {
        var submission = CreateValidSubmission();

        submission.MarkAsValidating();
        submission.MarkAsValidated();
        submission.SendForReview();
        submission.Approve();
        submission.StartPublishing();
        submission.MarkAsPublished();

        Assert.Equal(SubmissionStatus.Published, submission.Status);
    }

    [Fact]
    public void Can_be_rejected_during_validation()
    {
        var submission = CreateValidSubmission();
        submission.MarkAsValidating();

        submission.Reject("Frontmatter is missing required fields.");

        Assert.Equal(SubmissionStatus.Rejected, submission.Status);
        Assert.Equal("Frontmatter is missing required fields.", submission.RejectionReason);
    }

    [Fact]
    public void Can_be_rejected_during_human_review()
    {
        var submission = CreateValidSubmission();
        submission.MarkAsValidating();
        submission.MarkAsValidated();
        submission.SendForReview();

        submission.Reject("Not a good fit for the blog.");

        Assert.Equal(SubmissionStatus.Rejected, submission.Status);
    }

    [Fact]
    public void Reject_requires_a_reason()
    {
        var submission = CreateValidSubmission();
        submission.MarkAsValidating();

        Assert.Throws<ArgumentException>(() => submission.Reject(""));
    }

    [Fact]
    public void Cannot_skip_states_ahead()
    {
        var submission = CreateValidSubmission();

        var ex = Assert.Throws<InvalidSubmissionTransitionException>(submission.SendForReview);

        Assert.Equal(SubmissionStatus.Received, ex.From);
        Assert.Equal(SubmissionStatus.UnderReview, ex.To);
    }

    [Fact]
    public void Cannot_transition_out_of_published()
    {
        var submission = CreateValidSubmission();
        submission.MarkAsValidating();
        submission.MarkAsValidated();
        submission.SendForReview();
        submission.Approve();
        submission.StartPublishing();
        submission.MarkAsPublished();

        Assert.Throws<InvalidSubmissionTransitionException>(submission.MarkAsValidating);
    }

    [Fact]
    public void Cannot_transition_out_of_rejected()
    {
        var submission = CreateValidSubmission();
        submission.MarkAsValidating();
        submission.Reject("bad content");

        Assert.Throws<InvalidSubmissionTransitionException>(submission.MarkAsValidated);
    }

    [Fact]
    public void Cannot_approve_before_review()
    {
        var submission = CreateValidSubmission();
        submission.MarkAsValidating();
        submission.MarkAsValidated();

        Assert.Throws<InvalidSubmissionTransitionException>(submission.Approve);
    }
}
