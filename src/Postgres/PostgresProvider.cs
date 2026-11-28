using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider : ISqlProvider, ISchemaDiffProvider
{
    public static readonly PostgresProvider Instance = new();

    public string Id => "postgres";

    // NAMEDATALEN is 64 and the server keeps the first 63 bytes of an identifier.
    public const int MaxIdentifierBytes = 63;

    public CapabilitySet Capabilities { get; } = CapabilitySet.Everything()
        .With(Capability.WalCheckpoint, Support.None)
        .With(Capability.IntegrityCheck, Support.None)
        .WithMaxIdentifierBytes(MaxIdentifierBytes);

    public int MaxBindParameters => 65535;

    public ISerializer CreateSerializer() => new PostgresSerializer();

    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) =>
        PostgresActorStamp.StampAsync(connection, actor, ct);

    public void ValidateDataSource(string name, DbDataSource source)
    {
        if (SetsFlag(source.ConnectionString, "noresetonclose"))
            throw new InvalidOperationException(
                $"GatedDataSource refuses the {name} data source: its connection string sets No Reset On Close, "
                + "which would let a pooled connection keep the previous caller's "
                + OrmLiteSettings.ActorSetting + ". Remove No Reset On Close from the connection string.");
        if (SetsFlag(source.ConnectionString, "multiplexing"))
            throw new InvalidOperationException(
                $"GatedDataSource refuses the {name} data source: its connection string sets Multiplexing, "
                + "which lets the statements of one caller run on different physical connections, so the "
                + OrmLiteSettings.ActorSetting + " stamp of one session can miss its statements or reach another "
                + "caller's. Remove Multiplexing from the connection string.");
    }

    private static bool SetsFlag(string? connectionString, string normalisedKey)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return false;
        }

        foreach (string key in builder.Keys)
        {
            var normalised = string.Concat(key.Where(c => c is not (' ' or '_'))).ToLowerInvariant();
            if (normalised != normalisedKey) continue;
            var value = Convert.ToString(builder[key], CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant();
            if (value is not ("false" or "no" or "off" or "0")) return true;
        }
        return false;
    }

    public Node Lower(Node tree) => PostgresLowering.Lower(tree);

    public ISchemaCatalog CreateCatalog(DbConnection connection, DbTransaction? transaction = null) =>
        new PostgresCatalog(this, connection, transaction);

    private IMigrationPlanner? _planner;

    public IMigrationPlanner Planner => _planner ??= new PostgresMigrationPlanner(this);
}
