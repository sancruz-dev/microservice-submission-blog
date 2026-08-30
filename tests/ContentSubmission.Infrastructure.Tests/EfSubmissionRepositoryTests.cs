using ContentSubmission.Domain;
using ContentSubmission.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ContentSubmission.Infrastructure.Tests;

/// <summary>
/// Exercises the real EF Core mapping (constructor binding, the Slug/Tags value
/// converters, the owned SubmissionAuthor) against SQLite instead of SQL Server.
/// SQLite enforces real SQL and applies migrations like a real relational
/// database, which is what makes it useful here - it just doesn't require a
/// SQL Server instance to be available (e.g. in CI). The mapping is the same
/// either way; this is not a substitute for verifying against SQL Server
/// itself, which was done manually against the project's actual database.
/// </summary>
public sealed class EfSubmissionRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ContentSubmissionDbContext _dbContext;

    public EfSubmissionRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ContentSubmissionDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ContentSubmissionDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private static Submission CreateValidSubmission() => Submission.Create(
        "How RabbitMQ works",
        "An introduction to messaging.",
        SubmissionAuthor.Create("Jane Doe", "jane@example.com"),
        "Backend",
        SubmissionLevel.Intermediate,
        Slug.Create("how-rabbitmq-works"),
        ["rabbitmq", "messaging"],
        "RabbitMQ is a message broker.");

    [Fact]
    public async Task Round_trips_a_submission_through_a_real_database()
    {
        var repository = new EfSubmissionRepository(_dbContext);
        var submission = CreateValidSubmission();

        await repository.AddAsync(submission);

        // A fresh context, so this reads back from the database rather than the
        // change tracker's in-memory identity map.
        await using var freshContext = new ContentSubmissionDbContext(
            new DbContextOptionsBuilder<ContentSubmissionDbContext>().UseSqlite(_connection).Options);
        var reloaded = await new EfSubmissionRepository(freshContext).GetByIdAsync(submission.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(submission.Title, reloaded!.Title);
        Assert.Equal(submission.Slug, reloaded.Slug);
        Assert.Equal(submission.Author.Name, reloaded.Author.Name);
        Assert.Equal(submission.Author.Email, reloaded.Author.Email);
        Assert.Equal(submission.Tags, reloaded.Tags);
        Assert.Equal(submission.Status, reloaded.Status);
    }

    [Fact]
    public async Task Persists_state_transitions()
    {
        var repository = new EfSubmissionRepository(_dbContext);
        var submission = CreateValidSubmission();
        await repository.AddAsync(submission);

        submission.MarkAsValidating();
        submission.Reject("Not a good fit.");
        await _dbContext.SaveChangesAsync();

        await using var freshContext = new ContentSubmissionDbContext(
            new DbContextOptionsBuilder<ContentSubmissionDbContext>().UseSqlite(_connection).Options);
        var reloaded = await new EfSubmissionRepository(freshContext).GetByIdAsync(submission.Id);

        Assert.Equal(SubmissionStatus.Rejected, reloaded!.Status);
        Assert.Equal("Not a good fit.", reloaded.RejectionReason);
    }

    [Fact]
    public async Task Persists_and_reloads_multiple_submissions()
    {
        // Queries the DbContext directly rather than through
        // EfSubmissionRepository.GetAllAsync(), which orders by CreatedAt server
        // side: SQLite's EF provider refuses to translate ORDER BY on
        // DateTimeOffset at all (unlike SQL Server, which handles it natively).
        // That's a provider gap, not something to route around by changing
        // production code - what this test needs to verify is that multiple
        // rows round-trip correctly, which doesn't require that ordering.
        var repository = new EfSubmissionRepository(_dbContext);
        var older = Submission.Create(
            "Older post", "Description", SubmissionAuthor.Create("A", "a@example.com"), "Backend",
            SubmissionLevel.Beginner, Slug.Create("older-post"), [], "Body.",
            DateTimeOffset.UtcNow.AddDays(-1));
        var newer = Submission.Create(
            "Newer post", "Description", SubmissionAuthor.Create("B", "b@example.com"), "Backend",
            SubmissionLevel.Beginner, Slug.Create("newer-post"), [], "Body.",
            DateTimeOffset.UtcNow);

        await repository.AddAsync(older);
        await repository.AddAsync(newer);

        var all = await _dbContext.Submissions.AsNoTracking().ToListAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(older.CreatedAt, all.Single(s => s.Slug.Value == "older-post").CreatedAt);
        Assert.Equal(newer.CreatedAt, all.Single(s => s.Slug.Value == "newer-post").CreatedAt);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_id()
    {
        var repository = new EfSubmissionRepository(_dbContext);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
