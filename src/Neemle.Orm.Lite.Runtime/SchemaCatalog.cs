using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public sealed record CatalogColumn(string Name, string NativeType, bool Nullable, int KeyPosition);

public sealed record CatalogIndex(string Name, bool Unique, bool Partial, IReadOnlyList<string> Terms, string Definition);

public sealed record CatalogForeignKey(
    string? Name,
    IReadOnlyList<string> Columns,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns,
    ReferentialAction OnDelete,
    ReferentialAction OnUpdate);

public sealed record CatalogView(string Name, string? Marker);

public interface ISchemaCatalog
{
    Task<bool> TableExistsAsync(string table, CancellationToken ct = default);

    Task<CatalogView?> ViewAsync(string view, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogColumn>> ColumnsAsync(string table, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogIndex>> IndexesAsync(string table, CancellationToken ct = default);

    Task<IReadOnlyList<CatalogForeignKey>> ForeignKeysAsync(string table, CancellationToken ct = default);
}
