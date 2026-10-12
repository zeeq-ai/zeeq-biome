using System.Linq.Expressions;

namespace Zeeq.Biome;

/// <summary>Configures defaults and persistence; frozen on first use and safe to share across sessions.</summary>
public sealed class BiomeOptions<TSeed>
{
    private readonly object gate = new();
    private bool frozen;
    internal Func<TSeed, IEnumerable<object>> GetSeedEntities { get; private set; } = _ => [];
    internal List<Action<object, BiomeContext<TSeed>>> Defaults { get; } = [];
    internal List<
        Func<BiomeSaveContext<TSeed>, CancellationToken, ValueTask>
    > BeforeHooks { get; } = [];
    internal List<Func<BiomeSaveContext<TSeed>, CancellationToken, ValueTask>> AfterHooks { get; } =
    [];
    internal TimeSpan Precision { get; private set; } = TimeSpan.FromTicks(10);
    internal Func<string, string> NewId { get; private set; } =
        prefix => $"{prefix}_{Guid.CreateVersion7():N}";

    /// <summary>Selects baseline rows to persist before created entities.</summary>
    /// <remarks>Evaluated and snapshotted once at Biome.Start. Directly selected references qualify as session-owned parents for Under. Aggregate parts are expanded only for persistence and are not automatically tracked as parents. Null selected entries are rejected.</remarks>
    public BiomeOptions<TSeed> SeedEntities(Func<TSeed, IEnumerable<object>> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return Change(() => GetSeedEntities = entities);
    }

    /// <summary>Fills an unset property on entities matching a class or interface.</summary>
    public BiomeOptions<TSeed> Default<TBase, TProp>(
        Expression<Func<TBase, TProp>> property,
        Func<TBase, BiomeContext<TSeed>, TProp> value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        var derive = new Derivation<TSeed, TBase, TProp>(property, value);
        return Change(() =>
            Defaults.Add(
                (entity, ctx) =>
                {
                    if (entity is TBase typed)
                    {
                        derive.Apply(typed, ctx);
                    }
                }
            )
        );
    }

    /// <summary>Registers a hook that may add rows before the store saves.</summary>
    public BiomeOptions<TSeed> BeforeSave(
        Func<BiomeSaveContext<TSeed>, CancellationToken, ValueTask> hook
    )
    {
        ArgumentNullException.ThrowIfNull(hook);
        return Change(() => BeforeHooks.Add(hook));
    }

    /// <summary>Registers a hook invoked after the store successfully saves.</summary>
    public BiomeOptions<TSeed> AfterSave(
        Func<BiomeSaveContext<TSeed>, CancellationToken, ValueTask> hook
    )
    {
        ArgumentNullException.ThrowIfNull(hook);
        return Change(() => AfterHooks.Add(hook));
    }

    /// <summary>Sets UTC time truncation precision; zero disables truncation. Default is one microsecond.</summary>
    public BiomeOptions<TSeed> TimePrecision(TimeSpan precision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(precision, TimeSpan.Zero);
        return Change(() => Precision = precision);
    }

    /// <summary>Overrides ID generation for all factories and customizers in a session.</summary>
    public BiomeOptions<TSeed> Ids(Func<string, string> newId)
    {
        ArgumentNullException.ThrowIfNull(newId);
        return Change(() => NewId = newId);
    }

    internal void Freeze()
    {
        lock (gate)
        {
            frozen = true;
        }
    }

    private BiomeOptions<TSeed> Change(Action change)
    {
        lock (gate)
        {
            if (frozen)
            {
                throw new InvalidOperationException(
                    "Biome options are frozen after Biome.Start; configure them before first use."
                );
            }
            change();
        }
        return this;
    }
}

/// <summary>Provides the mutable persistence rows and scenario inspection to save hooks.</summary>
public sealed class BiomeSaveContext<TSeed>
{
    internal BiomeSaveContext(BiomeSession<TSeed> session, List<object> entities)
    {
        Session = session;
        Entities = entities;
    }

    /// <summary>Gets the application's seed.</summary>
    public TSeed Seed => Session.Seed;

    /// <summary>Gets rows to save; before-save hooks may add or remove rows.</summary>
    public List<object> Entities { get; }

    /// <summary>Gets the session for inspecting created entities.</summary>
    public BiomeSession<TSeed> Session { get; }
}
