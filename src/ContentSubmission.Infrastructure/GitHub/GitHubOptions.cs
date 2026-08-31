namespace ContentSubmission.Infrastructure.GitHub;

/// <summary>
/// Which repositories Issues/Pull Requests are created in - not secret, unlike
/// the auth token used to configure the HttpClient itself (see Program.cs).
/// Owner is shared: both the curation repo and the blog repo live under the
/// same account.
/// </summary>
public sealed record GitHubOptions(string Owner, string CurationRepo, string BlogRepo, string DefaultBranch);
