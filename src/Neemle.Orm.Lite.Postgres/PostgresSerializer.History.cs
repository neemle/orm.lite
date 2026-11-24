using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Postgres;

public sealed partial class PostgresSerializer
{
    public override void Visit(HistoryChangedKeys node)
    {
        W.KwCall(Kw.Array).Kw(Kw.Select).Bare(PostgresHistoryDataLiterals.ChangedKeysAlias).Kw(Kw.From).Func("jsonb_object_keys");
        Write(node.Document);
        W.Close().Kw(Kw.As).Bare(PostgresHistoryDataLiterals.ChangedKeysAlias).Kw(Kw.Order).Kw(Kw.By)
            .Bare(PostgresHistoryDataLiterals.ChangedKeysAlias).Close();
    }

    public override void Visit(HistoryStateQuery node) => WriteState(node);

    public override void Visit(HistoryRevertUpdate node)
    {
        var state = node.State;
        W.Atom(PostgresHistoryDataLiterals.RevertHead).Open();
        WriteState(state);
        W.Close().Atom(PostgresHistoryDataLiterals.RevertTarget).Ident(state.Table).Atom(PostgresHistoryDataLiterals.RevertAlias);
        var first = true;
        foreach (var assignment in node.Assignments.Cast<ColumnRef>())
        {
            if (!first) W.Comma();
            first = false;
            W.Ident(assignment.Column).Atom("=").Bare("r").Dot().Ident(assignment.Column);
        }
        if (node.StampColumn is { } stamp)
        {
            if (!first) W.Comma();
            W.Ident(stamp).Atom("=");
            Write(new ClockNow());
        }
        W.Atom(PostgresHistoryDataLiterals.RevertSource).Func("jsonb_populate_record").Kw(Kw.Null).Attach("::").Ident(state.Table)
            .Comma().Atom(PostgresHistoryDataLiterals.RevertRecord).Close().Kw(Kw.As).Bare("r");
        W.Kw(Kw.Where).Bare("t").Dot().Ident(state.KeyColumn).Atom("=");
        Write(state.RowId);
        if (state.Tenant is not null)
        {
            W.Kw(Kw.And).Bare("t").Dot().Ident(state.TenantColumn!).Atom("=");
            Write(state.Tenant);
        }
        W.Kw(Kw.And).Bare("t").Dot().Ident(node.SoftColumn).Kw(Kw.Is).Kw(Kw.Null);
    }

    private void WriteState(HistoryStateQuery node)
    {
        W.Atom(PostgresHistoryDataLiterals.StateHead);
        Write(node.SourceTable);
        W.Atom(PostgresHistoryDataLiterals.StateRowId);
        Write(node.RowId);
        W.Atom(PostgresHistoryDataLiterals.StateVersion);
        Write(node.Version);
        W.Atom(PostgresHistoryDataLiterals.StateMerged).Kw(Kw.Array).OpenBracket();
        WriteSeparated(node.Unversioned.Cast<ColumnRef>().Select(c => (Node)new StringLit(c.Column)));
        W.CloseBracket().Attach("::").Atom("text").OpenBracket().CloseBracket();
        W.Kw(Kw.From).Ident(node.Table).Bare("live").Kw(Kw.Where).Bare("live").Dot().Ident(node.KeyColumn).Atom("=");
        Write(node.RowId);
        if (node.Tenant is not null)
        {
            W.Kw(Kw.And).Bare("live").Dot().Ident(node.TenantColumn!).Atom("=");
            Write(node.Tenant);
        }
        W.Atom(PostgresHistoryDataLiterals.StateVersionExists);
        Write(node.Version);
        W.Atom(PostgresHistoryDataLiterals.StateVersionTable);
        Write(node.SourceTable);
        W.Atom(PostgresHistoryDataLiterals.StateVersionRow);
        Write(node.RowId);
        W.Close();
    }

    public override void Visit(ExistingTablesQuery node)
    {
        W.Kw(Kw.Select).Bare("t").Kw(Kw.From).Func("unnest");
        Write(node.Tables);
        W.Attach("::").Atom(PostgresHistoryDataLiterals.TextArray).Close().Kw(Kw.As).Bare("t").Kw(Kw.Where)
            .Kw(Kw.Exists).Open().Kw(Kw.Select).Int(1).Kw(Kw.From).Bare("pg_class").Bare("c").Kw(Kw.Where)
            .Bare("c.oid").Atom("=").Func("to_regclass").Func("quote_ident").Bare("t").Close().Close()
            .Kw(Kw.And).Bare("c.relkind").Kw(Kw.In).Open().Str("p").Comma().Str("r").Close().Close();
    }

    public override void Visit(HistorySoftDeleteLookup node)
    {
        W.Atom(PostgresHistoryDataLiterals.SoftDeleteHead).Func("unnest");
        Write(node.Tables);
        W.Attach("::").Atom(PostgresHistoryDataLiterals.TextArray).Comma();
        Write(node.Ids);
        W.Attach("::").Atom(PostgresHistoryDataLiterals.UuidArray).Close().Atom(PostgresHistoryDataLiterals.SoftDeleteTail);
    }

    public override void Visit(ConstraintColumnsOf node)
    {
        var constraint = node.Source == ConstraintSource.Constraint;
        W.Atom(constraint ? PostgresHistoryDataLiterals.ConstraintHead : PostgresHistoryDataLiterals.IndexHead);
        Write(node.Target);
        W.Atom(constraint ? PostgresHistoryDataLiterals.ConstraintTail : PostgresHistoryDataLiterals.IndexTail);
    }

    protected override void WriteTrashLabel(string column)
    {
        W.Func("left").Ident(column).Attach("::").Atom("text").Comma().Int(TrashLabelChars).Close();
    }

    protected override void WriteTypedNull(CastType type)
    {
        W.Kw(Kw.Null).Attach("::");
        WriteCastType(type);
    }
}
