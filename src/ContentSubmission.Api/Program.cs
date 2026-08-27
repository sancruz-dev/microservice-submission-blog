using ContentSubmission.Api.Endpoints;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Submissions;
using ContentSubmission.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Phase 2: in-memory only. Real persistence (EF Core + database) lands in Phase 4.
builder.Services.AddSingleton<ISubmissionRepository, InMemorySubmissionRepository>();
builder.Services.AddScoped<SubmissionService>();

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
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

app.MapSubmissionEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in ContentSubmission.Api.Tests.
public partial class Program;
