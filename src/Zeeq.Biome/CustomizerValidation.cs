namespace Zeeq.Biome;

internal static class CustomizerValidation
{
    internal static void Validate<TDelegate>(TDelegate[] customize)
        where TDelegate : Delegate
    {
        ArgumentNullException.ThrowIfNull(customize);
        for (var i = 0; i < customize.Length; i++)
        {
            if (customize[i] is null)
            {
                throw new ArgumentException($"Customizer at index {i} is null.", nameof(customize));
            }
        }
    }
}
