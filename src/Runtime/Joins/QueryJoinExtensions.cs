namespace Neemle.OrmLite;

public static class QueryJoinExtensions
{
    public static JoinSource<T> AsJoinSource<T>(this Query<T> query, string alias) where T : class =>
        new(QueryJoinCapture.Capture(query, alias));
}
