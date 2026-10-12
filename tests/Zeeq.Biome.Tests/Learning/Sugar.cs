using Zeeq.Biome;

namespace Zeeq.Biome.Tests.Learning;

internal static class Sugar
{
    // Root/input sugar works from any anchor; no implicit ancestor construction is needed.
    extension<TAnchor>(BiomeBuilder<LearningSeed, TAnchor> b)
    {
        /// <summary>Builds one default School.</summary>
        public BiomeBuilder<LearningSeed, School[]> AddSchool(out School entity) =>
            b.AddOne(Blueprints.School, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<LearningSeed, School[]> AddSchool(
            out School entity,
            Action<School> customize
        ) => b.AddOne(Blueprints.School, out entity, customize);

        /// <summary>Builds count outputs.</summary>
        public BiomeBuilder<LearningSeed, School[]> AddSchools(out School[] entities, int count) =>
            b.Add(Blueprints.School, out entities, count);

        /// <summary>Builds one output per delegate; use + to stack traits.</summary>
        public BiomeBuilder<LearningSeed, School[]> AddSchools(
            out School[] entities,
            params Action<School>[] customize
        ) => b.Add(Blueprints.School, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<LearningSeed, School[]> b)
    {
        /// <summary>Builds one default Course under exactly one School.</summary>
        public BiomeBuilder<LearningSeed, Course[]> AddCourse(out Course entity) =>
            b.AddOne(Blueprints.Course, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<LearningSeed, Course[]> AddCourse(
            out Course entity,
            Action<Course> customize
        ) => b.AddOne(Blueprints.Course, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<LearningSeed, Course[]> AddCourses(out Course[] entities, int count) =>
            b.Add(Blueprints.Course, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<LearningSeed, Course[]> AddCourses(
            out Course[] entities,
            params Action<Course>[] customize
        ) => b.Add(Blueprints.Course, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<LearningSeed, Course[]> b)
    {
        /// <summary>Builds one default Section under exactly one Course.</summary>
        public BiomeBuilder<LearningSeed, Section[]> AddSection(out Section entity) =>
            b.AddOne(Blueprints.Section, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<LearningSeed, Section[]> AddSection(
            out Section entity,
            Action<Section> customize
        ) => b.AddOne(Blueprints.Section, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<LearningSeed, Section[]> AddSections(
            out Section[] entities,
            int count
        ) => b.Add(Blueprints.Section, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<LearningSeed, Section[]> AddSections(
            out Section[] entities,
            params Action<Section>[] customize
        ) => b.Add(Blueprints.Section, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<LearningSeed, Section[]> b)
    {
        /// <summary>Builds one default Enrollment under exactly one Section.</summary>
        public BiomeBuilder<LearningSeed, Enrollment[]> AddEnrollment(out Enrollment entity) =>
            b.AddOne(Blueprints.Enrollment, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<LearningSeed, Enrollment[]> AddEnrollment(
            out Enrollment entity,
            Action<Enrollment> customize
        ) => b.AddOne(Blueprints.Enrollment, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<LearningSeed, Enrollment[]> AddEnrollments(
            out Enrollment[] entities,
            int count
        ) => b.Add(Blueprints.Enrollment, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<LearningSeed, Enrollment[]> AddEnrollments(
            out Enrollment[] entities,
            params Action<Enrollment>[] customize
        ) => b.Add(Blueprints.Enrollment, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<LearningSeed, Enrollment[]> b)
    {
        /// <summary>Builds one default Assignment under exactly one Enrollment.</summary>
        public BiomeBuilder<LearningSeed, Assignment[]> AddAssignment(out Assignment entity) =>
            b.AddOne(Blueprints.Assignment, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<LearningSeed, Assignment[]> AddAssignment(
            out Assignment entity,
            Action<Assignment> customize
        ) => b.AddOne(Blueprints.Assignment, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<LearningSeed, Assignment[]> AddAssignments(
            out Assignment[] entities,
            int count
        ) => b.Add(Blueprints.Assignment, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<LearningSeed, Assignment[]> AddAssignments(
            out Assignment[] entities,
            params Action<Assignment>[] customize
        ) => b.Add(Blueprints.Assignment, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<LearningSeed, Assignment[]> b)
    {
        /// <summary>Builds one default Submission under exactly one Assignment.</summary>
        public BiomeBuilder<LearningSeed, Submission[]> AddSubmission(out Submission entity) =>
            b.AddOne(Blueprints.Submission, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<LearningSeed, Submission[]> AddSubmission(
            out Submission entity,
            Action<Submission> customize
        ) => b.AddOne(Blueprints.Submission, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<LearningSeed, Submission[]> AddSubmissions(
            out Submission[] entities,
            int count
        ) => b.Add(Blueprints.Submission, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<LearningSeed, Submission[]> AddSubmissions(
            out Submission[] entities,
            params Action<Submission>[] customize
        ) => b.Add(Blueprints.Submission, out entities, customize);
    }

    // Scenario helpers are explicit app recipes, available only from the seed anchor.
    extension(BiomeBuilder<LearningSeed, LearningSeed> b)
    {
        /// <summary>Builds a complete default 6-level branch and returns its leaf with a chosen deadline.</summary>
        public BiomeBuilder<LearningSeed, Submission[]> AddLateSubmission(
            out Submission leaf,
            DateTimeOffset dueAt
        ) =>
            b.AddSchool(out _)
                .AddCourse(out _)
                .AddSection(out _)
                .AddEnrollment(out _)
                .AddAssignment(out _, a => a.DueAt = dueAt)
                .AddSubmission(out leaf);
    }
}
