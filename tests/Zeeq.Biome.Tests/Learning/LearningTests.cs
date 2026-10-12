using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.Learning;

public sealed class LearningTests
{
    [Test]
    public async Task District_tree_builds_six_levels_and_rosters_cross_section_boundaries()
    {
        // A district assessment graph: six levels, course-wide rosters, local assignments/attempts.
        // Arrange: no provider means every factory shares one frozen, rounded now.
        var biome = TestBiome.Start(out _);

        // Act: each Add re-anchors, so the next blueprint receives the correct parent type.
        await biome
            .AddSchools(out var schools, 2)
            // 2 schools × 2 courses = 4 courses.
            .AddCourses(out _, 2)
            // 4 courses × 2 sections = 8 sections.
            .AddSections(out var sections, 2)
            // 8 sections × 3 enrollments = 24 enrollments.
            .AddEnrollments(out var enrollments, 3)
            // 24 enrollments × 2 assignments = 48 assignments.
            .AddAssignments(out var assignments, 2)
            .AddSubmissions(out var submissions, 1)
            .BuildAsync();

        // Assert: explicit multiplication teaches fan-out; distant ancestors preserve school ownership.
        await Assert.That(biome.All<School>().Count).IsEqualTo(2);
        await Assert.That(biome.All<Course>().Count).IsEqualTo(2 * 2);
        await Assert.That(biome.All<Section>().Count).IsEqualTo(2 * 2 * 2);
        await Assert.That(biome.All<Enrollment>().Count).IsEqualTo(2 * 2 * 2 * 3);
        await Assert.That(biome.All<Assignment>().Count).IsEqualTo(2 * 2 * 2 * 3 * 2);
        await Assert.That(biome.All<Submission>().Count).IsEqualTo(2 * 2 * 2 * 3 * 2 * 1);
        await Assert.That(sections[2].Number).IsEqualTo(1);
        await Assert.That(enrollments[3].RosterNumber).IsEqualTo(4);
        await Assert.That(enrollments[6].RosterNumber).IsEqualTo(1);
        await Assert.That(assignments[2].Number).IsEqualTo(1);
        await Assert.That(submissions[47].School).IsSameReferenceAs(schools[1]);
        await Assert.That(submissions.All(s => s.Grade == 80m && !s.Late)).IsTrue();
        await Assert.That(submissions[47].SubmittedAt).IsEqualTo(submissions[0].SubmittedAt);
        await Assert
            .That(assignments[0].DueAt - assignments[0].OpensAt)
            .IsEqualTo(TimeSpan.FromDays(2));
    }

    [Test]
    public async Task Explicit_sections_grades_and_draft_submissions_preserve_call_site_intent()
    {
        // A course with numbered sections and graded attempts; a later draft stays out of persistence.
        // Arrange: use a store to distinguish a transient draft from a stored submission.
        var store = new RecordingStore();
        var biome = TestBiome.Start(out _, store);

        // Act: section 1 is reserved for the second output, so the first section receives 2.
        await biome
            .AddSchool(out _)
            .AddCourse(out var course)
            .AddSections(out var sections, _ => { }, s => s.Number = 1)
            .AddEnrollments(out _, 2)
            .AddAssignments(out var assignments, 1)
            // + grades and marks reviewed on one attempt PER assignment.
            .AddSubmissions(out var submissions, Submissions.Distinction + Submissions.Reviewed)
            // A sibling section resumes the course sequence; no new course is invented.
            .Under(course)
            .AddSection(out var third)
            // Return to a specific assignment for a second, preview-only attempt.
            .Under(assignments[0])
            .AddSubmission(out var draft, s => s.Grade = 0m)
            .Transient()
            .BuildAsync();

        // Assert: nullable grade 0 is explicit; fill-only Derive must not replace it with 80.
        await Assert.That(sections[0].Number).IsEqualTo(2);
        await Assert.That(sections[0].Name).IsEqualTo("Section 2");
        await Assert.That(third.Number).IsEqualTo(3);
        await Assert.That(submissions.All(s => s.Grade == 95m && s.Reviewed)).IsTrue();
        await Assert.That(draft.Grade).IsEqualTo(0m);
        await Assert.That(draft.Attempt).IsEqualTo(2);
        await Assert.That(biome.All<Submission>().Count).IsEqualTo(2 * 2 * 1 + 1);
        await Assert.That(store.Entities.OfType<Submission>().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Late_submission_helper_and_policy_share_one_fake_time_provider()
    {
        // A late assignment policy: the same clock drives both Biome setup and the tiny SUT.
        // Arrange: start one hour before the deadline and build an eager, six-level late scenario.
        var deadline = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(deadline.AddHours(-1));
        var policy = new LatePolicy(time);
        var biome = TestBiome.Start(out _, time: time);
        biome.AddLateSubmission(out var historicalLate, dueAt: time.GetUtcNow().AddMinutes(-1));
        var assignment = historicalLate.Assignment;
        // Use a separate on-time branch so crossing a deadline is observable, not assumed.
        biome
            .AddSchool(out _)
            .AddCourse(out _)
            .AddSection(out _)
            .AddEnrollment(out _)
            .AddAssignment(out var upcoming, a => a.DueAt = deadline);
        var wasOverdue = policy.IsOverdue(upcoming);

        // Act: eager adds allow time to advance before the single BuildAsync save.
        time.Advance(TimeSpan.FromHours(2));
        await biome.Under(upcoming).AddSubmission(out var late).BuildAsync();

        // Assert: setup derives and SUT agree; advancing time never mutates historical rows.
        await Assert.That(wasOverdue).IsFalse();
        await Assert.That(policy.IsOverdue(upcoming)).IsTrue();
        await Assert.That(LatePolicy.IsLate(late) && late.Late).IsTrue();
        await Assert.That(LatePolicy.IsLate(historicalLate) && historicalLate.Late).IsTrue();
        await Assert.That(historicalLate.SubmittedAt).IsEqualTo(deadline.AddHours(-1));
        await Assert.That(late.SubmittedAt).IsEqualTo(deadline.AddHours(1));
        await Assert.That(assignment.School).IsSameReferenceAs(historicalLate.School);
        await Assert.That(biome.All<Submission>().Count).IsEqualTo(2);
    }
}
