namespace Zeeq.Biome;

/// <summary>Defines explicit factories for entities, children, and input-based aggregates.</summary>
/// <remarks>Entities, parents, and aggregate inputs must be reference types so mutation and graph identity remain stable. Seeds may have any shape.</remarks>
public static class Blueprint
{
    /// <summary>Defines an entity factory.</summary>
    public static Blueprint<TSeed, T> For<TSeed, T>(Func<BiomeContext<TSeed>, T> factory)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(factory, [], [], false);
    }

    /// <summary>Defines a child factory requiring a typed parent anchor.</summary>
    public static ChildBlueprint<TSeed, TParent, T> ChildOf<TSeed, TParent, T>(
        Func<TParent, ChildContext<TSeed, TParent>, T> factory
    )
        where TParent : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new(factory, [], [], false);
    }

    /// <summary>Defines an aggregate factory whose customizers operate on its input.</summary>
    public static InputBlueprint<TSeed, TInput, T> FromInput<TSeed, TInput, T>(
        Func<TInput> newInput,
        Func<TInput, BiomeContext<TSeed>, T> build
    )
        where TInput : class
        where T : class, IBiomeAggregate
    {
        ArgumentNullException.ThrowIfNull(newInput);
        ArgumentNullException.ThrowIfNull(build);
        return new(newInput, build, [], [], false);
    }
}
