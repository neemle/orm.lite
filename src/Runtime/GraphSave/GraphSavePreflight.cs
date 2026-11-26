namespace Neemle.OrmLite;

internal static class GraphSavePreflight
{
    internal static GraphSavePrepared Prepare<T>(GraphSaveRequest<T> request, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            Validate(request);
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new GraphSaveCapture(request.Plan, cancellationToken).Read(request.Root, request.RootIntent);
            GraphSaveForeignKeys.Bind(snapshot);
            var canonical = GraphSaveDeduplication.Collapse(snapshot);
            var writes = GraphSaveTopological.Order(GraphSaveDependencies.Create(snapshot, canonical));
            var anchors = Anchors(snapshot, canonical);
            var links = GraphSaveLinks.Capture(snapshot);
            new GraphSaveBudget(request.Plan.Limits).Work(writes.Count, links.Count, anchors.Count);
            cancellationToken.ThrowIfCancellationRequested();
            return new GraphSavePrepared(snapshot, writes, anchors, links);
        }
        catch (GraphSaveException) { throw; }
        catch (OperationCanceledException cause)
        {
            throw new GraphSaveCanceledException(GraphSaveOutcome.NotStarted, cause, cancellationToken);
        }
        catch (Exception cause) { throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest, cause); }
    }

    private static void Validate<T>(GraphSaveRequest<T> request) where T : class
    {
        if (request?.Root is null || request.Plan?.Intents is null || request.Plan.Permissions is null)
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        GraphSaveRefusal.Intent(request.RootIntent);
        if (request.Plan.TransactionMode != GraphSaveTransactionMode.Owned)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedTransaction);
    }

    private static IReadOnlyList<GraphSaveNode> Anchors(GraphSaveSnapshot snapshot,
        IReadOnlyDictionary<GraphSaveNode, GraphSaveNode> canonical) => Array.AsReadOnly(snapshot.Edges
        .SelectMany(e => new[] { canonical[e.Source], canonical[e.Target] })
        .Where(n => n.Intent == GraphSaveIntent.Unchanged).Distinct().Order(GraphSaveNodeOrder.Instance).ToArray());
}
