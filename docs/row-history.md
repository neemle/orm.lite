# Soft delete and row history

[Documentation](README.md)

At run time the library holds the attributes, the query layer that turns lambdas into SQL, the migration
infrastructure and the connection gate. The gate stamps the acting user onto every connection it hands out, which
is what lets the history triggers attribute a change to a person, and clears it on a lease made with no actor, so a
command run again outside a `DbActor` scope is never attributed to the previous caller; it refuses connection strings
(`No Reset On Close`, `Multiplexing`) that would let a pooled connection keep the previous caller's stamp or send a
statement to another connection than the stamped one. Soft-deleted rows are hidden from queries until you ask for
them or restore them, and a restore brings back the rows that went into the trash in the same transaction.

The tenant scope of a row's history is one rule. The history reads (`RowHistoryRead`), the revert (`RowHistoryRevert`),
the restore (`SoftRestore`) and the trash list filter on the entity's tenant column, including an entity whose tenant
column is also its key; a wrong tenant finds nothing and changes nothing. A `Platform` entity takes no tenant and
ignores one. A call that gives no tenant for a tenant entity, and an entity that names no tenant column, are refused
with `InvalidOperationException`, so a missing scope never widens a statement to every tenant.

### Row history on SQLite

`RowHistoryTriggers` is declared `Emulated` on SQLite: the history of a row, the trash, a revert and a restore of a batch
deleted together give the same results on both providers (one suite runs on SQLite always and on PostgreSQL when
`ORMLITE_TEST_POSTGRES` names a server), and the capture is done by triggers, as on PostgreSQL. What differs is how the
trigger learns who acted and in which transaction, and a few properties follow from that:

- **The capture is a trigger plus three connection functions.** `RowHistoryDdl.Install` creates `row_history` and, per
  tracked table, an `AFTER UPDATE` and an `AFTER DELETE` trigger named `orm_history_<table>_u` and `_d`, and records the
  specification they were built from in the `orm_object_markers` table (`history:<table>`). The update trigger writes
  one history row with the old values of the versioned columns that changed, as a JSON object, and the operation
  (`update`, `soft_delete` or `restore`); the delete trigger removes the history of the row, as on PostgreSQL. Re-running the install replaces
  triggers whose specification or column set differs and leaves the others alone. The trigger names are
  fixed by the table, so a trigger of your own that merely starts with `orm_history_` is never taken for one of them and
  is put back by a rebuild; a column name with a `:` or `,` is stored escaped (marker version 2). A native `ADD COLUMN`, `RENAME COLUMN`
  or `DROP COLUMN` and a table rebuild regenerate the triggers around the change; a rename of a column the specification
  is built on (the key, the soft-delete or the tenant column) is followed, and a drop of one is refused by name.
- **The functions are registered on every connection the library rents.** The trigger calls `orm_actor()`, `orm_tx()` and
  `orm_tx_at()`, which the library registers each time it opens a connection (a pool does not keep them) and removes when
  it closes it, and registers through the engine API with `SQLITE_INNOCUOUS`, so they work when `trusted_schema` is off
  and, by design, under native AOT (not yet exercised in CI). Every other function the library writes into a schema object is registered the same way
  (`orm_decimal_fits` in the CHECK of a decimal column, `orm_json_*` and `orm_epoch_millis`, `orm_ilike`, and the
  `orm_decimal_sum` and `orm_decimal_average` aggregates), so a decimal column or a `[View]` also works with
  `trusted_schema` off. The acting user is the one the caller entered with `DbActor`; with none the change is recorded
  with the author `unknown`, as on PostgreSQL. A statement outside a unit of work is its own transaction: it gets its own
  identity and time, and every statement of a unit of work shares one, which is what a batch restore matches on.
- **A writer that is not the library cannot update a tracked table: that is a property, not a defect.** A second connection
  (the `sqlite3` shell, another tool, another process) has none of the functions, so an `UPDATE` of a tracked table fails
  with `no such function: orm_actor` and changes nothing; reading, inserting and deleting still work, and a delete removes the
  history of the row. An ops tool that must update such a table has to define the same three functions.
- **`recursive_triggers` is on** in the connection string the provider builds, so an `INSERT OR REPLACE` over a tracked row
  fires the delete trigger and purges the history of the replaced row instead of handing it to the new one.
- **Limits of the JSON image.** A BLOB value cannot be recorded and aborts the update of a tracked table that holds one,
  and the state of a version is built from the stored values: a boolean is `0` or `1`, a guid or timestamp is text and a
  JSON column is its stored text, where PostgreSQL's `jsonb` row image has `true`, `false` and nested documents.
- **`ConstraintColumns.ForAsync` finds indexes on SQLite.** SQLite gives a table constraint no name of its own, so a name
  is resolved as an index (a `CREATE UNIQUE INDEX`, or the automatic index of a constraint) and a constraint name that
  is not an index name has no columns.

See the [soft delete, restore, and history demo](../demo/README.md#soft-delete-restore-and-history).
