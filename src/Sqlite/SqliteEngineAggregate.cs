using SQLitePCL;

namespace Neemle.OrmLite.Sqlite;

/// <summary>
/// An aggregate registered through the engine API with SQLITE_INNOCUOUS (see <see cref="SqliteEngineFunctions"/>).
/// Each group of rows keeps its own accumulator in the statement context; a failing step is remembered and
/// reported by the final call, as Microsoft.Data.Sqlite's CreateAggregate does.
/// </summary>
internal sealed class SqliteEngineAggregate<TState>(
    TState seed, Func<TState, sqlite3_value[], TState> step, Func<TState, object?> result)
{
    internal void Register(sqlite3 handle, string name, int arity) => SqliteEngineFunctions.Check(
        name, raw.sqlite3_create_function(handle, name, arity, SqliteEngineFunctions.Flags, null, Step, Final));

    private void Step(sqlite3_context context, object userData, sqlite3_value[] args)
    {
        var running = context.state as Running;
        if (running is null)
        {
            running = new Running(seed);
            context.state = running;
        }
        if (running.Fault is not null) return;
        try
        {
            running.Value = step(running.Value, args);
        }
        catch (Exception fault)
        {
            running.Fault = fault;
        }
    }

    private void Final(sqlite3_context context, object userData)
    {
        var running = context.state as Running ?? new Running(seed);
        try
        {
            if (running.Fault is not null) throw running.Fault;
            SqliteEngineValues.Write(context, result(running.Value));
        }
        catch (Exception fault)
        {
            raw.sqlite3_result_error(context, fault.Message);
        }
    }

    private sealed class Running(TState value)
    {
        internal TState Value { get; set; } = value;

        internal Exception? Fault { get; set; }
    }
}
