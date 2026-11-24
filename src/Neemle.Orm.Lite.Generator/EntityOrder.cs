using System;
using System.Collections.Immutable;
using System.Linq;
using Entity = Neemle.Orm.Lite.OrmGenerator.Entity;

namespace Neemle.Orm.Lite;

/// <summary>
/// Puts the entities in an order that does not depend on how the compiler lists the syntax trees: tables before
/// views, then by the ordinal path of the file that declares the entity, then by position in the file, then by the
/// full type name. Within one file this is the declaration order. The generated entity list, the schema constants and
/// the baseline statements follow it, so the same sources produce the same output on every machine.
/// </summary>
internal static class EntityOrder
{
    internal static ImmutableArray<Entity> Of(ImmutableArray<Entity> entities) =>
        [.. entities
            .OrderBy(e => e.IsView)
            .ThenBy(e => e.Location?.SourceTree?.FilePath ?? "", StringComparer.Ordinal)
            .ThenBy(e => e.Location?.SourceSpan.Start ?? 0)
            .ThenBy(e => e.Full, StringComparer.Ordinal)];
}
