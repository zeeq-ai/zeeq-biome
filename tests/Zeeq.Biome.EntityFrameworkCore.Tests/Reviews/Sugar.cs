using Zeeq.Biome;

namespace Zeeq.Biome.EntityFrameworkCore.Tests.Reviews;

internal static class Sugar
{
    extension<TAnchor>(BiomeBuilder<ReviewSeed, TAnchor> b)
    {
        public BiomeBuilder<ReviewSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository entity
        ) => b.AddOne(Blueprints.Repository, out entity);

        public BiomeBuilder<ReviewSeed, CodeRepository[]> AddCodeRepository(
            out CodeRepository entity,
            Action<CodeRepository> customize
        ) => b.AddOne(Blueprints.Repository, out entity, customize);

        public BiomeBuilder<ReviewSeed, CodeRepository[]> AddCodeRepositories(
            out CodeRepository[] entities,
            int count
        ) => b.Add(Blueprints.Repository, out entities, count);

        public BiomeBuilder<ReviewSeed, CodeRepository[]> AddCodeRepositories(
            out CodeRepository[] entities,
            params Action<CodeRepository>[] customize
        ) => b.Add(Blueprints.Repository, out entities, customize);

        public BiomeBuilder<ReviewSeed, OrganizationGraph[]> AddOrganization(
            out OrganizationGraph entity
        ) => b.AddOne(Blueprints.Organization, out entity);

        public BiomeBuilder<ReviewSeed, OrganizationGraph[]> AddOrganization(
            out OrganizationGraph entity,
            Action<OrganizationInput> customize
        ) => b.AddOne(Blueprints.Organization, out entity, customize);

        public BiomeBuilder<ReviewSeed, OrganizationGraph[]> AddOrganizations(
            out OrganizationGraph[] entities,
            int count
        ) => b.Add(Blueprints.Organization, out entities, count);

        public BiomeBuilder<ReviewSeed, OrganizationGraph[]> AddOrganizations(
            out OrganizationGraph[] entities,
            params Action<OrganizationInput>[] customize
        ) => b.Add(Blueprints.Organization, out entities, customize);
    }
    extension(BiomeBuilder<ReviewSeed, CodeRepository[]> b)
    {
        public BiomeBuilder<ReviewSeed, PullRequestRecord[]> AddPullRequest(
            out PullRequestRecord entity
        ) => b.AddOne(Blueprints.PullRequest, out entity);

        public BiomeBuilder<ReviewSeed, PullRequestRecord[]> AddPullRequest(
            out PullRequestRecord entity,
            Action<PullRequestRecord> customize
        ) => b.AddOne(Blueprints.PullRequest, out entity, customize);

        public BiomeBuilder<ReviewSeed, PullRequestRecord[]> AddPullRequests(
            out PullRequestRecord[] entities,
            int count
        ) => b.Add(Blueprints.PullRequest, out entities, count);

        public BiomeBuilder<ReviewSeed, PullRequestRecord[]> AddPullRequests(
            out PullRequestRecord[] entities,
            params Action<PullRequestRecord>[] customize
        ) => b.Add(Blueprints.PullRequest, out entities, customize);
    }
    extension(BiomeBuilder<ReviewSeed, PullRequestRecord[]> b)
    {
        public BiomeBuilder<ReviewSeed, CodeReviewRecord[]> AddCodeReview(
            out CodeReviewRecord entity
        ) => b.AddOne(Blueprints.Review, out entity);

        public BiomeBuilder<ReviewSeed, CodeReviewRecord[]> AddCodeReview(
            out CodeReviewRecord entity,
            Action<CodeReviewRecord> customize
        ) => b.AddOne(Blueprints.Review, out entity, customize);

        public BiomeBuilder<ReviewSeed, CodeReviewRecord[]> AddCodeReviews(
            out CodeReviewRecord[] entities,
            int count
        ) => b.Add(Blueprints.Review, out entities, count);

        public BiomeBuilder<ReviewSeed, CodeReviewRecord[]> AddCodeReviews(
            out CodeReviewRecord[] entities,
            params Action<CodeReviewRecord>[] customize
        ) => b.Add(Blueprints.Review, out entities, customize);
    }
}
