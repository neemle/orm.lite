# Migrations

[Documentation](README.md)

## Write and apply a migration

The generator writes a baseline migration for your entities and one for their indexes; your own changes are classes
marked `[Migration]` that write only typed operations, so the same migration runs on either provider:

    using Neemle.OrmLite;

    [Migration("0100_welcome_note", Reversibility.Reversible)]
    public sealed class WelcomeNote : Migration
    {
        private static readonly Guid Id = new("31e91bb8-5c08-45c4-8f80-59f8359429d0");

        public override void Up(MigrationBuilder b) =>
            b.InsertData("notes", ["id", "title", "stars"], [Id, "welcome", 0]);
        public override void Down(MigrationBuilder b) => b.DeleteData("notes", "id", Id);
    }

A row can also be given as an object, `b.InsertData(NoteOrm.Descriptor, NoteOrm.Values, new Note { ... })`; the object has the shape of the entity today, so
such a migration must come after the ones that add its columns.

`InsertData` and `DeleteData` write their values as quoted, invariant literals (a Guid, a date or time and an enum name
become single-quoted text, and a number keeps its invariant spelling whatever the thread culture is); an empty
`InsertData`, a `null` in `DeleteData`, a non-finite number, a value of a type with no literal form, and a string or
character that holds a NUL character or a lone surrogate is refused when the migration is built. On PostgreSQL a string
that holds a backslash is written as an escape string (`E'a\\b'`), so its meaning does not depend on
`standard_conforming_strings`; on SQLite a backslash is an ordinary character and nothing changes.

On SQLite a decimal value in `InsertData`, `DeleteData` or a view definition is written as quoted text (`'1.10'`), so
it keeps its scale and every digit the way a bound parameter does; on PostgreSQL it stays a bare numeric literal.

A null array given to `InsertData` or `DeleteData` is an `ArgumentNullException`, and negative zero is written as `0`.
Two limits of the targets are accepted, not worked around: SQLite stores an integer in 64 bits, so a `ulong` above
`long.MaxValue` is read back as a REAL (store it as text or a decimal); PostgreSQL keeps a moment to the microsecond,
so ticks beyond the sixth fractional digit are rounded when the row is stored.

`db.MigrateAsync()` applies every version not yet recorded in `schema_migrations`, in ordinal order, one
transaction each, for every assembly of the process that holds `[Table]` or `[Migration]` classes and references the
package: the build registers each assembly's generated contributions at start-up, with no reflection, so nothing is
named in code. `db.MigrateAsync(contributions)` applies an explicit list instead (a test, or an assembly the process has
not loaded yet), and `MigrationRunner.RunAsync(dataSource, provider, contributions)` is what both call;
`RevertAsync` runs a migration's declared or derived `Down` and refuses a migration declared `Irreversible`. A
migration whose operations are all reversible gets its
`Down` derived. Raw statement text exists only as the provider-scoped `MigrationBuilder.Raw(providerId, text)`
escape hatch.
Because the baseline is one recorded version, a table for an entity added after the first run is not created by
re-running: write a `[Migration]` with `CreateTable` for it.

`MigrationRunner.RunAsync` refuses a list that carries one version twice, applied or not, by name, before it touches
the database and before any provider refusal. On PostgreSQL it takes the advisory lock before it creates the history
table, so replicas migrating an empty database together queue on the lock, and it renders and validates every pending
migration before the first one begins, so a render or capability refusal in a later migration leaves the earlier ones
unapplied, as on SQLite; a SQL error raised while a migration runs still leaves the earlier ones committed.

## Migration operations and rollback

A migration body is written with the typed
`MigrationBuilder` (`CreateTable`, `AddColumn`, `CreateIndex`, `InsertData`, `DeleteData` and the rest); each
operation is recorded as a node, classified as reversible, lossy or unknown, and rendered by the provider. A
migration whose operations are all reversible gets its `Down` derived from `Up` by `Migration.Down`. `MigrationBuilder.Raw(providerId, text)`
is the one escape hatch for a statement the builder has no method for: it names the provider the text is written
for, it is refused on any other provider, and it has no derived inverse (a migration that holds it writes its own
`Down` or is declared `Irreversible`). You own that statement's dialect and safety. A column type or default is written
with `ColumnType` and `Defaults`, never as text, and none of the builders' convenience methods takes statement text
other than `Raw`. The node-level door is `Add(Node)`: it accepts a `RawSql` node, and a `RawSql` with no provider is
text that no provider owns, so write one only on purpose.

