namespace Neemle.OrmLite;

internal sealed class GraphSaveExecution(OrmContext context, IReadOnlyList<GraphSaveStatement> statements,
    GraphSaveResult result, CancellationToken cancellationToken)
{
    private UnitOfWork? _unit;
    private Exception? _primary;
    private System.Data.Common.DbConnection? _connection;
    private GraphSaveOutcome _outcome = GraphSaveOutcome.NotStarted;
    private bool _commitStarted;

    internal async Task<GraphSaveResult> RunAsync()
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _unit = await UnitOfWork.BeginGraphSaveAsync(context, cancellationToken);
            _connection = _unit.Connection;
            var session = new GraphSaveSession(_unit);
            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await GraphSaveCommands.RunAsync(session, statement, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _commitStarted = true;
            await _unit.CommitAsync();
            _outcome = GraphSaveOutcome.Committed;
        }
        catch (Exception primary) { await FailedAsync(primary); }
        if (_unit is not null)
            _primary = await GraphSaveOwnedCleanup.DisposeAsync(_unit, _connection, _primary);
        if (_primary is not null) throw GraphSaveFailure.Wrap(_primary, _outcome, result);
        return result;
    }

    private async Task FailedAsync(Exception primary)
    {
        _primary = primary;
        if (_unit is null) return;
        if (_unit.Committed) { _outcome = GraphSaveOutcome.Committed; return; }
        if (_commitStarted) { _outcome = GraphSaveOutcome.Unknown; return; }
        try
        {
            await _unit.Transaction.RollbackAsync(CancellationToken.None);
            _outcome = GraphSaveOutcome.RolledBack;
        }
        catch (Exception rollback)
        {
            PolicyCleanup.Retain(primary, rollback);
            _primary = new GraphSaveException(GraphSaveError.RollbackUnconfirmed, GraphSaveOutcome.Unknown, primary);
            _outcome = GraphSaveOutcome.Unknown;
        }
    }

}
