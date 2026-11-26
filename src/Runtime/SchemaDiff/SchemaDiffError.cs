namespace Neemle.OrmLite;

public enum SchemaDiffError
{
    InvalidRequest, Ownership, UnknownMetadata, UnsupportedProvider, UnsupportedDifference, InvalidArtifact, ReviewMismatch,
    ScopeMismatch, Drift, HistoryMismatch, RequiresManualMigrationFirst, FreshLineageUnsupported, BaselineOwnsObjects,
    Capability, LimitExceeded, ProviderFailure, CommitUnknown, RollbackUnknown, RestorationUnknown, CleanupFailure,
}
