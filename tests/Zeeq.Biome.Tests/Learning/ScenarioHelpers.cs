namespace Zeeq.Biome.Tests.Learning;

internal static class ScenarioHelpers
{
    /// <summary>Builds a complete default branch and returns a submission with the chosen deadline.</summary>
    public static BiomeBuilder<LearningSeed, Submission[]> AddLateSubmission(
        this BiomeBuilder<LearningSeed, LearningSeed> builder,
        out Submission leaf,
        DateTimeOffset dueAt
    ) =>
        builder
            .AddSchool(out _)
            .AddCourse(out _)
            .AddSection(out _)
            .AddEnrollment(out _)
            .AddAssignment(out _, assignment => assignment.DueAt = dueAt)
            .AddSubmission(out leaf);
}
