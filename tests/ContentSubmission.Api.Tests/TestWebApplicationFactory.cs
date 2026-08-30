using ContentSubmission.Application.Abstractions;
using ContentSubmission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContentSubmission.Api.Tests;

/// <summary>
/// Swaps the real SQL Server-backed repository for the in-memory one. These
/// tests exercise HTTP status codes, validation responses and request/response
/// shapes - not persistence, which is EfSubmissionRepository's job and is
/// covered separately in ContentSubmission.Infrastructure.Tests. This also
/// means the test suite doesn't need a SQL Server instance available (e.g. in
/// CI) to run.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not "Development") so Program.cs's auto-migrate-on-startup
        // block is skipped - these tests never touch a real database.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ContentSubmissionDbContext>>();
            services.RemoveAll<ContentSubmissionDbContext>();
            services.RemoveAll<ISubmissionRepository>();
            services.AddSingleton<ISubmissionRepository, InMemorySubmissionRepository>();
        });
    }
}
