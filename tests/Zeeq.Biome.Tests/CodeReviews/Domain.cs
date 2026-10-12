using Zeeq.Biome;

namespace Zeeq.Biome.Tests.CodeReviews;

internal sealed record CodeReviewSeed(Organization Organization, User Owner);

internal sealed class Organization
{
    public string Id { get; init; } = "org";
}

internal sealed class User
{
    public string Id { get; init; } = "owner";
}

internal sealed class CodeRepository
{
    public string Id { get; init; } = "";
    public Organization Organization { get; init; } = null!;
    public string Name { get; set; } = "repository";
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class PullRequestRecord
{
    public string Id { get; init; } = "";
    public CodeRepository Repository { get; init; } = null!;
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Branch { get; set; }
    public bool Closed { get; set; }
    public bool Merged { get; set; }
}

internal sealed class CodeReviewRecord
{
    public string Id { get; init; } = "";
    public PullRequestRecord PullRequest { get; init; } = null!;
    public CodeRepository Repository { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class ActiveCodeReviewLock
{
    public CodeReviewRecord Review { get; init; } = null!;
    public CodeRepository Repository { get; init; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
}

internal static class Blueprints
{
    // Scope is declared on the sequence: Under(repo) and blueprint variants share its progression.
    internal static readonly Sequence<int> Numbers = Sequence
        .Of(1, n => n + 1)
        .Per<CodeRepository>();

    // Factory sets identity/time and seed relationships; scenario customizers choose the interesting facts.
    internal static readonly Blueprint<CodeReviewSeed, CodeRepository> Repository = Blueprint.For<
        CodeReviewSeed,
        CodeRepository
    >(ctx =>
        new()
        {
            Id = ctx.NewId("repo"),
            Organization = ctx.Seed.Organization,
            CreatedAt = ctx.Now,
        }
    );

    // Leave Number unset; derive it first, then Title and Branch from its final value.
    internal static readonly ChildBlueprint<
        CodeReviewSeed,
        CodeRepository,
        PullRequestRecord
    > PullRequest = Blueprint
        .ChildOf<CodeReviewSeed, CodeRepository, PullRequestRecord>(
            (repo, ctx) => new() { Id = ctx.NewId("pr"), Repository = repo }
        )
        .Derive(pr => pr.Number, Numbers)
        .Derive(pr => pr.Title, (pr, _) => $"PR {pr.Number}")
        .Derive(pr => pr.Branch, (pr, _) => $"feature/{pr.Number}");

    // Wire the typed PR parent and walk recorded ancestors to the repository.
    internal static readonly ChildBlueprint<
        CodeReviewSeed,
        PullRequestRecord,
        CodeReviewRecord
    > Review = Blueprint.ChildOf<CodeReviewSeed, PullRequestRecord, CodeReviewRecord>(
        (pr, ctx) =>
            new()
            {
                Id = ctx.NewId("review"),
                PullRequest = pr,
                Repository = ctx.Ancestor<CodeRepository>(),
                CreatedAt = ctx.Now,
            }
    );

    // Use rounded session FromNow for expiry rather than a second wall clock.
    internal static readonly ChildBlueprint<
        CodeReviewSeed,
        CodeReviewRecord,
        ActiveCodeReviewLock
    > Lock = Blueprint.ChildOf<CodeReviewSeed, CodeReviewRecord, ActiveCodeReviewLock>(
        (review, ctx) =>
            new()
            {
                Review = review,
                Repository = ctx.Ancestor<CodeRepository>(),
                ExpiresAt = ctx.FromNow(TimeSpan.FromMinutes(5)),
            }
    );
}

internal static class PullRequests
{
    // A plain delegate can be used directly as a customizer or composed with +.
    internal static readonly Action<PullRequestRecord> Closed = pr => pr.Closed = true;

    // Compose with Closed using + to change one output rather than create another.
    internal static readonly Action<PullRequestRecord> Merged = pr => pr.Merged = true;
}

internal static class TestBiome
{
    private static readonly BiomeOptions<CodeReviewSeed> Options =
        new BiomeOptions<CodeReviewSeed>().SeedEntities(seed => [seed.Owner, seed.Organization]);

    // Domain wrapper supplies a fresh seed/session while sharing frozen configuration.
    internal static BiomeBuilder<CodeReviewSeed, CodeReviewSeed> Start(
        out CodeReviewSeed seed,
        IBiomeStore? store = null,
        TimeProvider? time = null
    )
    {
        seed = new(new(), new());
        return Biome.Start(seed, Options, store, time);
    }
}
