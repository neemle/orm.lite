namespace Neemle.OrmLite;

internal static class QueryJoinCapture
{
    internal static QueryJoinSnapshot Capture<T>(Query<T> query, string alias) where T : class
    {
        if (query is null) throw new JoinReadException(JoinError.InvalidRequest);
        try
        {
            var input = query.JoinSnapshot();
            if (input.Masked || input.HasPolicy()) throw new JoinReadException(JoinError.UnsupportedVisibility);
            if (input.Unsupported) throw new JoinReadException(JoinError.UnsupportedSource);
            JoinAliases.Validate(alias, input.Database.Provider, JoinError.InvalidRequest);
            var entity = JoinModel.Find(typeof(T));
            var columns = entity.Snapshot();
            var root = JoinSourceNodes.Copy(input.Root, entity.Metadata, columns);
            var result = new QueryJoinSnapshot(input, entity.Metadata, columns, alias, root);
            result.Current();
            return result;
        }
        catch (JoinReadException) { throw; }
        catch (Exception error) { throw new JoinReadException(JoinError.UnsupportedSource, error); }
    }
}
