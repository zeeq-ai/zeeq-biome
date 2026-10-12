using Microsoft.EntityFrameworkCore;

namespace Zeeq.Biome.EntityFrameworkCore;

/// <summary>Biome conveniences for <see cref="DbContext" />.</summary>
public static class DbContextExtensions
{
    extension(DbContext db)
    {
        /// <summary>Wraps the context as a biome store, so wrappers can write <c>Biome.Start(seed, options, db.AsBiomeStore())</c>.</summary>
        public EfCoreStore AsBiomeStore() => new(db);
    }
}
