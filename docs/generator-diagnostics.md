# Generator diagnostics

[Documentation](README.md)

Every diagnostic of the source generator and its analyzers, with the severity it is reported at. The message of each
names the entity, the member and the way out. `ORMLITE032` and `ORMLITE033` belong to the actor setting build property
(see "Build properties").

- `ORMLITE001` (error): OrmGenerator has no column mapping for a property's CLR type; the message names the supported
  types, Jsonb and [NotAColumn] as the ways out, and adds a sentence when the property is a key (no keyed api) or a
  required member (the entity cannot be built from a row). Before this refusal the type switch fell back to a text
  column, so an unmapped type compiled and ran as text.
- `ORMLITE002` (error): A [SoftDelete] entity names a soft-delete column that is missing, is not System.DateTime?, or
  is excluded from the column set.
- `ORMLITE003` (error): An Irreversible [Migration] overrides Down; an irreversible migration has no down, so the
  entry is emitted without one.
- `ORMLITE004` (error): An [Index] declares LiveOnly on an entity that is not [SoftDelete] or on an index that is not
  Unique; the diagnostic names the entity and the index.
- `ORMLITE005` (error): An [Index] declares an explicit Name that is also the name of another index of the same
  entity.
- `ORMLITE006` (error): A [Migration] declares a Reversibility value that is not a named member of
  Neemle.OrmLite.Reversibility (default, (Reversibility)0 or any unnamed number); the migration is refused and not
  emitted.
- `ORMLITE007` (error): A [SoftDelete] entity has no usable tenant: Tenant names no mapped property, a property that
  is not System.Guid or one that is a nullable System.Guid?, and Tenant = Platform is not declared; the diagnostic
  names the entity and the Tenant value.
- `ORMLITE008` (error): A [SoftDelete] entity does not have exactly one non-nullable System.Guid key property (none, a
  composite key, or a non-Guid or nullable key); the diagnostic names the entity.
- `ORMLITE009` (error): A [SoftDelete] Unversioned list names a property that is not a mapped column of the entity;
  the diagnostic names the entity and the name.
- `ORMLITE010` (error): A [SoftDelete] Label or Owners names a property that is not a mapped column of the entity; the
  diagnostic names the entity, the argument and the name.
- `ORMLITE011` (error): A [SoftDelete] Label names a mapped property that is not System.String; the diagnostic names
  the entity and the property.
- `ORMLITE012` (error): A [SoftDelete] Pinned or StatusColumns names a property that is not a mapped column of the
  entity; the diagnostic names the entity, the argument and the name.
- `ORMLITE013` (error): A [SoftDelete] StatusColumns names a column that is also in Pinned; the diagnostic names the
  entity and the property.
- `ORMLITE014` (error): The OrmLiteDefaultTenant build property is set to a value that is not a C# identifier; the
  value names the tenant property a [SoftDelete] entity carries when it declares no Tenant, and a bad value is refused
  rather than replaced by the library default.
- `ORMLITE015` (error): An entity declaration (a LiveOnly index, a Jsonb property, [SoftDelete] or a declared index)
  needs a capability that a provider named in OrmLiteProviders declares as None in its capability manifest; the
  diagnostic names the entity, the declaration, the capability and the provider.
- `ORMLITE016` (error): A call of a member marked [RequiresCapability], or of a MigrationBuilder, IndexTerm or DDL
  helper member that needs a capability (listed in BuilderCapabilities), needs a capability that a provider named in
  OrmLiteProviders declares as None in its capability manifest; the diagnostic names the member, the capability and
  the provider.
- `ORMLITE017` (warning): A [Migration] declares Reversible without a Down override while its Up holds an operation
  with no automatic inverse, or declares Irreversible while every operation of its Up has one; found by a syntactic
  scan of Up.
- `ORMLITE019` (warning): A provider named in OrmLiteProviders has no ormlite.capabilities.`<provider>`.txt additional
  file, so no declaration is checked against it.
- `ORMLITE020` (error): An [Index] declaration is not valid: a term or Where condition cannot be read (a number beyond
  64 bits and a decimal number are named as such), names a column that is not a property or column of the entity,
  repeats a column, applies COLLATE or LOWER to a non-text column, or an [Index] on a property carries terms; the
  diagnostic names the entity, the index and the reason.
- `ORMLITE021` (error): Two indexes of one entity share a name but differ in columns, order, collation, uniqueness or
  predicate; only one would be created.
- `ORMLITE022` (error): An entity declares more than one key property; no keyed get, update or delete is built.
- `ORMLITE023` (error): A property the generated code cannot read, assign or populate (accessor, init, required), or a
  [Key] property it would skip silently (get-only, set-only, not public); such a key counts as a lost key and the
  keyed get, update and delete are withheld.
- `ORMLITE024` (error): An entity type the generated code cannot build: static, abstract or no usable ctor.
- `ORMLITE025` (error): An entity the generated code cannot name (generic, file-local, unreachable).
- `ORMLITE026` (error): A relation, collection or many-to-many navigation cannot be loaded: its foreign key or inverse
  key is not a mapped property, is of a type that does not fit the key it points at, or the entity on either side has
  no key or is a view; the diagnostic names the navigation and the reason, and no Load<Navigation>Async is generated.
- `ORMLITE027` (error): Two entities with Managed = true map the same table name; the generated baseline would create
  the table twice. Each is reported and names the others.
- `ORMLITE028` (error): Two [Migration] classes declare the same version (the primary key of schema_migrations); each
  is reported and none is emitted.
- `ORMLITE029` (error): A [Migration] declares an empty or blank version; the migration is reported and not emitted.
- `ORMLITE030` (error): A [Migration] class cannot be built by the generated code: it is static, abstract, generic,
  not reachable, does not derive from Neemle.OrmLite.Migration or has no public parameterless constructor; the
  migration is reported and not emitted.
- `ORMLITE031` (error): A generator failed with an exception on this input; the diagnostic names the generator and the
  exception (a defect of the generator, not of the declarations).
- `ORMLITE034` (error): An entity name (table, view, column, index, join table) is over a provider byte limit.
