namespace Neemle.OrmLite;

internal sealed class JoinReadExecution
{
    private readonly JoinedReadPlan _plan;
    private readonly CancellationToken _token;
    private readonly StreamCursor<JoinedRow> _cursor;
    private readonly StreamCleanup _cleanup = new();

    internal JoinReadExecution(JoinedReadPlan plan, CancellationToken token)
    {
        _plan = plan;
        _token = token;
        var input = plan.Root.Input;
        var snapshot = new StreamReadSnapshot<JoinedRow>(plan.Statement, plan.Layout.Read,
            new(input.Database.DataSource, input.Database.Provider), input.Borrowed);
        _cursor = new StreamCursor<JoinedRow>(snapshot, plan.ValidateCurrent);
    }

    internal async Task<List<JoinedRow>> RunAsync()
    {
        var rows = new List<JoinedRow>();
        Exception? failure = null;
        try
        {
            Check();
            if (!_plan.Empty)
            {
                await _cursor.OpenAsync(_token);
                await ReadAsync(rows);
            }
        }
        catch (Exception error) { failure = JoinReadFailures.Observed(error, _token, _cleanup); }
        await _cleanup.Attempt(() => new ValueTask(_cursor.CleanupAsync(_cleanup)));
        if (failure is not null) throw JoinReadFailures.Retain(failure, _cleanup.Errors);
        if (_cleanup.Errors.Count != 0)
            throw JoinReadFailures.Retain(new JoinReadException(JoinError.ProviderFailure, _cleanup.Errors[0]),
                _cleanup.Errors);
        return Publish(rows);
    }

    private async Task ReadAsync(List<JoinedRow> rows)
    {
        while (true)
        {
            Check();
            var available = await _cursor.Reader.ReadAsync(_token);
            Check();
            if (!available) return;
            if (rows.Count == _plan.MaxResultRows) throw new JoinReadException(JoinError.LimitExceeded);
            var row = _plan.Layout.Read(_cursor.Reader);
            Check();
            rows.Add(row);
        }
    }

    private void Check()
    {
        _token.ThrowIfCancellationRequested();
        _cursor.Validate();
    }

    // The plan is validated when the read is compiled, before the command (StreamCursor) and here, before the rows are
    // published: a fixed number of times per execution, never per row.
    private List<JoinedRow> Publish(List<JoinedRow> rows)
    {
        try { Check(); _plan.ValidateCurrent(); return rows; }
        catch (Exception error) { throw JoinReadFailures.Observed(error, _token, _cleanup); }
    }
}
