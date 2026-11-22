namespace Zeeq.Biome.Tests.Membership;

// The same scenarios as MembershipTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
public sealed partial class MembershipTests
{
    [Test]
    public async Task Aliases_fan_out_per_user_and_normalization_is_fill_only_typical()
    {
        // Arrange: two users, each with an alias per variant; normalization is repeated by hand.
        var users = new[]
        {
            new User { Id = "user-1" },
            new User { Id = "user-2" },
        };
        var aliases = users
            .SelectMany(u =>
                new[]
                {
                    new UserAlias
                    {
                        User = u,
                        Value = "Alice",
                        NormalizedValue = "ALICE",
                    },
                    new UserAlias
                    {
                        User = u,
                        Value = "Bob",
                        NormalizedValue = "manual",
                    },
                }
            )
            .ToArray();

        // Assert: identical to the Biome version.
        await Assert.That(aliases.Length).IsEqualTo(4);
        await Assert.That(aliases[0].User).IsSameReferenceAs(users[0]);
        await Assert.That(aliases[2].User).IsSameReferenceAs(users[1]);
        await Assert.That(aliases[0].NormalizedValue).IsEqualTo("ALICE");
        await Assert.That(aliases[3].NormalizedValue).IsEqualTo("manual");
    }

    [Test]
    public async Task Invitation_expiry_uses_customized_creation_time_and_preserves_override_typical()
    {
        // Arrange: the default 7-day expiry is recomputed by hand from the chosen creation time.
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var invitations = new[]
        {
            new PendingInvitation { CreatedAt = created, ExpiresAt = created.AddDays(7) },
            new PendingInvitation { CreatedAt = created, ExpiresAt = created.AddDays(1) },
        };

        // Assert: identical to the Biome version.
        await Assert.That(invitations[0].ExpiresAt).IsEqualTo(created.AddDays(7));
        await Assert.That(invitations[1].ExpiresAt).IsEqualTo(created.AddDays(1));
    }

    [Test]
    public async Task Organization_inputs_build_aggregates_then_expand_to_rows_in_order_typical()
    {
        // Arrange: the owner, input values, and the expansion into rows are all manual.
        var owner = new User();
        var inputs = new[]
        {
            new OrganizationInput { Slug = "alpha", IsDefault = true },
            new OrganizationInput { Slug = "beta" },
        };
        var graphs = inputs
            .Select(input =>
            {
                var org = new Organization { Slug = input.Slug };
                return new OrganizationGraph(
                    org,
                    new OrganizationMembership
                    {
                        Organization = org,
                        User = owner,
                        IsDefault = input.IsDefault,
                    }
                );
            })
            .ToArray();

        // Saved rows: owner first, then each graph expanded in order.
        var saved = new List<object> { owner };
        saved.AddRange(graphs.SelectMany(g => g.GetEntities()));

        // Assert: identical to the Biome version.
        await Assert.That(saved.Count).IsEqualTo(5);
        await Assert.That(saved[0]).IsSameReferenceAs(owner);
        await Assert.That(saved[1]).IsSameReferenceAs(graphs[0].Organization);
        await Assert.That(saved[2]).IsSameReferenceAs(graphs[0].Membership);
        await Assert.That(graphs[0].Membership.User).IsSameReferenceAs(owner);
        await Assert.That(graphs[0].Membership.IsDefault).IsTrue();
        await Assert.That(graphs[1].Organization.Slug).IsEqualTo("beta");
        await Assert.That(graphs.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Input_variants_apply_traits_then_customizers_then_derivations_and_can_be_transient_typical()
    {
        // Arrange: the trait order (slug "trait", then "custom") collapses to the final values.
        var owner = new User();
        var input = new OrganizationInput { Slug = "custom", IsDefault = owner.Id == "owner" };
        var org = new Organization { Slug = input.Slug };
        var membership = new OrganizationMembership
        {
            Organization = org,
            User = owner,
            IsDefault = input.IsDefault,
        };
        var graph = new OrganizationGraph(org, membership);

        // The preview is not saved: only the owner is.
        var saved = new List<object> { owner };

        // Assert: identical to the Biome version.
        await Assert.That(graph.Organization.Slug).IsEqualTo("custom");
        await Assert.That(graph.Membership.IsDefault).IsTrue();
        await Assert.That(saved.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Blueprint_variants_leave_original_unchanged_and_empty_string_is_explicit_typical()
    {
        // Arrange: nothing is shared, so the VIP flag cannot leak into the ordinary user.
        var vip = new User { Id = "user-1", IsVip = true };
        var ordinary = new User { Id = "user-2" };
        var alias = new UserAlias { User = ordinary, NormalizedValue = "" };

        // Assert: identical to the Biome version.
        await Assert.That(vip.IsVip).IsTrue();
        await Assert.That(ordinary.IsVip).IsFalse();
        await Assert.That(alias.NormalizedValue).IsEqualTo("");
    }
}
