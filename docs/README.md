# Neemle.OrmLite documentation

[Project overview](../README.md)

## Get started

- [Installation](installation.md) — requirements, provider packages, and the local NuGet feed.
- [Quickstart](quickstart.md) — create a SQLite database and run your first query.
- [Configuration](configuration.md) — configure connection settings and switch to PostgreSQL.

## Use ORM.lite

- [Entities and mapping](entities.md) — declare tables and understand the generated API.
- [Relations](relations.md) — load related entities, manage join rows, and add foreign keys.
- [Save an explicit graph](relations.md#7-save-an-explicit-related-graph) — choose mutation intents and relation permissions, with a runnable JIT/NativeAOT case.
- [Queries](queries.md) — filter, project, page, aggregate, and lock rows.
- [Streaming reads](streaming.md) — process rows on demand and observe cleanup, cancellation, and resource ownership.
- [Migrations](migrations.md) — apply and revert schema changes and seed data.
- [Soft delete and row history](row-history.md) — read history, restore rows, and scope by tenant.
- [SQL diagnostics](diagnostics.md) — send statement and transaction events to a sink.
- [Examples](examples.md) — run the demo and find a complete example for a concrete task.

## Look up behavior

- [Provider capabilities](providers.md) — compare PostgreSQL and SQLite, including JSON and numeric differences.
- [Generator diagnostics](generator-diagnostics.md) — interpret build errors and warnings.
- [Build properties](build-properties.md) — configure provider registration, capabilities, and tenant defaults.

## Contribute

- [Build, test, and package](development.md) — repository layout, local checks, and CI.
