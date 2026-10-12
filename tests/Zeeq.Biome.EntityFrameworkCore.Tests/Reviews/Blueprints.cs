using Zeeq.Biome.EntityFrameworkCore;

namespace Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

internal static class Blueprints
{
    internal static readonly Sequence<int> Numbers = Sequence
        .Of(1, n => n + 1)
        .Per<CodeRepository>();

    // Keys are database-generated, so children point at parents through navigations and EF fixes up the FKs.
    internal static readonly Blueprint<ReviewSeed, CodeRepository> Repository = Blueprint.For<
        ReviewSeed,
        CodeRepository
    >(ctx =>
        new()
        {
            Organization = ctx.Seed.Organization,
            Name = ctx.NewId("repo"),
            CreatedAt = ctx.Now,
        }
    );
    internal static readonly ChildBlueprint<
        ReviewSeed,
        CodeRepository,
        PullRequestRecord
    > PullRequest = Blueprint
        .ChildOf<ReviewSeed, CodeRepository, PullRequestRecord>(
            (repo, ctx) => new() { Repository = repo, CreatedAt = ctx.Now }
        )
        .Derive(pr => pr.Number, Numbers)
        .Derive(pr => pr.Title, (pr, _) => $"PR {pr.Number}");
    internal static readonly ChildBlueprint<
        ReviewSeed,
        PullRequestRecord,
        CodeReviewRecord
    > Review = Blueprint.ChildOf<ReviewSeed, PullRequestRecord, CodeReviewRecord>(
        (pr, ctx) => new() { PullRequest = pr, CreatedAt = ctx.Ago(TimeSpan.FromMinutes(5)) }
    );
    internal static readonly InputBlueprint<
        ReviewSeed,
        OrganizationInput,
        OrganizationGraph
    > Organization = Blueprint.FromInput<ReviewSeed, OrganizationInput, OrganizationGraph>(
        () => new(),
        (input, ctx) =>
        {
            var org = new Organization { Id = ctx.NewId("org"), Name = input.Name };
            return new(
                org,
                new()
                {
                    Organization = org,
                    User = ctx.Seed.Owner,
                    IsDefault = input.IsDefault,
                }
            );
        }
    );
}

internal static class PullRequests
{
    internal static readonly Action<PullRequestRecord> Closed = pr => pr.Closed = true;
}

internal static class TestBiome
{
    // Open pull requests are queued for review by a coordinator row, as an app's own save rules might.
    private static readonly BiomeOptions<ReviewSeed> Options = new BiomeOptions<ReviewSeed>()
        .SeedEntities(seed => [seed.Owner, seed.Organization])
        .BeforeSave(
            (save, _) =>
            {
                save.Entities.AddRange([
                    .. save
                        .Entities.OfType<PullRequestRecord>()
                        .Where(pr => !pr.Closed)
                        .Select(pr => new ReviewQueueEntry { PullRequest = pr }),
                ]);
                return ValueTask.CompletedTask;
            }
        );

    internal static BiomeBuilder<ReviewSeed, ReviewSeed> Start(
        ReviewDb db,
        out ReviewSeed seed,
        TimeProvider? time = null
    )
    {
        seed = new(
            new() { Id = $"org_{Guid.CreateVersion7():N}" },
            new() { Id = $"user_{Guid.CreateVersion7():N}" }
        );
        return Biome.Start(seed, Options, db.AsBiomeStore(), time);
    }
}
