using ContentSubmission.Domain;
using Microsoft.EntityFrameworkCore;

namespace ContentSubmission.Infrastructure.Persistence;

public sealed class ContentSubmissionDbContext(DbContextOptions<ContentSubmissionDbContext> options)
    : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentSubmissionDbContext).Assembly);
    }
}
