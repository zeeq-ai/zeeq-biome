using Microsoft.Extensions.Time.Testing;

namespace Zeeq.Biome.Tests.Learning;

// The same scenarios as LearningTests.WithBiome.cs, set up by hand.
// Compare how much of each test is setup bookkeeping instead of intent.
public sealed partial class LearningTests
{
    [Test]
    public async Task District_tree_builds_six_levels_and_rosters_cross_section_boundaries_typical()
    {
        // Arrange: every level, parent link, counter and timestamp is written out by hand.
        var now = DateTimeOffset.UtcNow;
        var schools = new List<School>();
        var courses = new List<Course>();
        var sections = new List<Section>();
        var enrollments = new List<Enrollment>();
        var assignments = new List<Assignment>();
        var submissions = new List<Submission>();

        for (var s = 0; s < 2; s++)
        {
            var school = new School { District = "Example district" };
            schools.Add(school);
            for (var c = 0; c < 2; c++)
            {
                var course = new Course { School = school };
                courses.Add(course);
                // Section numbers and the roster both restart per course.
                var sectionNumber = 1;
                var roster = 1;
                for (var x = 0; x < 2; x++)
                {
                    var section = new Section
                    {
                        Course = course,
                        Number = sectionNumber,
                        Name = $"Section {sectionNumber++}",
                    };
                    sections.Add(section);
                    for (var e = 0; e < 3; e++)
                    {
                        var enrollment = new Enrollment
                        {
                            Section = section,
                            RosterNumber = roster,
                            Student = $"Student {roster++}",
                        };
                        enrollments.Add(enrollment);
                        // Assignment numbers restart under each enrollment.
                        for (var a = 1; a <= 2; a++)
                        {
                            var assignment = new Assignment
                            {
                                Enrollment = enrollment,
                                School = school,
                                Number = a,
                                Title = $"Assignment {a}",
                                OpensAt = now.AddDays(-1),
                                DueAt = now.AddDays(1),
                            };
                            assignments.Add(assignment);
                            submissions.Add(
                                new Submission
                                {
                                    Assignment = assignment,
                                    School = school,
                                    Attempt = 1,
                                    Grade = 80m,
                                    SubmittedAt = now,
                                    Late = false,
                                }
                            );
                        }
                    }
                }
            }
        }

        // Assert: identical to the Biome version.
        await Assert.That(schools.Count).IsEqualTo(2);
        await Assert.That(courses.Count).IsEqualTo(2 * 2);
        await Assert.That(sections.Count).IsEqualTo(2 * 2 * 2);
        await Assert.That(enrollments.Count).IsEqualTo(2 * 2 * 2 * 3);
        await Assert.That(assignments.Count).IsEqualTo(2 * 2 * 2 * 3 * 2);
        await Assert.That(submissions.Count).IsEqualTo(2 * 2 * 2 * 3 * 2 * 1);
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
    public async Task Explicit_sections_grades_and_draft_submissions_preserve_call_site_intent_typical()
    {
        // Arrange: build every entity by hand and track what is saved.
        var now = DateTimeOffset.UtcNow;
        var school = new School { District = "Example district" };
        var course = new Course { School = school };

        // Reserve section 1 by hand so the earlier section has to use 2.
        var sectionTwo = new Section
        {
            Course = course,
            Number = 2,
            Name = "Section 2",
        };
        var sectionOne = new Section
        {
            Course = course,
            Number = 1,
            Name = "Section 1",
        };
        var sections = new[] { sectionTwo, sectionOne };
        var third = new Section
        {
            Course = course,
            Number = 3,
            Name = "Section 3",
        };

        var roster = 1; // per course, across sections
        var enrollments = sections
            .SelectMany(s =>
                Enumerable
                    .Range(0, 2)
                    .Select(_ => new Enrollment
                    {
                        Section = s,
                        RosterNumber = roster,
                        Student = $"Student {roster++}",
                    })
            )
            .ToList();
        var assignments = enrollments
            .Select(e => new Assignment
            {
                Enrollment = e,
                School = school,
                Number = 1,
                Title = "Assignment 1",
                OpensAt = now.AddDays(-1),
                DueAt = now.AddDays(1),
            })
            .ToList();
        var submissions = assignments
            .Select(a => new Submission
            {
                Assignment = a,
                School = school,
                Attempt = 1,
                Grade = 95m,
                Reviewed = true,
                SubmittedAt = now,
                Late = false,
            })
            .ToList();

        // Give the draft its own attempt number and leave it out of the saved list.
        var draft = new Submission
        {
            Assignment = assignments[0],
            School = school,
            Attempt = 2,
            Grade = 0m,
            SubmittedAt = now,
        };
        var saved = new List<object> { school, course, sectionTwo, sectionOne, third };
        saved.AddRange(enrollments);
        saved.AddRange(assignments);
        saved.AddRange(submissions);

        // Assert: identical to the Biome version.
        await Assert.That(sections[0].Number).IsEqualTo(2);
        await Assert.That(sections[0].Name).IsEqualTo("Section 2");
        await Assert.That(third.Number).IsEqualTo(3);
        await Assert.That(submissions.All(s => s.Grade == 95m && s.Reviewed)).IsTrue();
        await Assert.That(draft.Grade).IsEqualTo(0m);
        await Assert.That(draft.Attempt).IsEqualTo(2);
        await Assert.That(submissions.Count + 1).IsEqualTo(2 * 2 * 1 + 1);
        await Assert.That(saved.OfType<Submission>().Count()).IsEqualTo(4);
    }

    [Test]
    public async Task Late_submission_helper_and_policy_share_one_fake_time_provider_typical()
    {
        // Arrange: start one hour before the deadline; the clock drives setup and the SUT.
        var deadline = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(deadline.AddHours(-1));
        var policy = new LatePolicy(time);
        var school = new School { District = "Example district" };

        // Without a helper, each branch repeats the full parent chain.
        Assignment NewAssignment(DateTimeOffset dueAt)
        {
            var course = new Course { School = school };
            var section = new Section
            {
                Course = course,
                Number = 1,
                Name = "Section 1",
            };
            var enrollment = new Enrollment
            {
                Section = section,
                RosterNumber = 1,
                Student = "Student 1",
            };
            return new Assignment
            {
                Enrollment = enrollment,
                School = school,
                Number = 1,
                Title = "Assignment 1",
                OpensAt = time.GetUtcNow().AddDays(-1),
                DueAt = dueAt,
            };
        }

        // Lateness is derived by hand from the final submission time.
        Submission NewSubmission(Assignment assignment)
        {
            var submittedAt = time.GetUtcNow();
            return new Submission
            {
                Assignment = assignment,
                School = school,
                Attempt = 1,
                Grade = 80m,
                SubmittedAt = submittedAt,
                Late = submittedAt > assignment.DueAt,
            };
        }

        var assignment = NewAssignment(time.GetUtcNow().AddMinutes(-1));
        var historicalLate = NewSubmission(assignment);
        var upcoming = NewAssignment(deadline);
        var wasOverdue = policy.IsOverdue(upcoming);

        // Act: advance the clock, then stamp the second submission.
        time.Advance(TimeSpan.FromHours(2));
        var late = NewSubmission(upcoming);
        var submissions = new[] { historicalLate, late };

        // Assert: identical to the Biome version.
        await Assert.That(wasOverdue).IsFalse();
        await Assert.That(policy.IsOverdue(upcoming)).IsTrue();
        await Assert.That(LatePolicy.IsLate(late) && late.Late).IsTrue();
        await Assert.That(LatePolicy.IsLate(historicalLate) && historicalLate.Late).IsTrue();
        await Assert.That(historicalLate.SubmittedAt).IsEqualTo(deadline.AddHours(-1));
        await Assert.That(late.SubmittedAt).IsEqualTo(deadline.AddHours(1));
        await Assert.That(assignment.School).IsSameReferenceAs(historicalLate.School);
        await Assert.That(submissions.Length).IsEqualTo(2);
    }
}
