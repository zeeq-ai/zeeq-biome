using Zeeq.Biome;

namespace Zeeq.Biome.Tests.ProjectManagement;

internal static class Sugar
{
    // Root/input sugar works from any anchor; no implicit ancestor construction is needed.
    extension<TAnchor>(BiomeBuilder<ProjectSeed, TAnchor> b)
    {
        /// <summary>Builds one default Workspace.</summary>
        public BiomeBuilder<ProjectSeed, Workspace[]> AddWorkspace(out Workspace entity) =>
            b.AddOne(Blueprints.Workspace, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, Workspace[]> AddWorkspace(
            out Workspace entity,
            Action<Workspace> customize
        ) => b.AddOne(Blueprints.Workspace, out entity, customize);

        /// <summary>Builds count outputs.</summary>
        public BiomeBuilder<ProjectSeed, Workspace[]> AddWorkspaces(
            out Workspace[] entities,
            int count
        ) => b.Add(Blueprints.Workspace, out entities, count);

        /// <summary>Builds one output per delegate; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, Workspace[]> AddWorkspaces(
            out Workspace[] entities,
            params Action<Workspace>[] customize
        ) => b.Add(Blueprints.Workspace, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<ProjectSeed, Workspace[]> b)
    {
        /// <summary>Builds one default Project under exactly one Workspace.</summary>
        public BiomeBuilder<ProjectSeed, Project[]> AddProject(out Project entity) =>
            b.AddOne(Blueprints.Project, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, Project[]> AddProject(
            out Project entity,
            Action<Project> customize
        ) => b.AddOne(Blueprints.Project, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<ProjectSeed, Project[]> AddProjects(
            out Project[] entities,
            int count
        ) => b.Add(Blueprints.Project, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, Project[]> AddProjects(
            out Project[] entities,
            params Action<Project>[] customize
        ) => b.Add(Blueprints.Project, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<ProjectSeed, Project[]> b)
    {
        /// <summary>Builds one default Board under exactly one Project.</summary>
        public BiomeBuilder<ProjectSeed, Board[]> AddBoard(out Board entity) =>
            b.AddOne(Blueprints.Board, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, Board[]> AddBoard(
            out Board entity,
            Action<Board> customize
        ) => b.AddOne(Blueprints.Board, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<ProjectSeed, Board[]> AddBoards(out Board[] entities, int count) =>
            b.Add(Blueprints.Board, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, Board[]> AddBoards(
            out Board[] entities,
            params Action<Board>[] customize
        ) => b.Add(Blueprints.Board, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<ProjectSeed, Board[]> b)
    {
        /// <summary>Builds one default Column under exactly one Board.</summary>
        public BiomeBuilder<ProjectSeed, Column[]> AddColumn(out Column entity) =>
            b.AddOne(Blueprints.Column, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, Column[]> AddColumn(
            out Column entity,
            Action<Column> customize
        ) => b.AddOne(Blueprints.Column, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<ProjectSeed, Column[]> AddColumns(out Column[] entities, int count) =>
            b.Add(Blueprints.Column, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, Column[]> AddColumns(
            out Column[] entities,
            params Action<Column>[] customize
        ) => b.Add(Blueprints.Column, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<ProjectSeed, Column[]> b)
    {
        /// <summary>Builds one default Card under exactly one Column.</summary>
        public BiomeBuilder<ProjectSeed, Card[]> AddCard(out Card entity) =>
            b.AddOne(Blueprints.Card, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, Card[]> AddCard(out Card entity, Action<Card> customize) =>
            b.AddOne(Blueprints.Card, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<ProjectSeed, Card[]> AddCards(out Card[] entities, int count) =>
            b.Add(Blueprints.Card, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, Card[]> AddCards(
            out Card[] entities,
            params Action<Card>[] customize
        ) => b.Add(Blueprints.Card, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<ProjectSeed, Card[]> b)
    {
        /// <summary>Builds one default ChecklistItem under exactly one Card.</summary>
        public BiomeBuilder<ProjectSeed, ChecklistItem[]> AddChecklistItem(
            out ChecklistItem entity
        ) => b.AddOne(Blueprints.Checklist, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, ChecklistItem[]> AddChecklistItem(
            out ChecklistItem entity,
            Action<ChecklistItem> customize
        ) => b.AddOne(Blueprints.Checklist, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<ProjectSeed, ChecklistItem[]> AddChecklistItems(
            out ChecklistItem[] entities,
            int count
        ) => b.Add(Blueprints.Checklist, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, ChecklistItem[]> AddChecklistItems(
            out ChecklistItem[] entities,
            params Action<ChecklistItem>[] customize
        ) => b.Add(Blueprints.Checklist, out entities, customize);
    }

    // Only the matching parent anchor offers these child methods; each Add returns a new output anchor.
    extension(BiomeBuilder<ProjectSeed, Card[]> b)
    {
        /// <summary>Builds one default Comment under exactly one Card.</summary>
        public BiomeBuilder<ProjectSeed, Comment[]> AddComment(out Comment entity) =>
            b.AddOne(Blueprints.Comment, out entity);

        /// <summary>Customizes one output before fill-only derives; the call site wins.</summary>
        public BiomeBuilder<ProjectSeed, Comment[]> AddComment(
            out Comment entity,
            Action<Comment> customize
        ) => b.AddOne(Blueprints.Comment, out entity, customize);

        /// <summary>Builds count outputs per parent, flattened in parent order.</summary>
        public BiomeBuilder<ProjectSeed, Comment[]> AddComments(
            out Comment[] entities,
            int count
        ) => b.Add(Blueprints.Comment, out entities, count);

        /// <summary>Builds one output per delegate per parent; use + to stack traits.</summary>
        public BiomeBuilder<ProjectSeed, Comment[]> AddComments(
            out Comment[] entities,
            params Action<Comment>[] customize
        ) => b.Add(Blueprints.Comment, out entities, customize);
    }

    // Scenario helpers are explicit app recipes, available only from the seed anchor.
    extension(BiomeBuilder<ProjectSeed, ProjectSeed> b)
    {
        /// <summary>Builds a complete default 6-level branch and returns its leaf.</summary>
        public BiomeBuilder<ProjectSeed, ChecklistItem[]> AddReadyChecklist(
            out ChecklistItem leaf
        ) =>
            b.AddWorkspace(out _)
                .AddProject(out _)
                .AddBoard(out _)
                .AddColumn(out _)
                .AddCard(out _)
                .AddChecklistItem(out leaf);
    }
}
