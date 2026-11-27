namespace Neemle.OrmLite;

internal static class JoinedReadCompiler
{
    internal static JoinedReadPlan Build(JoinQueryPlan query, JoinSelection selection)
    {
        if (query is null || selection is null) throw new JoinReadException(JoinError.InvalidRequest);
        try
        {
            JoinPlanChecks.Full(query);
            var context = new JoinCompilationContext(query);
            var projection = new JoinProjectionCompiler(context, query);
            var layout = projection.Build(selection);
            var tree = new JoinSelectCompiler(query, context).Build(projection.Columns);
            var statement = StatementCompiler.Compile(tree, context.Provider, StatementKind.Read);
            if (statement.Parameters.Count > Math.Min(query.Limits.MaxParameters, context.Provider.MaxBindParameters))
                throw new JoinReadException(JoinError.ParameterCapacity);
            return new JoinedReadPlan(statement, layout, query);
        }
        catch (JoinReadException) { throw; }
        catch (Exception error) { throw new JoinReadException(JoinError.UnsupportedOperation, error); }
    }
}
