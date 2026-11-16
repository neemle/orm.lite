; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ORMLITE001 | OrmLite | Error | OrmGenerator has no column mapping for a property's CLR type. Before this refusal the type switch fell back to a text column, so an unmapped type compiled and ran as text.
ORMLITE002 | OrmLite | Error | A [SoftDelete] entity names a soft-delete column that is missing, is not System.DateTime?, or is excluded from the column set.
ORMLITE003 | OrmLite | Error | An Irreversible [Migration] overrides Down; an irreversible migration has no down, so the entry is emitted without one.
ORMLITE004 | OrmLite | Error | An [Index] declares LiveOnly on an entity that is not [SoftDelete] or on an index that is not Unique; the diagnostic names the entity and the index.
ORMLITE005 | OrmLite | Error | An [Index] declares an explicit Name that is also the name of another index of the same entity.
ORMLITE006 | OrmLite | Error | A [Migration] declares a Reversibility value that is not a named member of Neemle.Orm.Lite.Reversibility (default, (Reversibility)0 or any unnamed number); the migration is refused and not emitted.
ORMLITE007 | OrmLite | Error | A [SoftDelete] entity has no usable tenant: Tenant names no mapped property or a property that is not System.Guid, and Tenant = Platform is not declared; the diagnostic names the entity and the Tenant value.
ORMLITE008 | OrmLite | Error | A [SoftDelete] entity does not have exactly one non-nullable System.Guid key property (none, a composite key, or a non-Guid or nullable key); the diagnostic names the entity.
ORMLITE009 | OrmLite | Error | A [SoftDelete] Unversioned list names a property that is not a mapped column of the entity; the diagnostic names the entity and the name.
ORMLITE010 | OrmLite | Error | A [SoftDelete] Label or Owners names a property that is not a mapped column of the entity; the diagnostic names the entity, the argument and the name.
ORMLITE011 | OrmLite | Error | A [SoftDelete] Label names a mapped property that is not System.String; the diagnostic names the entity and the property.
ORMLITE012 | OrmLite | Error | A [SoftDelete] Pinned or StatusColumns names a property that is not a mapped column of the entity; the diagnostic names the entity, the argument and the name.
ORMLITE013 | OrmLite | Error | A [SoftDelete] StatusColumns names a column that is also in Pinned; the diagnostic names the entity and the property.
ORMLITE014 | OrmLite | Error | The OrmLiteDefaultTenant build property is set to a value that is not a C# identifier; the value names the tenant property a [SoftDelete] entity carries when it declares no Tenant, and a bad value is refused rather than replaced by the library default.
