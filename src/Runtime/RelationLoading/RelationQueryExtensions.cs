namespace Neemle.OrmLite;

public static class RelationQueryExtensions
{
    public static Task<List<T>> ToListAsync<T>(this Query<T> query, RelationReadOptions options,
        CancellationToken ct = default) where T : class => PolicyReader<T>.RowsAsync(query, options, ct);

    public static Task<T?> FirstOrDefaultAsync<T>(this Query<T> query, RelationReadOptions options,
        CancellationToken ct = default) where T : class => PolicyReader<T>.FirstAsync(query, options, ct);
}
