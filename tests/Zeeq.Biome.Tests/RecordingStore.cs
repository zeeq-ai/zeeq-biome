using Zeeq.Biome;

namespace Zeeq.Biome.Tests;

internal sealed class RecordingStore : IBiomeStore
{
    internal List<object> Entities { get; } = [];
    internal int Calls { get; private set; }
    internal CancellationToken Token { get; private set; }
    internal Action? OnSave { get; init; }

    public ValueTask SaveAsync(IReadOnlyList<object> entities, CancellationToken cancellationToken)
    {
        Calls++;
        Token = cancellationToken;
        Entities.AddRange(entities);
        OnSave?.Invoke();
        return ValueTask.CompletedTask;
    }
}
