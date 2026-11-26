from pathlib import Path
root=Path('src/Runtime/SchemaDiff');header='namespace Neemle.OrmLite;\n\n'
records={
'SchemaDiffLimits':'public sealed record SchemaDiffLimits(\n    int MaxTables, int MaxColumns, int MaxCatalogObjects, int MaxOperations, int MaxStatements);',
'SchemaDiffScope':'public sealed record SchemaDiffScope(string DatabaseScopeId, string Namespace, IReadOnlyList<string> OwnedTables);',
'SchemaDiffTarget':'public sealed record SchemaDiffTarget(OrmSchema Schema, string ModelVersion, SchemaDiffScope Scope, SchemaDiffLimits Limits);',
'SchemaObjectRef':'public sealed record SchemaObjectRef(string Table, string? Column);',
'SchemaDiffChange':'public sealed record SchemaDiffChange(\n    SchemaDiffChangeKind Kind, SchemaObjectRef Object, SchemaDiffImpact Impact, string Category);',
'SchemaObservationIdentity':'public sealed record SchemaObservationIdentity(string DatabaseIdentityDigest, string ProviderProfile, bool Consistent);',
'SchemaObservationHistory':'public sealed record SchemaObservationHistory(\n    SchemaFact<bool> HistoryTableExists, SchemaFact<IReadOnlyList<string>> AppliedVersions);',
'SchemaDiffInspection':'public sealed record SchemaDiffInspection(SchemaDiffTarget Target, string ExpectedProviderProfile);',
'SchemaStorageType':'public sealed record SchemaStorageType(string CanonicalStorage, int? Precision, int? Scale);',
'SchemaDefault':'public sealed record SchemaDefault(SchemaDefaultKind Kind);',
'SchemaObjectStamp':'public sealed record SchemaObjectStamp(string Kind, string Name, string DefinitionDigest);',
'SchemaObservedColumn':'public sealed record SchemaObservedColumn(\n    string Name, SchemaFact<SchemaStorageType> Type, SchemaFact<bool> Nullable,\n    SchemaFact<int> KeyPosition, SchemaObservedColumnFacts Facts);',
'SchemaObservedColumnFacts':'public sealed record SchemaObservedColumnFacts(\n    SchemaFact<SchemaDefault> Default, SchemaFact<bool> Generated,\n    SchemaFact<bool> Identity, SchemaFact<string?> Collation);',
'SchemaObservedTable':'public sealed record SchemaObservedTable(\n    string Name, SchemaFact<bool> Exists, IReadOnlyList<SchemaObservedColumn> Columns, SchemaObservedTableFacts Facts);',
'SchemaObservedTableFacts':'public sealed record SchemaObservedTableFacts(\n    SchemaFact<IReadOnlyList<SchemaObjectStamp>> Indexes, SchemaFact<IReadOnlyList<SchemaObjectStamp>> Constraints,\n    SchemaFact<IReadOnlyList<SchemaObjectStamp>> Triggers, SchemaFact<IReadOnlyList<SchemaObjectStamp>> DependentObjects);',
'SchemaDiffUnitResult':'public sealed record SchemaDiffUnitResult(string Version, SchemaUnitOutcome Outcome, SchemaRestoration Restoration);',
}
for name,text in records.items():(root/(name+'.cs')).write_text(header+text+'\n')
enums={
'SchemaKnowledge':'Known, Unknown, Unsupported',
'SchemaDiffStatus':'NoOp, Changes, Incomplete, ManualRequired',
'SchemaDefaultKind':'None, Zero, False, EmptyText, CurrentTime, Unsupported',
'SchemaDiffImpact':'Additive, Destructive, CompatibilityRisk, Unknown, Preserved',
'SchemaDiffChangeKind':'CreateTable, AddNullableColumn, DropTable, DropColumn, PossibleRename, TypeChange, NullabilityChange,\n    DefaultChange, KeyOrConstraintChange, IndexOrTriggerChange, UnknownMetadata, PreserveExternal',
'SchemaRestoration':'NotRequired, Restored, Unknown',
'SchemaUnitOutcome':'NotStarted, RolledBack, Committed, Unknown',
'SchemaDiffApplyOutcome':'NotStarted, Applied, AlreadyApplied, CoveredByCurrentBaseline, PartiallyApplied, Unknown',
'SchemaDiffError':'InvalidRequest, Ownership, UnknownMetadata, UnsupportedProvider, UnsupportedDifference, InvalidArtifact, ReviewMismatch,\n    ScopeMismatch, Drift, HistoryMismatch, RequiresManualMigrationFirst, FreshLineageUnsupported, BaselineOwnsObjects,\n    Capability, LimitExceeded, ProviderFailure, CommitUnknown, RollbackUnknown, RestorationUnknown, CleanupFailure',
}
for name,text in enums.items():(root/(name+'.cs')).write_text(header+'public enum '+name+'\n{\n    '+text+',\n}\n')
(root/'.editorconfig').write_text('# Pinned SchemaDiff public API stays beside OrmSchema in Neemle.OrmLite.\n[*.cs]\ndotnet_diagnostic.IDE0130.severity = none\n')
