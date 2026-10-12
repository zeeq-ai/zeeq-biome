using Zeeq.Biome;

namespace Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

internal sealed record ReviewSeed(Organization Organization, User Owner);

internal sealed class Organization
{
    public string Id { get; init; } = "";
    public string Name { get; set; } = "organization";
}

internal sealed class User
{
    public string Id { get; init; } = "";
}

internal sealed class Membership
{
    public int Id { get; set; }
    public User User { get; init; } = null!;
    public Organization Organization { get; init; } = null!;
    public bool IsDefault { get; init; }
}

internal sealed class CodeRepository
{
    public int Id { get; set; }
    public Organization Organization { get; init; } = null!;
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class PullRequestRecord
{
    public int Id { get; set; }
    public CodeRepository Repository { get; init; } = null!;
    public int Number { get; set; }
    public string? Title { get; set; }
    public bool Closed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CodeReviewRecord
{
    public int Id { get; set; }
    public PullRequestRecord PullRequest { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class ReviewQueueEntry
{
    public int Id { get; set; }
    public PullRequestRecord PullRequest { get; init; } = null!;
}

internal sealed class OrganizationInput
{
    public string Name { get; set; } = "organization";
    public bool IsDefault { get; set; }
}

internal sealed record OrganizationGraph(Organization Organization, Membership Membership)
    : IBiomeAggregate
{
    public IEnumerable<object> GetEntities() => [Organization, Membership];
}
