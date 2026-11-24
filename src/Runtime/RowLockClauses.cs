#nullable enable
namespace Neemle.OrmLite;

    // ForUpdate and ForNoKeyUpdate of a query: a dialect that has no such lock refuses, because an unlocked read
    // would silently serve a caller that depends on the lock.
    internal sealed class RowLockClauses
    {
        private readonly RenderState _state;
        private readonly SelectClauses _select;

        internal RowLockClauses(RenderState state, SelectClauses select)
        {
            _state = state;
            _select = select;
        }

        internal void ForUpdate()
        {
            if (!_state.Allows(Capability.RowLock))
                throw _state.Refusal(Capability.RowLock, "ForUpdate",
                    $"{_state.ProviderName} has no row lock; a caller that needs one cannot be served by an "
                    + "unlocked read.");
            _select.LockRows();
        }

        internal void ForNoKeyUpdate()
        {
            if (!_state.Allows(Capability.KeyPreservingRowLock))
                throw _state.Refusal(Capability.KeyPreservingRowLock, "ForNoKeyUpdate",
                    $"{_state.ProviderName} has no key-preserving row lock; a caller that needs to serialise writers "
                    + "of a row WITHOUT blocking a foreign key that references it cannot be served by an unlocked "
                    + "read, and must not be silently handed the stronger lock.");
            _select.LockRowsKeyPreserving();
        }
    }
