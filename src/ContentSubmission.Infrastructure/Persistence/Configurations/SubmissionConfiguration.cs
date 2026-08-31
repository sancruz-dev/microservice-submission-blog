using System.Text.Json;
using ContentSubmission.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ContentSubmission.Infrastructure.Persistence.Configurations;

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    private static readonly ValueConverter<Slug, string> SlugConverter =
        new(slug => slug.Value, value => Slug.Create(value));

    private static readonly ValueConverter<IReadOnlyList<string>, string> TagsConverter = new(
        tags => JsonSerializer.Serialize(tags, JsonSerializerOptions.Default),
        json => JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Default) ?? new List<string>());

    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("Submissions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Body).IsRequired();
        builder.Property(s => s.RejectionReason).HasMaxLength(1000);
        builder.Property(s => s.GitHubIssueNumber);
        builder.Property(s => s.GitHubPullRequestNumber);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Level)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Slug is a value object wrapping a single validated string - mapped straight
        // to one column via a converter, same idea as Slug.Create() being the only
        // way to produce one in the first place.
        builder.Property(s => s.Slug)
            .HasConversion(SlugConverter)
            .HasMaxLength(100)
            .IsRequired();

        // SubmissionAuthor is mapped as an EF "complex type" (not an owned entity):
        // it's a plain value object with no identity or lifecycle of its own,
        // which is exactly what complex types (unlike owned entities) are for.
        // Submission itself is still materialized via its private parameterless
        // constructor + property setters (see Submission.cs) rather than
        // constructor binding, since EF Core's constructor binding cannot
        // reference a complex/owned property either way.
        builder.ComplexProperty(s => s.Author, author =>
        {
            author.Property(a => a.Name).HasColumnName("AuthorName").HasMaxLength(200).IsRequired();
            author.Property(a => a.Email).HasColumnName("AuthorEmail").HasMaxLength(320).IsRequired();
        });

        // Tags don't need their own table yet - there's no tag-based querying or
        // cross-submission reporting in this phase, so a JSON column is simpler than
        // a join table for a feature that doesn't exist yet.
        var tagsComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
            tags => tags.ToList());

        builder.Property(s => s.Tags)
            .HasConversion(TagsConverter)
            .Metadata.SetValueComparer(tagsComparer);
    }
}
