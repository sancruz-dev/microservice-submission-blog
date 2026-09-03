using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ContentSubmission.Api.Contracts;
using ContentSubmission.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ContentSubmission.Api.Tests;

public class GitHubWebhookEndpointsTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // GET /submissions was removed (ADR-006), so these tests read state back
    // directly from the in-memory repository the factory injects, instead of
    // through the API.
    private ISubmissionRepository Repository => factory.Services.CreateScope().ServiceProvider
        .GetRequiredService<ISubmissionRepository>();

    private static StringContent SignedJson(string json, string secret = TestWebApplicationFactory.WebhookSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = "sha256=" + Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(json)));

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", signature);
        return content;
    }

    private static string ClosedIssuePayload(int issueNumber, string stateReason) =>
        $$"""
        {
          "action": "closed",
          "issue": {
            "number": {{issueNumber}},
            "state": "closed",
            "state_reason": "{{stateReason}}"
          }
        }
        """;

    private static string ClosedPullRequestPayload(int pullRequestNumber, bool merged) =>
        $$"""
        {
          "action": "closed",
          "pull_request": {
            "number": {{pullRequestNumber}},
            "merged": {{(merged ? "true" : "false")}}
          }
        }
        """;

    private static HttpRequestMessage WebhookRequest(string githubEvent, string payload) => new(HttpMethod.Post, "/webhooks/github")
    {
        Content = SignedJson(payload),
        Headers = { { "X-GitHub-Event", githubEvent } },
    };

    private async Task<int> CreateUnderReviewSubmissionAsync()
    {
        const string mdx = """
            ---
            title: x
            description: y
            slug: x
            author: a
            category: b
            level: Beginner
            ---

            Body.
            """;

        var content = new MultipartFormDataContent
        {
            { new StringContent("a@example.com"), "authorEmail" },
        };
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(mdx));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", "x.mdx");

        var response = await _client.PostAsync("/submissions", content);
        var body = await response.Content.ReadFromJsonAsync<SubmissionResponse>();
        return body!.GitHubIssueNumber!.Value;
    }

    private async Task<SubmissionResponse> GetByIssueAsync(int issueNumber)
    {
        var all = await Repository.GetAllAsync();
        return SubmissionResponse.FromDomain(all.Single(s => s.GitHubIssueNumber == issueNumber));
    }

    private async Task<SubmissionResponse> GetByPullRequestAsync(int pullRequestNumber)
    {
        var all = await Repository.GetAllAsync();
        return SubmissionResponse.FromDomain(all.Single(s => s.GitHubPullRequestNumber == pullRequestNumber));
    }

    [Fact]
    public async Task POST_approves_and_opens_a_pull_request_when_closed_as_completed()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();

        var response = await _client.SendAsync(WebhookRequest("issues", ClosedIssuePayload(issueNumber, "completed")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Approve() -> create PR -> StartPublishing() all happen in the same
        // webhook delivery (see SubmissionService.HandleGitHubIssueClosedAsync) -
        // "Approved" is never the status actually persisted, only "Publishing".
        var updated = await GetByIssueAsync(issueNumber);
        Assert.Equal("Publishing", updated.Status);
        Assert.NotNull(updated.GitHubPullRequestNumber);
    }

    [Fact]
    public async Task POST_rejects_the_submission_when_closed_as_not_planned()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();

        var response = await _client.SendAsync(WebhookRequest("issues", ClosedIssuePayload(issueNumber, "not_planned")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Rejected", (await GetByIssueAsync(issueNumber)).Status);
    }

    [Fact]
    public async Task POST_marks_as_published_when_pull_request_is_merged()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();
        await _client.SendAsync(WebhookRequest("issues", ClosedIssuePayload(issueNumber, "completed")));
        var pullRequestNumber = (await GetByIssueAsync(issueNumber)).GitHubPullRequestNumber!.Value;

        var response = await _client.SendAsync(
            WebhookRequest("pull_request", ClosedPullRequestPayload(pullRequestNumber, merged: true)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Published", (await GetByPullRequestAsync(pullRequestNumber)).Status);
    }

    [Fact]
    public async Task POST_does_not_publish_when_pull_request_is_closed_without_merging()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();
        await _client.SendAsync(WebhookRequest("issues", ClosedIssuePayload(issueNumber, "completed")));
        var pullRequestNumber = (await GetByIssueAsync(issueNumber)).GitHubPullRequestNumber!.Value;

        var response = await _client.SendAsync(
            WebhookRequest("pull_request", ClosedPullRequestPayload(pullRequestNumber, merged: false)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Publishing", (await GetByPullRequestAsync(pullRequestNumber)).Status);
    }

    [Fact]
    public async Task POST_returns_401_for_an_invalid_signature()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = SignedJson(ClosedIssuePayload(issueNumber, "completed"), secret: "wrong-secret"),
        };
        request.Headers.Add("X-GitHub-Event", "issues");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // The submission must be untouched - a forged/incorrect signature never
        // reaches the point of applying a state transition.
        Assert.Equal("UnderReview", (await GetByIssueAsync(issueNumber)).Status);
    }

    [Fact]
    public async Task POST_ignores_events_other_than_issues_and_pull_request()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();

        var response = await _client.SendAsync(WebhookRequest("ping", ClosedIssuePayload(issueNumber, "completed")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("UnderReview", (await GetByIssueAsync(issueNumber)).Status);
    }

    [Fact]
    public async Task POST_is_idempotent_for_a_redelivered_webhook()
    {
        var issueNumber = await CreateUnderReviewSubmissionAsync();

        async Task<HttpStatusCode> DeliverAsync() =>
            (await _client.SendAsync(WebhookRequest("issues", ClosedIssuePayload(issueNumber, "completed")))).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await DeliverAsync());
        // GitHub redelivering the same webhook must not turn into a 500 from
        // InvalidSubmissionTransitionException (or a second Pull Request) on
        // the second delivery.
        Assert.Equal(HttpStatusCode.OK, await DeliverAsync());
        Assert.Equal("Publishing", (await GetByIssueAsync(issueNumber)).Status);
    }
}