On SQLite the runner has no advisory lock to take: each version runs in one IMMEDIATE transaction behind the
single-writer gate, checks the history again inside it, and is validated before any of its statements runs, so an
operation the provider cannot run (a decimal wider than 28 digits, a session lock) is refused by
name and changes nothing. The operations `ALTER TABLE` cannot express (adding, dropping or converting a foreign key,
a column with an expression default or a primary key) are planned against the live schema as a table rebuild: foreign
key enforcement is switched off outside the transaction, the table is copied into a new one, and its indexes,
triggers and views are put back (views first, then the triggers: one defined on a view, one that names a view and one
on another table that names the rebuilt table are dropped before the views and recreated after them), then
`foreign_key_check` runs before the commit. `ADD COLUMN IF NOT EXISTS`,
`DROP COLUMN IF EXISTS` and the marker comments of the live-only index and soft-delete conversions are likewise
emulated from the catalog, and so is the generated index reconciliation: an index is created where its table and columns
exist, skipped where they do not, and a live-only index whose columns already carry a total unique index is refused by
name. On PostgreSQL an index is looked up through its own table, and an index name that another relation of the same
schema already holds is refused by name, because index names are schema-global there; table, index and column names with
quotes, percent signs, whitespace or dollar tags are quoted and escaped in every generated block, and a table lookup
accepts only an ordinary or partitioned table.
`MigrationRunner.RevertAsync` runs a migration's declared or derived `Down` and forgets
its version on either provider; a migration declared `Irreversible` is refused by name before anything runs, and the
generated baseline (it would drop every managed table with its rows) and the generated index reconciliation are
declared `Irreversible`. A declaration that is not a named member of `Reversibility` is refused the same way, and
`MigrationRunner.RunAsync` refuses it, a blank version and a default entry before any statement.
`MigrationOps.Audit(contribution, provider)` compares a hand-built entry's declaration with its operations (a
`Reversible` entry that holds an operation with no inverse and no `Down`, an `Irreversible` entry whose every operation
can be inverted); the runner does not refuse on a finding, so call it from a startup check or a test of your own.

Recovery plan of the three generated `Irreversible` entries (the migrations are applied by `MigrateAsync`, which a
deployment already authorises; none of them records the state it replaced):

- **Baseline (`0000_...`)** creates the managed tables and join tables with `CREATE TABLE IF NOT EXISTS`, so it
  touches no existing row. Its inverse would drop every managed table with its rows, which is why it is not
  reversible. Recovery: restore the database from a backup; on an empty database running the migration again creates
  the tables.
- **Index reconciliation (`9900_orm_index_reconcile_...`)** repairs index definitions only: it drops an index whose
  definition drifted and creates it again, and touches no table data. Recovery: migrate again (the version changes when
  the set of declared indexes changes) or recreate the index by hand from its declaration.
- **Row history (`9900_orm_history_reconcile_...`)** installs `row_history`, the capture function and its triggers; no
  table data is touched and rows already captured are kept. Recovery: migrate again, or drop the triggers and the
  function and migrate again.

The two version names of `9900_` carry a hash of what the assembly declares: the index reconcile the set of declared
indexes (table, index name, uniqueness, live column, columns), the row history the tables it covers and their key,
soft, tenant and unversioned columns, plus the actor setting name when it is not the default `ormlite.actor` (the
capture function names it, so a changed setting installs the function again). The hash is computed by the core from
those declarations alone, so the names are
the same whichever providers the process loads. An earlier build hashed the statements the PostgreSQL provider rendered whenever
that provider was loaded, so a PostgreSQL database created by that build carries the other names;
`MigrateAsync` there finds both versions unrecorded, runs them once and records the new names beside the old rows, and
the next run does nothing. Both installs are written to be run again (`CREATE TABLE IF NOT EXISTS`, `CREATE OR REPLACE`
for the function and the triggers, and an index is only dropped when its stored definition drifted from the
declaration), so the one repeated run changes nothing that is already right. SQLite already used the declaration hash.
