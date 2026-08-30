using ContentSubmission.Api.Endpoints;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Submissions;
using ContentSubmission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ContentSubmissionDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SubmissionDb")));
builder.Services.AddScoped<ISubmissionRepository, EfSubmissionRepository>();
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

    // No deployment pipeline exists yet (that's Phase 11), so applying pending
    // migrations automatically on startup keeps local dev friction-free. This is
    // not how migrations should be applied to a real environment.
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ContentSubmissionDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

app.MapSubmissionEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in ContentSubmission.Api.Tests.
public partial class Program;
