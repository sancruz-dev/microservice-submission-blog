using ContentSubmission.Application.Abstractions;
using ContentSubmission.Infrastructure.GitHub;
using ContentSubmission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContentSubmission.Api.Tests;

/// <summary>
/// Swaps the real SQL Server-backed repository for the in-memory one, and the
/// real GitHub Issues client for a fake one. These tests exercise HTTP status
/// codes, validation responses and request/response shapes - not persistence
/// (EfSubmissionRepository's job, covered in ContentSubmission.Infrastructure.Tests)
/// and not real calls to the GitHub API, which would need a live token and
/// would create real Issues on every test run. This also means the test suite
/// doesn't need a SQL Server instance or GitHub credentials available (e.g. in
/// CI) to run.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "test-webhook-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not "Development") so Program.cs's auto-migrate-on-startup
        // block is skipped - these tests never touch a real database.
        builder.UseEnvironment("Testing");

        // GitHubWebhookEndpoints reads GitHub:WebhookSecret directly from
        // IConfiguration (it's checked per-request, not bound once at startup
        // like GitHubOptions), so tests need a known value to sign requests with.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:WebhookSecret"] = WebhookSecret,
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ContentSubmissionDbContext>>();
            services.RemoveAll<ContentSubmissionDbContext>();
            services.RemoveAll<ISubmissionRepository>();
            services.AddSingleton<ISubmissionRepository, InMemorySubmissionRepository>();

            services.RemoveAll<IGitHubIssueClient>();
            services.AddSingleton<IGitHubIssueClient, FakeGitHubIssueClient>();

            services.RemoveAll<IGitHubPullRequestClient>();
            services.AddSingleton<IGitHubPullRequestClient, FakeGitHubPullRequestClient>();
        });
    }
}
