#nullable enable
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

    // What every write of a query shares once its tree is built: the read-only view refusal, the clause guard that
    // refuses a paged or locked query, the RETURNING capability refusal and the soft-delete form of a DELETE.
    internal sealed class WriteCompiler
    {
        private readonly RenderState _state;
        private readonly SelectClauses _select;
        private readonly SoftDeleteMeta? _softDelete;

        internal WriteCompiler(RenderState state, SelectClauses select, SoftDeleteMeta? softDelete, bool isView)
        {
            _state = state;
            _select = select;
            _softDelete = softDelete;
            IsView = isView;
        }

        private bool IsView { get; }

        internal Statement Write(Node tree) =>
            IsView
                ? throw EntityStatements.ReadOnlyView(_state.Table)
                : StatementCompiler.Compile(tree, _state.Provider, StatementKind.Write, _state.Table);

        internal Statement QueryWrite(string operation, Node tree)
        {
            if (IsView) throw EntityStatements.ReadOnlyView(_state.Table);
            ClauseGuard.Require(_select, operation, ClauseRule.Write);
            return StatementCompiler.Compile(tree, _state.Provider, StatementKind.Write, _state.Table);
        }

        internal void RequireReturning(string affected)
        {
            if (!_state.Allows(Capability.Returning))
                throw _state.Refusal(Capability.Returning, "a write that returns columns",
                    $"{_state.ProviderName} cannot return columns from a write; select the affected {affected} first, "
                    + "inside the same transaction.");
        }

        internal Node DeleteTree(Where? where, Returning? returning = null) =>
            _softDelete is null
                ? new Delete(new TableRef(_state.Table), where, returning)
                : new Update(
                    new TableRef(_state.Table),
                    [new Assignment(new ColumnRef(_state.Col(_softDelete.Column)), new ClockNow())],
                    where,
                    returning);
    }
