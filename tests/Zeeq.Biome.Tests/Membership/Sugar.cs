using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Membership;

internal static class Sugar
{
    // Root sugar can start a new branch from any anchor; all adds return a new output anchor.
    extension<TAnchor>(BiomeBuilder<MembershipSeed, TAnchor> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<MembershipSeed, User[]> AddUser(out User entity) =>
            b.AddOne(Blueprints.User, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<MembershipSeed, User[]> AddUser(
            out User entity,
            Action<User> customize
        ) => b.AddOne(Blueprints.User, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<MembershipSeed, User[]> AddUsers(out User[] entities, int count) =>
            b.Add(Blueprints.User, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<MembershipSeed, User[]> AddUsers(
            out User[] entities,
            params Action<User>[] customize
        ) => b.Add(Blueprints.User, out entities, customize);
    }

    // Child sugar lives on this exact parent-array anchor; incompatible chains cannot call it.
    extension(BiomeBuilder<MembershipSeed, User[]> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<MembershipSeed, UserAlias[]> AddAlias(out UserAlias entity) =>
            b.AddOne(Blueprints.Alias, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<MembershipSeed, UserAlias[]> AddAlias(
            out UserAlias entity,
            Action<UserAlias> customize
        ) => b.AddOne(Blueprints.Alias, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<MembershipSeed, UserAlias[]> AddAliases(
            out UserAlias[] entities,
            int count
        ) => b.Add(Blueprints.Alias, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<MembershipSeed, UserAlias[]> AddAliases(
            out UserAlias[] entities,
            params Action<UserAlias>[] customize
        ) => b.Add(Blueprints.Alias, out entities, customize);
    }

    // Root sugar can start a new branch from any anchor; all adds return a new output anchor.
    extension<TAnchor>(BiomeBuilder<MembershipSeed, TAnchor> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<MembershipSeed, PendingInvitation[]> AddInvitation(
            out PendingInvitation entity
        ) => b.AddOne(Blueprints.Invitation, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<MembershipSeed, PendingInvitation[]> AddInvitation(
            out PendingInvitation entity,
            Action<PendingInvitation> customize
        ) => b.AddOne(Blueprints.Invitation, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<MembershipSeed, PendingInvitation[]> AddInvitations(
            out PendingInvitation[] entities,
            int count
        ) => b.Add(Blueprints.Invitation, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<MembershipSeed, PendingInvitation[]> AddInvitations(
            out PendingInvitation[] entities,
            params Action<PendingInvitation>[] customize
        ) => b.Add(Blueprints.Invitation, out entities, customize);
    }

    // Root sugar can start a new branch from any anchor; all adds return a new output anchor.
    extension<TAnchor>(BiomeBuilder<MembershipSeed, TAnchor> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<MembershipSeed, OrganizationGraph[]> AddOrganization(
            out OrganizationGraph entity
        ) => b.AddOne(Blueprints.Organization, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<MembershipSeed, OrganizationGraph[]> AddOrganization(
            out OrganizationGraph entity,
            Action<OrganizationInput> customize
        ) => b.AddOne(Blueprints.Organization, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<MembershipSeed, OrganizationGraph[]> AddOrganizations(
            out OrganizationGraph[] entities,
            int count
        ) => b.Add(Blueprints.Organization, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<MembershipSeed, OrganizationGraph[]> AddOrganizations(
            out OrganizationGraph[] entities,
            params Action<OrganizationInput>[] customize
        ) => b.Add(Blueprints.Organization, out entities, customize);
    }
}
