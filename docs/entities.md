# Entities and mapping

[Documentation](README.md)

## Declare a table

Mark a class or record with `[Table]` and identify its key with `[Key]`.

```csharp
using System;
using Neemle.OrmLite;

[Table("notes")]
public sealed class Note
{
    [Key] public Guid Id { get; set; }
    public string Title { get; init; } = "";

    [CalculatedLower(nameof(Title))]
    public string LowerTitle { get; set; } = "";
}
```

The [SQLite quickstart](quickstart.md) shows how to open the database and use the generated API.

## Calculated read fields

`[CalculatedLower(nameof(Title))]` maps `LowerTitle` to the database's `lower(title)` result.
Include `Note` in your normal generated schema/migration setup, then pass the composed context:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

public static class NoteExample
{
    public static async Task<List<Note>> ReadAsync(OrmContext db)
    {
        await NoteOrm.InsertAsync(db, new Note { Id = Guid.NewGuid(), Title = "MiXeD" });
        // The inserted note's LowerTitle is "mixed" in the returned list.
        return await NoteOrm.Query(db).ToListAsync();
    }
}
```

- Generated full-entity list, keyed and relation readers populate the value in the existing read;
  no extra query is needed. Values assigned by the caller are ignored by generated inserts and updates.
- `Lower` is the only supported calculation. Its source must be a physical CLR `string` property on
  the same model. A nullable source requires a nullable calculated property; SQL `NULL` stays `null`.
  Non-ASCII case conversion follows the provider and can differ between PostgreSQL and SQLite.
- An optional `[Column("lower_alias")]` sets a distinct, nonempty read alias. Calculated fields add no
  physical column, write value or persisted-history value and cannot be keys, index terms or `NotAColumn`.
- `Redact` or `RedactWhen` on the source refuses a full-entity calculated read before SQL executes,
  including conditional masks. The calculated value cannot reveal masked input.
- Filtering, ordering, grouping and custom projection of a calculated property are unsupported;
  physical-column translation refuses it. Raw/custom readers must supply the computed value and alias
  expected by the mapper. Stored generated columns, raw SQL expressions and chained calculations are unsupported.

### Runnable calculated-field cases

The [public demo](../examples/calculated/README.md#build-private-packages) builds private packages from this
checkout. Its instructions run the same cases with [JIT](../examples/calculated/README.md#run-with-jit) or
[NativeAOT](../examples/calculated/README.md#run-the-same-cases-with-nativeaot) on SQLite and PostgreSQL.

- [ReadCases.cs](../examples/calculated/ReadCases.cs): full and keyed reads, nullable/empty text, and valid
  physical-column filtering, ordering and custom projection.
- [RelationCases.cs](../examples/calculated/RelationCases.cs): reference, collection and many-to-many reads.
- [BoundaryCases.cs](../examples/calculated/BoundaryCases.cs): ignored caller-assigned values and exclusion
  from physical metadata, generated DDL and the actual database schema.
- [RefusalCases.cs](../examples/calculated/RefusalCases.cs): calculated-property filter/order/projection
  refusals and direct/conditional source redaction, with no SQL execution.

## Generated API

| Generated type | What it contains |
| --- | --- |
| `<Entity>Orm` | Entity descriptor, typed mapper, CRUD methods, and `Query`. The runtime builds statement nodes from the descriptor. |
| `<AssemblyName>Schema` | Entity descriptors for the assembly. |
| `<AssemblyName>Migrations` | Baseline, indexes, index reconciliation, history triggers, and user migrations in `Contributions`. |

Apply schema changes with `db.MigrateAsync()`. See [Migrations](migrations.md) for versions, transactions,
and rollback.

## Column and entity rules

- A column is a public instance property with an accessible getter and an accessible setter or init accessor.
  The generator supports `required` and init-only properties.
- A private setter or unusable key produces [`ORMLITE023`](generator-diagnostics.md#entity-mapping).
  Indexers are excluded. A type the generator cannot construct produces `ORMLITE024`.
- Navigation keys must map to compatible columns. Only one managed entity may map a given table.
- Set [`OrmLiteProviders`](build-properties.md#provider-capability-checks) to validate declarations and calls
  against the providers you target.

For a complete example of navigation properties and foreign keys, see [Relations](relations.md).

For more models, see the [demo entities](../demo/Tracker/) and [generator diagnostics](generator-diagnostics.md).
