using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Submissions;
using ContentSubmission.Domain;

namespace ContentSubmission.Application.Tests;

public class SubmissionServiceTests
{
    private static Submission CreateUnderReviewSubmission(int gitHubIssueNumber = 42)
    {
        var submission = Submission.Create(
            "Title",
            "Description",
            SubmissionAuthor.Create("Jane Doe", "jane@example.com"),
            "Backend",
            SubmissionLevel.Beginner,
            Slug.Create("title"),
            [],
            "Body text.");

        submission.MarkAsValidating();
        submission.MarkAsValidated();
        submission.SendForReview(gitHubIssueNumber);

        return submission;
    }

    private static Submission CreatePublishingSubmission(int gitHubPullRequestNumber = 100)
    {
        var submission = CreateUnderReviewSubmission();
        submission.Approve();
        submission.StartPublishing(gitHubPullRequestNumber);
        return submission;
    }

    private static SubmissionService CreateService(
        ISubmissionRepository repository,
        IGitHubIssueClient? gitHubIssueClient = null,
        IGitHubPullRequestClient? gitHubPullRequestClient = null) =>
        new(
            repository,
            gitHubIssueClient ?? new NotUsedGitHubIssueClient(),
            gitHubPullRequestClient ?? new NotUsedGitHubPullRequestClient(),
            new SubmissionMetrics());

    private const string ValidMdx = """
        ---
        title: Title
        description: Description
        slug: title
        author: Jane Doe
        category: Backend
        level: Beginner
        ---

        Body text.
        """;

    [Fact]
    public async Task CreateAsync_persists_before_calling_GitHub()
    {
        var repository = new FakeSubmissionRepository([]);
        var service = CreateService(
            repository,
            gitHubIssueClient: new ThrowingGitHubIssueClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateSubmissionInput(ValidMdx, "jane@example.com")));

