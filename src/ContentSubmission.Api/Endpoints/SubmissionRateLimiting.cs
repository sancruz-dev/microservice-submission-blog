using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ContentSubmission.Api.Endpoints;

/// <summary>
/// POST /submissions is public and unauthenticated by design (ADR-003 - the
/// submission form is the product), but each request creates a GitHub Issue
/// and writes to the database. Without a cap, a simple script can flood the
/// curation repo with Issues and burn through the GitHub API rate limit and
/// the Azure SQL serverless free tier. Fixed window per client IP is enough
/// for a personal blog's traffic; there's no need for a distributed limiter
/// since this runs as a single Container App instance.
/// </summary>
public static class SubmissionRateLimiting
{
    public const string PolicyName = "submissions-post";

    public static void AddSubmissionRateLimiting(this IServiceCollection services, bool isTesting = false)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // WebApplicationFactory-driven tests (ContentSubmission.Api.Tests) share
            // one HttpClient/TestServer connection across many POSTs per test class,
            // which would all land in the same IP partition and trip the limiter -
            // a high limit here keeps the real policy shape (so 429 itself is still
            // testable) without every other test failing on request count alone.
            var permitLimit = isTesting ? 10_000 : 5;

            options.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });
    }
}
