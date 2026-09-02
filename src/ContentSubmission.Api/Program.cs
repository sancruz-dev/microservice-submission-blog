using System.Net.Http.Headers;
using ContentSubmission.Api.Endpoints;
using ContentSubmission.Api.Middleware;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Submissions;
using ContentSubmission.Infrastructure.GitHub;
using ContentSubmission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ContentSubmissionDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SubmissionDb")));
builder.Services.AddScoped<ISubmissionRepository, EfSubmissionRepository>();
builder.Services.AddScoped<SubmissionService>();

// Owner/CurationRepo/BlogRepo/DefaultBranch aren't secret (appsettings.Development.json);
// the token is (User Secrets locally, an env var in Docker - see
// docs/local-development.md and docs/decisions/ADR-003). Both HttpClients carry
// auth/version headers set once here, so neither GitHub client class itself
// ever touches the token.
//
// Registered as a factory (not a pre-built instance) so config is only read -
// and only required - the first time something actually resolves it. Tests
// swap in the Fake* clients, which never depend on GitHubOptions, so the
// Testing environment doesn't need these keys configured at all.
builder.Services.AddSingleton(_ => new GitHubOptions(
    builder.Configuration["GitHub:Owner"]
        ?? throw new InvalidOperationException("GitHub:Owner is not configured."),
    builder.Configuration["GitHub:CurationRepo"]
        ?? throw new InvalidOperationException("GitHub:CurationRepo is not configured."),
    builder.Configuration["GitHub:BlogRepo"]
        ?? throw new InvalidOperationException("GitHub:BlogRepo is not configured."),
    builder.Configuration["GitHub:DefaultBranch"]
        ?? throw new InvalidOperationException("GitHub:DefaultBranch is not configured.")));

void ConfigureGitHubClient(HttpClient client)
{
    var token = builder.Configuration["GitHub:Token"]
        ?? throw new InvalidOperationException("GitHub:Token is not configured.");

    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("content-submission-service", "1.0"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}

builder.Services.AddHttpClient<IGitHubIssueClient, GitHubIssueClient>(ConfigureGitHubClient);
builder.Services.AddHttpClient<IGitHubPullRequestClient, GitHubPullRequestClient>(ConfigureGitHubClient);

// Allows the Next.js frontend (a different origin) to call this API directly
// from the browser. Origins come from config, not hardcoded, since the
// allowed frontend origin(s) will differ between local dev and production.
const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // No deployment pipeline exists yet (that's Phase 11), so applying pending
    // migrations automatically on startup keeps local dev friction-free. This is
    // not how migrations should be applied to a real environment.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ContentSubmissionDbContext>().Database.MigrateAsync();
}

// Only in Development: in Azure Container Apps, TLS terminates at the
// ingress and the container itself is reached over plain HTTP
// (ASPNETCORE_URLS=http://+:8080, see Dockerfile) - forcing a redirect here
// too would fight the proxy instead of the app's own dev server.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors(FrontendCorsPolicy);

app.MapSubmissionEndpoints();
app.MapGitHubWebhookEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in ContentSubmission.Api.Tests.
public partial class Program;
