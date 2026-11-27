namespace Neemle.OrmLite;

public enum OrphanError
{
    InvalidRequest, UnsupportedMapping, UnsupportedVisibility, UnsupportedTransaction, UnsupportedGuard,
    ParentMissing, ParentMismatch, SnapshotConsumed, ScopeEnded, DuplicateIdentity, ConflictingIdentity,
    DesiredMembership, OwnershipChanged, StaleAuthority, Nullability, MissingSoftDelete, ProtectedRow,
    UnverifiedEffects, LimitExceeded, MissingExisting, ProviderFailure, RollbackUnconfirmed, CommitUnknown,
    PostCommitCleanup,
}
