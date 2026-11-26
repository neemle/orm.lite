namespace Neemle.OrmLite;

public static class GraphSave
{
    public static Task<GraphSaveResult> SaveAsync<T>(OrmContext context, GraphSaveRequest<T> request,
        CancellationToken ct = default) where T : class
    {
        try
        {
            if (context?.DataSource is null) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
            var prepared = GraphSavePreflight.Prepare(request, ct);
            var statements = GraphSaveCompile.All(prepared, context.Provider);
            var result = GraphSavePublication.Prepare(prepared);
            return new GraphSaveExecution(context, statements, result, ct).RunAsync();
        }
        catch (GraphSaveException) { throw; }
        catch (GraphSaveCanceledException) { throw; }
        catch (OperationCanceledException cause)
        {
            throw new GraphSaveCanceledException(GraphSaveOutcome.NotStarted, cause, ct);
        }
        catch (Exception cause) { throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest, cause); }
    }
}
