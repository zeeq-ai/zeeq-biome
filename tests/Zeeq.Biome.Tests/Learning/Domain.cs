using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Learning;

internal sealed record LearningSeed(string District);

internal sealed class School
{
    public string District { get; init; } = "";
}

internal sealed class Course
{
    public School School { get; init; } = null!;
}

internal sealed class Section
{
    public Course Course { get; init; } = null!;
    public int Number { get; set; }
    public string? Name { get; set; }
}

internal sealed class Enrollment
{
    public Section Section { get; init; } = null!;
    public int RosterNumber { get; set; }
    public string? Student { get; set; }
}

internal sealed class Assignment
{
    public Enrollment Enrollment { get; init; } = null!;
    public School School { get; init; } = null!;
    public int Number { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset OpensAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
}

internal sealed class Submission
{
    public Assignment Assignment { get; init; } = null!;
    public School School { get; init; } = null!;
    public int Attempt { get; set; }
    public decimal? Grade { get; set; }
    public bool Reviewed { get; set; }
    public bool Late { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}

internal static class Submissions
{
    // + keeps grade and review state on the same submission.
    internal static readonly Action<Submission> Distinction = s => s.Grade = 95m;
    internal static readonly Action<Submission> Reviewed = s => s.Reviewed = true;
}

internal static class Blueprints
{
    // Section numbering belongs to the course, even when multiple schools are fanned out.
    internal static readonly Sequence<int> SectionNumbers = Sequence
        .Of(1, n => n + 1)
        .Per<Course>();

    // Enrollment parents are sections; Per<Course> continues the roster across those sections.
    internal static readonly Sequence<int> RosterNumbers = Sequence.Of(1, n => n + 1).Per<Course>();

    // Assignments restart under each enrollment.
    internal static readonly Sequence<int> AssignmentNumbers = Sequence
        .Of(1, n => n + 1)
        .PerParent();

    // Attempts restart under each assignment and continue when Under(assignment) is used later.
    internal static readonly Sequence<int> Attempts = Sequence.Of(1, n => n + 1).PerParent();

    // The seed is a plain record; no persistence introspection is needed.
    internal static readonly Blueprint<LearningSeed, School> School = Blueprint.For<
        LearningSeed,
        School
    >(ctx => new() { District = ctx.Seed.District });

    // Direct typed parents provide relationships through the deep chain.
    internal static readonly ChildBlueprint<LearningSeed, School, Course> Course =
        Blueprint.ChildOf<LearningSeed, School, Course>((school, _) => new() { School = school });

    // Number and Name stay unset until customization finishes; Name derives from final Number.
    internal static readonly ChildBlueprint<LearningSeed, Course, Section> Section = Blueprint
        .ChildOf<LearningSeed, Course, Section>((course, _) => new() { Course = course })
        .Derive(s => s.Number, SectionNumbers)
        .Derive(s => s.Name, (s, _) => $"Section {s.Number}");

    // RosterNumber spans sibling sections instead of restarting per section.
    internal static readonly ChildBlueprint<LearningSeed, Section, Enrollment> Enrollment =
        Blueprint
            .ChildOf<LearningSeed, Section, Enrollment>((section, _) => new() { Section = section })
            .Derive(e => e.RosterNumber, RosterNumbers)
            .Derive(e => e.Student, (e, _) => $"Student {e.RosterNumber}");

    // Open in the past, due in the future; use ctx time for setup instead of UtcNow.
    internal static readonly ChildBlueprint<LearningSeed, Enrollment, Assignment> Assignment =
        Blueprint
            .ChildOf<LearningSeed, Enrollment, Assignment>(
                (enrollment, ctx) =>
                    new()
                    {
                        Enrollment = enrollment,
                        School = ctx.Ancestor<School>(),
                        OpensAt = ctx.Ago(TimeSpan.FromDays(1)),
                        DueAt = ctx.FromNow(TimeSpan.FromDays(1)),
                    }
            )
            .Derive(a => a.Number, AssignmentNumbers)
            .Derive(a => a.Title, (a, _) => $"Assignment {a.Number}");

    // Time precedes the Late derive; grade remains fill-only so a call-site grade survives.
    internal static readonly ChildBlueprint<LearningSeed, Assignment, Submission> Submission =
        Blueprint
            .ChildOf<LearningSeed, Assignment, Submission>(
                (assignment, ctx) =>
                    new()
                    {
                        Assignment = assignment,
                        School = ctx.Ancestor<School>(),
                        SubmittedAt = ctx.Now,
                    }
            )
            .Derive(s => s.Attempt, Attempts)
            .Derive(s => s.Grade, (_, _) => 80m)
            .Derive(s => s.Late, (s, _) => s.SubmittedAt > s.Assignment.DueAt);
}

// A tiny SUT: sharing the provider makes policy and setup respond to the same clock advance.
internal sealed class LatePolicy(TimeProvider time)
{
    internal bool IsOverdue(Assignment assignment) => time.GetUtcNow() > assignment.DueAt;

    internal static bool IsLate(Submission submission) =>
        submission.SubmittedAt > submission.Assignment.DueAt;
}

internal static class TestBiome
{
    private static readonly BiomeOptions<LearningSeed> Options = new();

    // Supply one fake provider to this wrapper and LatePolicy in time-sensitive tests.
    internal static BiomeBuilder<LearningSeed, LearningSeed> Start(
        out LearningSeed seed,
        IBiomeStore? store = null,
        TimeProvider? time = null
    )
    {
        seed = new("Example district");
        return Biome.Start(seed, Options, store, time);
    }
}
