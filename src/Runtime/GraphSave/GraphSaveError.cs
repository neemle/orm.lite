namespace Neemle.OrmLite;

public enum GraphSaveError
{
    InvalidRequest, UnsupportedMapping, MissingIntent, UnusedIntent, ActionNotAllowed, DuplicateIdentity,
    ConflictingForeignKey, UnchangedForeignKeyBinding, Cycle, LimitExceeded, MissingExisting, MissingAnchor,
    UnsupportedTransaction, ProviderFailure, RollbackUnconfirmed, CommitUnknown, PostCommitCleanup, NoPublishedBinding,
}
