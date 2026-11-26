# Queries

[Documentation](README.md)

Start with the `Note` entity and open database from the [quickstart](quickstart.md).

## Build and run a query

A query is a builder: `Where`, `Take`, `Skip`, `OrderBy`, `ForUpdate`, `IncludingDeleted` and the other builder methods
change the query and return it, while terminals (`ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, `UpdateAsync` and
the other `Compile*`, `Render*` and `*Async` calls) never change it, so one query can feed several terminals; a query is
not thread-safe. `FirstOrDefaultAsync` reads at most the smaller of one and `Take` rows, so after `Take(0)` it returns
null.

## Paging and ordering: which terminals take them

`Take`, `Skip` and the `OrderBy` family (`OrderBy`, `ThenBy`, `OrderByPredicate`, the JSON-key orders) shape the
rows a query reads. What a terminal does with them depends on what it is:

- Reads that return rows (`ToListAsync`, `ProjectAsync`, `ProjectionAsync`, `FirstOrDefaultAsync`) honour all three.
- `ScalarAsync` reads the column of the first row of the order: it honours `OrderBy`/`ThenBy` and `Skip`, and a
  `Take(n)` counts as at most one row, so `Take(0)` reads no row and gives the default value.
- Writes (`UpdateAsync`, `DeleteAsync`, `UpdateReturningAsync`, `DeleteReturningAsync`, and the hidden `PurgeAsync()`)
  refuse a query that carries `Take`, `Skip` or any order, with an `InvalidOperationException` that names the
  operation and every such clause. Nothing is executed and no row changes, on a `[SoftDelete]` entity too. A write
  affects every row that matches `Where`, and PostgreSQL has no `LIMIT` on `UPDATE` or `DELETE`, so a page of rows
  cannot be written without naming them.
- Aggregates (`CountAsync`, `AnyAsync`, `SumAsync`, `AggregatesAsync` and its `Compile`/`Render` forms) refuse `Take`
  and `Skip` the same way. An order alone is allowed and changes nothing.
- `GroupBy(...)` refuses a `Take`, `Skip` or order already set on the query, and a clause added to the query after
  `GroupBy` is refused when the grouping is compiled or run. Put them on the grouping, whose own `Take`, `Skip`,
  `OrderBy` and `OrderByKey` work.
## Lock rows in a unit of work

- A row lock (`ForUpdate`, `ForNoKeyUpdate`) is taken by the reads that return rows (`ToListAsync`, `ProjectAsync`,
  `ProjectionAsync`, `FirstOrDefaultAsync`). Every other terminal refuses a query that carries one, with an
  `InvalidOperationException` that names the operation and the lock: `CountAsync`, `AnyAsync`, `SumAsync`,
  `AggregatesAsync` and its `Compile`/`Render` forms, `ScalarAsync` and its `Compile`/`Render` forms, `GroupBy(...)`,
  and the writes. They return no row to lock or lock the rows they change by themselves, so the lock was dropped
  without a word before. Read the rows with the lock in a unit of work, then count, sum or write by their keys.
## Ordering and NULL values

- Every plain order term (`OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `OrderByPredicate`,
  `ThenByPredicate`) says `NULLS LAST`, ascending and descending, on both providers, so `OrderBy(x => x.Due).Take(n)`
  reads the same rows on SQLite and on PostgreSQL. Before, SQLite put NULLs first in both directions and PostgreSQL
  last when ascending and first when descending. The grouped orders already said the same. There is no plain form for
  NULLs first. A PostgreSQL index serves such an order when its term states the same placement
  (`Nulls = NullsPlacement.Last` on the `[Index]`; a descending term is `DESC NULLS LAST`), a capability SQLite
  refuses by name (`IndexNullsOrdering`).

## Update or delete a limited set

To delete or update the first N rows, select their keys first and write by those keys:

```csharp
var ids = await NoteOrm.Query(db).Where(n => n.Stars < 3).OrderBy(n => n.Title).Take(100)
    .ProjectAsync([n => n.Id], r => r.GetGuid(0));
await NoteOrm.Query(db).Where(n => ids.Contains(n.Id)).DeleteAsync();
```

or filter the write with `WhereIn` over a subquery that carries the order and the `Take`, which deletes exactly the
first N rows of that order:

```csharp
await NoteOrm.Query(db)
    .WhereIn<Note>(n => n.Id, sub => sub.Where(n => n.Stars < 3).OrderBy(n => n.Title).Take(100), n => n.Id)
    .DeleteAsync();
```

A negative `Take` or `Skip` is an `ArgumentOutOfRangeException` at the call, on the query and on a grouping alike:
PostgreSQL rejects `LIMIT -1` and SQLite reads it as no limit, so the same query would mean two things. Zero is allowed.

## Read a single value

`ScalarAsync<TCol>` converts the value it reads the way the row mapper does, so the same call gives the same answer on
SQLite and on PostgreSQL. It reads int, long, short, byte and the other integral types (an integer that does not fit is
an `OverflowException`), float, double, decimal (the scale is kept), bool (a stored 0 or 1), Guid, DateTime (a stored
`Z` timestamp is read as UTC), DateOnly, an enum (the exact member name), `Jsonb` and string, each also as a nullable
type. It refuses a lossy or unknown conversion with an `InvalidCastException` that names the target and the type the
query returned and never the stored value: a fractional number into an integer, a double into a decimal, a number
into a string or an enum, an integer other than 0 and 1 into a bool, text that is not in the form the library writes,
and a type such as `DateTimeOffset`. A query that matches no row and a stored NULL both give `default(TCol)`; read a
nullable type to get `null`, and the two cannot be told apart. `ScalarAsync<object?>` returns the raw provider value,
which is provider-dependent by design: an integer column is a `long` on SQLite and an `int` on PostgreSQL.
