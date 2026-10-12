using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Membership;

internal sealed record MembershipSeed(User Owner);

internal sealed class User
{
    public string Id { get; init; } = "owner";
    public string Email { get; set; } = "owner@example.test";
    public bool IsVip { get; set; }
}

internal sealed class UserAlias
{
    public User User { get; init; } = null!;
    public string Value { get; set; } = "alias";
    public string? NormalizedValue { get; set; }
}

internal sealed class PendingInvitation
{
    public string Email { get; set; } = "invited@example.test";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

internal sealed class OrganizationInput
{
    public string Slug { get; set; } = "organization";
    public bool IsDefault { get; set; }
}

internal sealed class Organization
{
    public string Slug { get; init; } = "";
}

internal sealed class OrganizationMembership
{
    public User User { get; init; } = null!;
    public Organization Organization { get; init; } = null!;
    public bool IsDefault { get; init; }
}

internal sealed record OrganizationGraph(
    Organization Organization,
    OrganizationMembership Membership
) : IBiomeAggregate
{
    public IEnumerable<object> GetEntities() => [Organization, Membership];
}

internal static class Blueprints
{
    // Root entities use configured IDs; relationship-dependent fields belong in child factories.
    internal static readonly Blueprint<MembershipSeed, User> User = Blueprint.For<
        MembershipSeed,
        User
    >(ctx => new() { Id = ctx.NewId("user") });

    // With returns a new immutable variant; it never mutates User.
    internal static readonly Blueprint<MembershipSeed, User> VipUser = User.With(u =>
        u.IsVip = true
    );

    // Wire the typed user parent; leave NormalizedValue null so Derive fills only absent values.
    internal static readonly ChildBlueprint<MembershipSeed, User, UserAlias> Alias = Blueprint
        .ChildOf<MembershipSeed, User, UserAlias>((user, _) => new() { User = user })
        .Derive(a => a.NormalizedValue, (a, _) => a.Value.ToUpperInvariant());

    // Expiry derives after customizers, so changing CreatedAt changes the default deadline.
    internal static readonly Blueprint<MembershipSeed, PendingInvitation> Invitation = Blueprint
        .For<MembershipSeed, PendingInvitation>(ctx => new() { CreatedAt = ctx.Now })
        .Derive(i => i.ExpiresAt, (i, _) => i.CreatedAt.AddDays(7));

    // Customizers operate on input; the factory produces coordinated aggregate rows.
    internal static readonly InputBlueprint<
        MembershipSeed,
        OrganizationInput,
        OrganizationGraph
    > Organization = Blueprint.FromInput<MembershipSeed, OrganizationInput, OrganizationGraph>(
        () => new(),
        (input, ctx) =>
        {
            var org = new Organization { Slug = input.Slug };
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

internal static class TestBiome
{
    private static readonly BiomeOptions<MembershipSeed> Options =
        new BiomeOptions<MembershipSeed>().SeedEntities(s => [s.Owner]);

    // Domain wrapper supplies a fresh seed/session while sharing frozen configuration.
    internal static BiomeBuilder<MembershipSeed, MembershipSeed> Start(
        out MembershipSeed seed,
        IBiomeStore? store = null
    )
    {
        seed = new(new());
        return Biome.Start(seed, Options, store);
    }
}
