namespace Neemle.Orm.Lite.Tests.Ports;

// The catalog of the coupling the neutrality ratchet counts. Every marker is a pattern, so a new spelling of the same
// coupling needs a new line here, and the ratchet then fails until the allow-list records it or the source loses it.
internal static class CouplingMarkers
{
    private const string PgSqlText = @"::jsonb|\bpg_[a-z_]+|\bjsonb_[a-z_]+|\bto_jsonb"
        + @"|make_interval|FOR NO KEY UPDATE|IS NOT DISTINCT FROM|= ANY\(";

    internal static readonly IReadOnlyList<CouplingMarker> All =
    [
        // The legacy PostgreSQL dialect classes and the interfaces that served them beside the provider path.
        new CouplingMarker("pg-type", CouplingTokenKind.Identifier, @"\APg[A-Z]"),
        new CouplingMarker("legacy-dialect", CouplingTokenKind.Identifier, @"\A(ISqlDialect|IDdlDialect)\z"),

        // The id of one provider spelled in the neutral sources, as a constant or as a literal.
        new CouplingMarker("postgres-id", CouplingTokenKind.Identifier, @"\APostgresId\z"),
        new CouplingMarker("postgres-id", CouplingTokenKind.Literal, @"\Apostgres\z"),

        // PostgreSQL SQL text and PostgreSQL type vocabulary written in the neutral sources.
        new CouplingMarker("pg-sql-text", CouplingTokenKind.Literal, PgSqlText),
        new CouplingMarker("jsonb-type-name", CouplingTokenKind.Literal, @"\Ajsonb\z"),

        // The statement that stamps the acting user on a connection, and the neutral entry points that run it.
        new CouplingMarker("actor-stamp-sql", CouplingTokenKind.Literal, "set_config"),
        new CouplingMarker("actor-stamp-api", CouplingTokenKind.Identifier, @"\A(StampStatement|StampAsync)\z"),

        // The migration host that exists for one provider: its advisory lock key, its lock statements, and the
        // second path that only a provider without a planner takes.
        new CouplingMarker(
            "migration-lock-key", CouplingTokenKind.Identifier, @"\A(LockKey|LockMigrations|UnlockMigrations)\z"),
        new CouplingMarker("no-planner-path", CouplingTokenKind.Identifier, @"\A(RunLockedAsync|locked)\z"),
    ];

    internal static IReadOnlyList<string> Ids() =>
        All.Select(m => m.Id).Distinct().Order(StringComparer.Ordinal).ToList();
}
