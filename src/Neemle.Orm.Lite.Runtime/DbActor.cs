#nullable enable
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record DbActorValue
{
    private DbActorValue(string stamp) => Stamp = stamp;

    public string Stamp { get; }

    public static DbActorValue User(Guid userId) => new(userId.ToString("D"));

    public static DbActorValue System { get; } = new("system");
}

public static class DbActor
{
    public static string StampStatement => OrmLiteSettings.StampStatement;

    private static readonly AsyncLocal<DbActorScope?> s_scope = new();

    internal static DbActorValue? Current
    {
        get
        {
            var scope = s_scope.Value;
            while (scope is { IsDisposed: true }) scope = scope.Previous;
            return scope?.Value;
        }
    }

    public static IDisposable EnterUser(Guid userId) => Enter(DbActorValue.User(userId));

    public static IDisposable EnterSystem() => Enter(DbActorValue.System);

    public static async Task StampAsync(DbConnection connection, DbActorValue value, CancellationToken ct = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = StampStatement;
        command.AddParam("v", value.Stamp);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static DbActorScope Enter(DbActorValue value)
    {
        var scope = new DbActorScope(value, s_scope.Value);
        s_scope.Value = scope;
        return scope;
    }

    private sealed class DbActorScope(DbActorValue value, DbActorScope? previous) : IDisposable
    {
        private int _disposed;

        public DbActorValue Value { get; } = value;

        public DbActorScope? Previous { get; } = previous;

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (ReferenceEquals(s_scope.Value, this)) s_scope.Value = Previous;
        }
    }
}
