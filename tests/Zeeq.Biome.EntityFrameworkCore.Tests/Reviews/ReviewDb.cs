using Microsoft.EntityFrameworkCore;

namespace Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

internal sealed class ReviewDb(DbContextOptions<ReviewDb> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<CodeRepository> Repositories => Set<CodeRepository>();
    public DbSet<PullRequestRecord> PullRequests => Set<PullRequestRecord>();
    public DbSet<CodeReviewRecord> Reviews => Set<CodeReviewRecord>();
    public DbSet<ReviewQueueEntry> ReviewQueue => Set<ReviewQueueEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder
            .Entity<PullRequestRecord>()
            .HasIndex("RepositoryId", nameof(PullRequestRecord.Number))
            .IsUnique();
}
