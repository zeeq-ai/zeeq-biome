using Zeeq.Biome;

namespace Zeeq.Biome.Tests.CodeReviews;

internal static class Sugar
{
    // Root sugar can start a new branch from any anchor; all adds return a new output anchor.
    extension<TAnchor>(BiomeBuilder<CodeReviewSeed, TAnchor> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository entity
        ) => b.AddOne(Blueprints.Repository, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository entity,
            Action<CodeRepository> customize
        ) => b.AddOne(Blueprints.Repository, out entity, customize);

        // Add a named context overload where this domain frequently varies time; do not multiply every sugar method.
        /// <summary>Customizes one repository with access to the session clock and seed.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository entity,
            Action<CodeRepository, BiomeContext<CodeReviewSeed>> customize
        ) => b.AddOne(Blueprints.Repository, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeRepository[]> AddCodeRepositories(
            out CodeRepository[] entities,
            int count
        ) => b.Add(Blueprints.Repository, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeRepository[]> AddCodeRepositories(
            out CodeRepository[] entities,
            params Action<CodeRepository>[] customize
        ) => b.Add(Blueprints.Repository, out entities, customize);
    }

    // Child sugar lives on this exact parent-array anchor; incompatible chains cannot call it.
    extension(BiomeBuilder<CodeReviewSeed, CodeRepository[]> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<CodeReviewSeed, PullRequestRecord[]> AddPullRequest(
            out PullRequestRecord entity
        ) => b.AddOne(Blueprints.PullRequest, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CodeReviewSeed, PullRequestRecord[]> AddPullRequest(
            out PullRequestRecord entity,
            Action<PullRequestRecord> customize
        ) => b.AddOne(Blueprints.PullRequest, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<CodeReviewSeed, PullRequestRecord[]> AddPullRequests(
            out PullRequestRecord[] entities,
            int count
        ) => b.Add(Blueprints.PullRequest, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<CodeReviewSeed, PullRequestRecord[]> AddPullRequests(
            out PullRequestRecord[] entities,
            params Action<PullRequestRecord>[] customize
        ) => b.Add(Blueprints.PullRequest, out entities, customize);
    }

    // Child sugar lives on this exact parent-array anchor; incompatible chains cannot call it.
    extension(BiomeBuilder<CodeReviewSeed, PullRequestRecord[]> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeReviewRecord[]> AddCodeReview(
            out CodeReviewRecord entity
        ) => b.AddOne(Blueprints.Review, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeReviewRecord[]> AddCodeReview(
            out CodeReviewRecord entity,
            Action<CodeReviewRecord> customize
        ) => b.AddOne(Blueprints.Review, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeReviewRecord[]> AddCodeReviews(
            out CodeReviewRecord[] entities,
            int count
        ) => b.Add(Blueprints.Review, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<CodeReviewSeed, CodeReviewRecord[]> AddCodeReviews(
            out CodeReviewRecord[] entities,
            params Action<CodeReviewRecord>[] customize
        ) => b.Add(Blueprints.Review, out entities, customize);
    }

    // Child sugar lives on this exact parent-array anchor; incompatible chains cannot call it.
    extension(BiomeBuilder<CodeReviewSeed, CodeReviewRecord[]> b)
    {
        /// <summary>Creates one default output; child singular sugar requires exactly one parent.</summary>
        public BiomeBuilder<CodeReviewSeed, ActiveCodeReviewLock[]> AddActiveLock(
            out ActiveCodeReviewLock entity
        ) => b.AddOne(Blueprints.Lock, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<CodeReviewSeed, ActiveCodeReviewLock[]> AddActiveLock(
            out ActiveCodeReviewLock entity,
            Action<ActiveCodeReviewLock> customize
        ) => b.AddOne(Blueprints.Lock, out entity, customize);

        /// <summary>Creates count outputs per parent where applicable, flattened in parent order.</summary>
        public BiomeBuilder<CodeReviewSeed, ActiveCodeReviewLock[]> AddActiveLocks(
            out ActiveCodeReviewLock[] entities,
            int count
        ) => b.Add(Blueprints.Lock, out entities, count);

        /// <summary>Creates one output per delegate per parent where applicable; + stacks traits on one output.</summary>
        public BiomeBuilder<CodeReviewSeed, ActiveCodeReviewLock[]> AddActiveLocks(
            out ActiveCodeReviewLock[] entities,
            params Action<ActiveCodeReviewLock>[] customize
        ) => b.Add(Blueprints.Lock, out entities, customize);
    }

    // A named scenario recipe is deliberately available only on the seed anchor.
    extension(BiomeBuilder<CodeReviewSeed, CodeReviewSeed> b)
    {
        /// <summary>Names an explicit repository-to-lock recipe and returns the leaf.</summary>
        public BiomeBuilder<CodeReviewSeed, ActiveCodeReviewLock[]> AddLockedReview(
            out ActiveCodeReviewLock activeLock
        ) =>
            b.AddCodeRepository(out _)
                .AddPullRequest(out _)
                .AddCodeReview(out _)
                .AddActiveLock(out activeLock);
    }
}
