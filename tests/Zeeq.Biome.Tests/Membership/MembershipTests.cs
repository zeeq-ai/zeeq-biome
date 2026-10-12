using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Membership;

public sealed class MembershipTests
{
    [Test]
    public async Task Aliases_fan_out_per_user_and_normalization_is_fill_only()
    {
        // Aliases for two users: every customizer fans out per parent; explicit normalization survives Derive.
        // Arrange: Use two users and two alias delegates to make the parent multiplication visible.

        // Act: Create an automatically normalized alias and an explicitly normalized sibling per user.
        await TestBiome
            .Start(out _)
            // Create user parents so alias customizers can fan out under each one.
            .AddUsers(out var users, 2)
            // Apply each customization PER user; Derive normalizes only unset values.
            .AddAliases(
                out var aliases,
                a => a.Value = "Alice",
                a =>
                {
                    a.Value = "Bob";
                    a.NormalizedValue = "manual";
                }
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: 2 users × 2 delegates = 4 aliases, preserving both parent identity and explicit normalization.
        await Assert.That(aliases.Length).IsEqualTo(4);
        await Assert.That(aliases[0].User).IsSameReferenceAs(users[0]);
        await Assert.That(aliases[2].User).IsSameReferenceAs(users[1]);
        await Assert.That(aliases[0].NormalizedValue).IsEqualTo("ALICE");
        await Assert.That(aliases[3].NormalizedValue).IsEqualTo("manual");
    }

    [Test]
    public async Task Invitation_expiry_uses_customized_creation_time_and_preserves_override()
    {
        // Invitation deadlines: derive from customized creation time, while explicit expiry wins.
        // Arrange: Choose a historical creation time instead of depending on the wall clock.
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act: Create one derived deadline and one explicitly supplied deadline.
        await TestBiome
            .Start(out _)
            // Customize creation/expiry first; expiry Derive fills only what remains unset.
            .AddInvitations(
                out var invitations,
                i => i.CreatedAt = created,
                i =>
                {
                    i.CreatedAt = created;
                    i.ExpiresAt = created.AddDays(1);
                }
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: The derived expiry follows customized creation time; the explicit expiry is untouched.
        await Assert.That(invitations[0].ExpiresAt).IsEqualTo(created.AddDays(7));
        await Assert.That(invitations[1].ExpiresAt).IsEqualTo(created.AddDays(1));
    }

    [Test]
    public async Task Organization_inputs_build_aggregates_then_expand_to_rows_in_order()
    {
        // Organization setup inputs: customizers build aggregate wrappers, which expand to ordered persistence rows.
        // Arrange: Keep the seed owner, root builder, and recording store.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out var seed, store);

        // Act: Build two organizations from different input customizers.
        await biome
            // Customize inputs; the builder returns aggregate wrappers rather than persistence rows.
            .AddOrganizations(
                out var graphs,
                input =>
                {
                    input.Slug = "alpha";
                    input.IsDefault = true;
                },
                input => input.Slug = "beta"
            )
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: Owner precedes organization/membership parts; inspection retains aggregate wrappers.
        await Assert.That(store.Entities.Count).IsEqualTo(5);
        await Assert.That(store.Entities[0]).IsSameReferenceAs(seed.Owner);
        await Assert.That(store.Entities[1]).IsSameReferenceAs(graphs[0].Organization);
        await Assert.That(store.Entities[2]).IsSameReferenceAs(graphs[0].Membership);
        await Assert.That(graphs[0].Membership.User).IsSameReferenceAs(seed.Owner);
        await Assert.That(graphs[0].Membership.IsDefault).IsTrue();
        await Assert.That(graphs[1].Organization.Slug).IsEqualTo("beta");
        await Assert.That(biome.All<OrganizationGraph>().Count).IsEqualTo(2);
    }

    [Test]
    public async Task Input_variants_apply_traits_then_customizers_then_derivations_and_can_be_transient()
    {
        // An organization preview: immutable traits precede customizers, and transient aggregate parts are not saved.
        // Arrange: Create an immutable preview variant with two traits and a fill-only fallback.
        var store = new RecordingStore();
        var variant = Blueprints
            .Organization.With(i => i.Slug = "trait")
            .With((i, ctx) => i.IsDefault = ctx.Seed.Owner.Id == "owner")
            .Derive(i => i.Slug, (_, _) => "fallback")
            .AsTransient();

        // Act: Override its slug at the call site before the aggregate factory runs.
        await TestBiome
            .Start(out _, store)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(variant, out var graph, (input, _) => input.Slug = "custom")
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: The explicit slug survives and only the baseline owner is persisted.
        await Assert.That(graph.Organization.Slug).IsEqualTo("custom");
        await Assert.That(graph.Membership.IsDefault).IsTrue();
        await Assert.That(store.Entities.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Blueprint_variants_leave_original_unchanged_and_empty_string_is_explicit()
    {
        // VIP and ordinary users: blueprint variants are immutable; empty string counts as explicitly set.
        // Arrange: Compare a VIP variant with its original user blueprint.

        // Act: Build both users, then supply an empty normalized alias value.
        await TestBiome
            .Start(out _)
            // Build one output eagerly; customizers run before fill-only derives and defaults.
            .AddOne(Blueprints.VipUser, out var vip)
            // Use the original blueprint to prove a VIP variant did not mutate it.
            .AddUser(out var ordinary)
            // Keep the explicit empty normalized string: call site wins, nothing overwrites.
            .AddAlias(out var alias, a => a.NormalizedValue = "")
            // Only this call saves; construction above was eager.
            .BuildAsync();

        // Assert: The original stays ordinary and empty string is preserved as explicitly set.
        await Assert.That(vip.IsVip).IsTrue();
        await Assert.That(ordinary.IsVip).IsFalse();
        await Assert.That(alias.NormalizedValue).IsEqualTo("");
    }
}
