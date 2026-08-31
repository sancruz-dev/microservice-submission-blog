using System.Text;
using System.Text.Json;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Submissions.Mdx;
using ContentSubmission.Domain;

namespace ContentSubmission.Infrastructure.GitHub;

/// <summary>
/// Publishes an approved submission into the public blog repo: branch, file
/// commit, Pull Request - never a direct push to the default branch (see
/// docs/decisions/ADR-003 and the PROMPT INICIAL's "never push directly to
/// main" rule). The HttpClient injected here is the same shape as
/// GitHubIssueClient's: base address/auth/version headers configured once in
/// Program.cs, so this class only knows the request/response shapes.
/// </summary>
public sealed class GitHubPullRequestClient(HttpClient httpClient, GitHubOptions options) : IGitHubPullRequestClient
{
    public async Task<int> CreatePullRequestAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        var baseSha = await GetDefaultBranchShaAsync(cancellationToken);
        var branchName = $"submission/{submission.Id}-{submission.Slug.Value}";

        await CreateBranchAsync(branchName, baseSha, cancellationToken);
        await CreateFileAsync(submission, branchName, cancellationToken);

        return await OpenPullRequestAsync(submission, branchName, cancellationToken);
    }

    private async Task<string> GetDefaultBranchShaAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"repos/{options.Owner}/{options.BlogRepo}/git/ref/heads/{options.DefaultBranch}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement.GetProperty("object").GetProperty("sha").GetString()!;
    }

    private async Task CreateBranchAsync(string branchName, string baseSha, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { @ref = $"refs/heads/{branchName}", sha = baseSha });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(
            $"repos/{options.Owner}/{options.BlogRepo}/git/refs",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task CreateFileAsync(Submission submission, string branchName, CancellationToken cancellationToken)
    {
        var mdxContent = MdxDocumentBuilder.Build(submission);
        var payload = JsonSerializer.Serialize(new
        {
            message = $"Add post: {submission.Title}",
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes(mdxContent)),
            branch = branchName,
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PutAsync(
            $"repos/{options.Owner}/{options.BlogRepo}/contents/posts/{submission.Slug.Value}.mdx",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> OpenPullRequestAsync(Submission submission, string branchName, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = submission.Title,
            head = branchName,
            @base = options.DefaultBranch,
            body = $"Automated publication for submission `{submission.Id}`.",
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(
            $"repos/{options.Owner}/{options.BlogRepo}/pulls",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement.GetProperty("number").GetInt32();
    }
}
