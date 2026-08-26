using ContentSubmission.Api.Endpoints;
using ContentSubmission.Application.Abstractions;
using ContentSubmission.Application.Submissions;
using ContentSubmission.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Phase 2: in-memory only. Real persistence (EF Core + database) lands in Phase 4.
builder.Services.AddSingleton<ISubmissionRepository, InMemorySubmissionRepository>();
builder.Services.AddScoped<SubmissionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapSubmissionEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in ContentSubmission.Api.Tests.
public partial class Program;
