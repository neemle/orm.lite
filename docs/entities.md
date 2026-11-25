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
}
```

The [SQLite quickstart](quickstart.md) shows how to open the database and use the generated API.

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

For more models, see the [demo entities](../demo/Tracker/) and [generator diagnostics](generator-diagnostics.md).
