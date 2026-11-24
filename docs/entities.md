# Entities and mapping

[Documentation](README.md)

Declare a model with `[Table]` and mark its key with `[Key]`. The [quickstart](quickstart.md) shows a complete example.

At build time the generator reads the classes and records marked `[Table]` and `[Migration]` and emits,
through Neemle.CSharpComposer,
into the consuming project one `<Entity>Orm` class per table (a typed `Map`, an `EntityDescriptor`, insert, update, get,
delete, typed `Query`; the statements are node trees the core builds from the descriptor, never text in the generated
source), a `<AssemblyName>Schema` holding the descriptors, and an `<AssemblyName>Migrations` class whose `Contributions`
list the generated baseline, indexes, index reconciliation and history triggers and your hand-written migrations; the
core runner applies every pending one (through `db.MigrateAsync()`, which collects what each assembly registered, or
through `MigrationRunner.RunAsync(dataSource, provider, contributions)`) in version order,
one transaction each, under the provider's migration lock. Hand-written
migrations are classes with `Up` and `Down` that must say whether they are reversible. Mistakes in the declarations
(an unmapped property type, a `[SoftDelete]` entity without a tenant or a key, an entity with more than one key, a
property the generated code cannot assign or read, an entity type it cannot construct or, for a static one, generate, a
navigation whose foreign key is missing or of another type than the key it points at (`ORMLITE026`), two managed
entities on one table (`ORMLITE027`), a repeated or empty migration version (`ORMLITE028`, `ORMLITE029`), a `[Migration]` class the generated code cannot
build (`ORMLITE030`), an irreversible migration with a `Down`) are build errors with `ORMLITEnnn` ids, not runtime surprises. A property is a
column when it is a public instance property with an accessible get and a set or init accessor, so init-only and
`required` properties are supported and a private setter is refused (`ORMLITE023`); indexers and static classes are
not columns or entities (`ORMLITE024` names a static, abstract or not constructible entity). The `OrmLiteProviders`
build property names the
providers a project targets; with a capability manifest for each (an `ormlite.capabilities.<provider>.txt` additional
file) the generator refuses a declaration a provider cannot serve (`ORMLITE015`) and a call of a member marked
`[RequiresCapability]` or of a migration member that needs a capability (foreign key and view operations, decimal and
jsonb columns, nulls ordering, collation and `LowerOf` index terms, the `LiveOnlyConversionDdl`, `ForeignKeyActionDdl`
and `SoftDeleteColumnDdl` helpers) (`ORMLITE016`); `ORMLITE017` compares a migration's declared reversibility with its `Up`.

For more models, see the [demo entities](../demo/Tracker/) and [generator diagnostics](generator-diagnostics.md).
