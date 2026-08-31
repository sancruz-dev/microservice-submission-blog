using System.Text;
using System.Text.Json;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Domain;

namespace ContentSubmission.Infrastructure.GitHub;

/// <summary>
/// The HttpClient injected here already carries base address, Accept, User-Agent
/// and Authorization headers - configured once at startup in Program.cs, from
/// configuration/User Secrets. This class only knows the GitHub Issues request/
/// response shape, never the token itself.
/// </summary>
public sealed class GitHubIssueClient(HttpClient httpClient, GitHubOptions options) : IGitHubIssueClient
{
    public async Task<int> CreateIssueAsync(Submission submission, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = submission.Title,
            body = BuildBody(submission),
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(
            $"repos/{options.Owner}/{options.CurationRepo}/issues",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        // "number" is the repo-scoped number shown as #42 in the UI and reported
        // by webhooks - not "id", GitHub's internal globally-unique identifier.
        return document.RootElement.GetProperty("number").GetInt32();
    }

    private static string BuildBody(Submission submission) =>
        $"""
        **Autor**: {submission.Author.Name} ({submission.Author.Email})
        **Categoria**: {submission.Category}
        **Nível**: {submission.Level}
        **Submission ID**: {submission.Id}

        ---

        {submission.Body}
        """;
}
