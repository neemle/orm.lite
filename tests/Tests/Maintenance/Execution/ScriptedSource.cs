using System.Data.Common;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// The source a test's GatedDataSource opens from. Its connections run the scripts the test sets per command text; a
// text with no script answers an empty reader, zero rows affected or a null scalar.
internal sealed class ScriptedSource(EffectLog log) : DbDataSource
{
    private readonly Dictionary<string, Func<Dispatch, ScriptedCommand, Task<object?>>> _scripts =
        new(StringComparer.Ordinal);

    private readonly List<Dispatch> _dispatches = [];

    internal EffectLog Log { get; } = log;

    // Runs inside every open, before it completes; it receives the open's token.
    internal Func<CancellationToken, Task>? OnOpen { get; set; }

    // Runs when a command's timeout is set, after the value is stored.
    internal Action<ScriptedCommand, int>? OnTimeoutSet { get; set; }

    // Runs when a connection or a command is disposed or a reader closed, before it is logged done: "dispose <what>".
    internal Func<string, Task>? OnDispose { get; set; }

    internal List<ScriptedConnection> Connections { get; } = [];

    // Texts whose failure ExecuteNonQueryAsync throws synchronously, before it returns a task.
    internal HashSet<string> Synchronous { get; } = new(StringComparer.Ordinal);

    // Every connection, command and reader made, with how many times it was disposed.
    private readonly Dictionary<object, int> _resources = new(ReferenceEqualityComparer.Instance);

    internal IReadOnlyDictionary<object, int> Resources
    {
        get
        {
            lock (_resources) return new Dictionary<object, int>(_resources, ReferenceEqualityComparer.Instance);
        }
    }

    internal void Track(object resource)
    {
        lock (_resources) _resources[resource] = 0;
    }

    internal IReadOnlyList<Dispatch> Dispatches
    {
        get
        {
            lock (_dispatches) return [.. _dispatches];
        }
    }

    public override string ConnectionString => "Data Source=scripted";

    internal void Script(string text, Func<Dispatch, ScriptedCommand, Task<object?>> script) => _scripts[text] = script;

    internal void Script(string text, Func<Dispatch, object?> script) =>
        _scripts[text] = (d, _) => Task.FromResult(script(d));

    internal IReadOnlyList<Dispatch> Of(string text) => [.. Dispatches.Where(d => d.Text == text)];

    internal async Task<object?> RunAsync(ScriptedCommand command, CancellationToken token)
    {
        Dispatch dispatch;
        lock (_dispatches)
        {
            var attempt = _dispatches.Count(d => d.Text == command.CommandText) + 1;
            dispatch = new Dispatch(command.CommandText, command.CommandTimeout, token, attempt);
            _dispatches.Add(dispatch);
        }
        Log.Add("exec " + command.CommandText);
        return _scripts.TryGetValue(command.CommandText, out var script) ? await script(dispatch, command) : null;
    }

    internal async Task DisposedAsync(object resource, string what)
    {
        lock (_resources) _resources[resource]++;
        if (OnDispose is { } hook) await hook(what);
        Log.Add("dispose " + what);
    }

    protected override DbConnection CreateDbConnection()
    {
        var connection = new ScriptedConnection(this);
        lock (Connections) Connections.Add(connection);
        return connection;
    }
}