        // ADR-006: the row must already be saved (Validated) by the time the
        // GitHub call is attempted, so a failure here - like the one that
        // actually happened in production - leaves a recoverable row in our
        // own database instead of nothing at all.
        var saved = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(SubmissionStatus.Validated, saved.Status);
        Assert.Null(saved.GitHubIssueNumber);
    }

    [Fact]
    public async Task CreateAsync_moves_to_UnderReview_after_the_issue_is_created()
    {
        var repository = new FakeSubmissionRepository([]);
        var service = CreateService(repository, gitHubIssueClient: new StubGitHubIssueClient(7));

        var submission = await service.CreateAsync(new CreateSubmissionInput(ValidMdx, "jane@example.com"));

        Assert.Equal(SubmissionStatus.UnderReview, submission.Status);
        Assert.Equal(7, submission.GitHubIssueNumber);

        var saved = Assert.Single(await repository.GetAllAsync());
        Assert.Equal(SubmissionStatus.UnderReview, saved.Status);
        Assert.Equal(7, saved.GitHubIssueNumber);
    }

    [Fact]
    public async Task HandleGitHubIssueClosedAsync_approves_and_opens_a_pull_request_on_completed()
    {
        var submission = CreateUnderReviewSubmission();
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository, gitHubPullRequestClient: new StubGitHubPullRequestClient(101));

        await service.HandleGitHubIssueClosedAsync(submission.GitHubIssueNumber!.Value, "completed");

        // Approve() -> create PR -> StartPublishing() all happen in the same call
        // (see SubmissionService.HandleGitHubIssueClosedAsync) - "Approved" is
        // never the state actually persisted, only the on-the-way-through one.
        Assert.Equal(SubmissionStatus.Publishing, submission.Status);
        Assert.Equal(101, submission.GitHubPullRequestNumber);
    }

    [Fact]
    public async Task HandleGitHubIssueClosedAsync_rejects_on_not_planned()
    {
        var submission = CreateUnderReviewSubmission();
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository);

        await service.HandleGitHubIssueClosedAsync(submission.GitHubIssueNumber!.Value, "not_planned");

        Assert.Equal(SubmissionStatus.Rejected, submission.Status);
        Assert.NotNull(submission.RejectionReason);
    }

    [Fact]
    public async Task HandleGitHubIssueClosedAsync_is_a_noop_for_an_unknown_issue_number()
    {
        var submission = CreateUnderReviewSubmission(gitHubIssueNumber: 1);
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository);

        await service.HandleGitHubIssueClosedAsync(gitHubIssueNumber: 999, "completed");

        Assert.Equal(SubmissionStatus.UnderReview, submission.Status);
    }

    [Fact]
    public async Task HandleGitHubIssueClosedAsync_ignores_an_unrecognized_state_reason()
    {
        var submission = CreateUnderReviewSubmission();
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository);

        await service.HandleGitHubIssueClosedAsync(submission.GitHubIssueNumber!.Value, stateReason: null);

        Assert.Equal(SubmissionStatus.UnderReview, submission.Status);
    }

    [Fact]
    public async Task HandleGitHubIssueClosedAsync_is_idempotent_for_a_redelivered_webhook()
    {
        var submission = CreateUnderReviewSubmission();
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository, gitHubPullRequestClient: new StubGitHubPullRequestClient(101));

        await service.HandleGitHubIssueClosedAsync(submission.GitHubIssueNumber!.Value, "completed");
        // GitHub can redeliver the same webhook (timeout, retry) - a second
        // delivery for a submission already past UnderReview must not throw
        // InvalidSubmissionTransitionException (or open a second PR).
        var exception = await Record.ExceptionAsync(() =>
            service.HandleGitHubIssueClosedAsync(submission.GitHubIssueNumber!.Value, "completed"));

        Assert.Null(exception);
        Assert.Equal(SubmissionStatus.Publishing, submission.Status);
    }

    [Fact]
    public async Task HandlePullRequestClosedAsync_marks_as_published_when_merged()
    {
        var submission = CreatePublishingSubmission();
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository);

        await service.HandlePullRequestClosedAsync(submission.GitHubPullRequestNumber!.Value, merged: true);

        Assert.Equal(SubmissionStatus.Published, submission.Status);
    }

    [Fact]
    public async Task HandlePullRequestClosedAsync_is_a_noop_when_closed_without_merging()
    {
        var submission = CreatePublishingSubmission();
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository);

        await service.HandlePullRequestClosedAsync(submission.GitHubPullRequestNumber!.Value, merged: false);

        Assert.Equal(SubmissionStatus.Publishing, submission.Status);
    }

    [Fact]
    public async Task HandlePullRequestClosedAsync_is_a_noop_for_an_unknown_pull_request_number()
    {
        var submission = CreatePublishingSubmission(gitHubPullRequestNumber: 1);
        var repository = new FakeSubmissionRepository([submission]);
        var service = CreateService(repository);

        await service.HandlePullRequestClosedAsync(gitHubPullRequestNumber: 999, merged: true);

        Assert.Equal(SubmissionStatus.Publishing, submission.Status);
    }

    private sealed class NotUsedGitHubIssueClient : IGitHubIssueClient
    {
        public Task<int> CreateIssueAsync(Submission submission, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never create an Issue in this test.");
    }

    private sealed class NotUsedGitHubPullRequestClient : IGitHubPullRequestClient
    {
        public Task<int> CreatePullRequestAsync(Submission submission, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should never create a Pull Request in this test.");
    }

    private sealed class StubGitHubIssueClient(int issueNumber) : IGitHubIssueClient
    {
        public Task<int> CreateIssueAsync(Submission submission, CancellationToken cancellationToken = default) =>
            Task.FromResult(issueNumber);
    }

    private sealed class ThrowingGitHubIssueClient : IGitHubIssueClient
    {
        public Task<int> CreateIssueAsync(Submission submission, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated GitHub outage.");
    }

    private sealed class StubGitHubPullRequestClient(int pullRequestNumber) : IGitHubPullRequestClient
    {
        public Task<int> CreatePullRequestAsync(Submission submission, CancellationToken cancellationToken = default) =>
            Task.FromResult(pullRequestNumber);
    }

    private sealed class FakeSubmissionRepository(IEnumerable<Submission> seed) : ISubmissionRepository
    {
        private readonly Dictionary<Guid, Submission> _submissions = seed.ToDictionary(s => s.Id);

        public Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
        {
            _submissions[submission.Id] = submission;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Submission submission, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _submissions.TryGetValue(id, out var submission);
            return Task.FromResult(submission);
        }

        public Task<Submission?> GetByGitHubIssueNumberAsync(int gitHubIssueNumber, CancellationToken cancellationToken = default)
        {
            var submission = _submissions.Values.FirstOrDefault(s => s.GitHubIssueNumber == gitHubIssueNumber);
            return Task.FromResult(submission);
        }

        public Task<Submission?> GetByGitHubPullRequestNumberAsync(int gitHubPullRequestNumber, CancellationToken cancellationToken = default)
        {
            var submission = _submissions.Values.FirstOrDefault(s => s.GitHubPullRequestNumber == gitHubPullRequestNumber);
            return Task.FromResult(submission);
        }

        public Task<IReadOnlyList<Submission>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Submission> all = [.. _submissions.Values];
            return Task.FromResult(all);
        }
    }
}
