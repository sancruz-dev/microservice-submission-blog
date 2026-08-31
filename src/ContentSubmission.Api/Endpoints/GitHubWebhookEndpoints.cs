using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContentSubmission.Application.Submissions;

namespace ContentSubmission.Api.Endpoints;

public static class GitHubWebhookEndpoints
{
    private const string SignatureHeader = "X-Hub-Signature-256";
    private const string EventHeader = "X-GitHub-Event";
    private const string SignaturePrefix = "sha256=";

    public static void MapGitHubWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhooks/github", HandleWebhook).WithTags("Webhooks");
    }

    private static async Task<IResult> HandleWebhook(
        HttpRequest request,
        SubmissionService submissionService,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        using var bodyStream = new MemoryStream();
        await request.Body.CopyToAsync(bodyStream, cancellationToken);
        var rawBody = bodyStream.ToArray();

        var secret = configuration["GitHub:WebhookSecret"]
            ?? throw new InvalidOperationException("GitHub:WebhookSecret is not configured.");

        // Verified against the raw bytes, not a re-parsed/re-serialized version of
        // them - the signature was computed over exactly what GitHub sent, and
        // deserializing first would change the byte sequence being compared.
        if (!IsSignatureValid(rawBody, request.Headers[SignatureHeader], secret))
        {
            return Results.Unauthorized();
        }

        // This single endpoint receives two different subscriptions - "issues"
        // from the curation repo, "pull_request" from the blog repo - each
        // covering several actions (opened, labeled, reopened, ...) besides the
        // one that matters to each.
        var eventType = request.Headers[EventHeader].ToString();

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;

        if (root.GetProperty("action").GetString() != "closed")
        {
            return Results.Ok();
        }

        switch (eventType)
        {
            case "issues":
            {
                var issue = root.GetProperty("issue");
                var issueNumber = issue.GetProperty("number").GetInt32();
                var stateReason = issue.TryGetProperty("state_reason", out var stateReasonProperty)
                    ? stateReasonProperty.GetString()
                    : null;

                await submissionService.HandleGitHubIssueClosedAsync(issueNumber, stateReason, cancellationToken);
                break;
            }

            case "pull_request":
            {
                var pullRequest = root.GetProperty("pull_request");
                var pullRequestNumber = pullRequest.GetProperty("number").GetInt32();
                var merged = pullRequest.GetProperty("merged").GetBoolean();

                await submissionService.HandlePullRequestClosedAsync(pullRequestNumber, merged, cancellationToken);
                break;
            }
        }

        return Results.Ok();
    }

    private static bool IsSignatureValid(byte[] rawBody, string? signatureHeader, string secret)
    {
        if (signatureHeader is null || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedSignature = SignaturePrefix + Convert.ToHexStringLower(hmac.ComputeHash(rawBody));

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes = Encoding.UTF8.GetBytes(signatureHeader);

        // Constant-time comparison: a plain == would return on the first
        // mismatched byte, leaking (via response timing) how many leading bytes
        // of a guessed signature were correct.
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
