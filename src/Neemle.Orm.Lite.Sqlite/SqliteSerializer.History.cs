using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Sqlite;

public sealed partial class SqliteSerializer
{
    public override void Visit(HistoryChangedKeys node)
    {
        W.Open().Kw(Kw.Select).Func("json_group_array").Bare("k.key").Kw(Kw.Order).Kw(Kw.By).Bare("k.key").Close()
            .Kw(Kw.From).Func("json_each");
        Write(node.Document);
        W.Close().Kw(Kw.As).Bare("k").Close();
    }

    public override void Visit(HistoryStateQuery node)
    {
        W.Kw(Kw.Select).Func("json_object");
        var first = true;
        foreach (var column in node.Columns.Cast<ColumnRef>().Select(c => c.Column).Except(node.Unversioned.Cast<ColumnRef>().Select(c => c.Column)))
        {
            if (!first) W.Comma();
            first = false;
            W.Str(column).Comma();
            WriteHistoricalValue(node, column, "live");
        }
        W.Close().Kw(Kw.From).Ident(node.Table).Bare("live").Kw(Kw.Where).Bare("live").Dot().Ident(node.KeyColumn).Atom("=");
        Write(node.RowId);
        if (node.Tenant is not null)
        {
            W.Kw(Kw.And).Bare("live").Dot().Ident(node.TenantColumn!).Atom("=");
            Write(node.Tenant);
        }
        WriteVersionExists(node);
    }

    public override void Visit(HistoryRevertUpdate node)
    {
        var state = node.State;
        W.Kw(Kw.Update).Ident(state.Table).Kw(Kw.Set);
        var first = true;
        foreach (var assignment in node.Assignments.Cast<ColumnRef>())
        {
            if (!first) W.Comma();
            first = false;
            W.Ident(assignment.Column).Atom("=");
            WriteHistoricalValue(state, assignment.Column, null);
        }
        if (node.StampColumn is { } stamp)
        {
            if (!first) W.Comma();
            W.Ident(stamp).Atom("=");
            Write(new ClockNow());
        }
        W.Kw(Kw.Where).Ident(state.KeyColumn).Atom("=");
        Write(state.RowId);
        if (state.Tenant is not null)
        {
            W.Kw(Kw.And).Ident(state.TenantColumn!).Atom("=");
            Write(state.Tenant);
        }
        W.Kw(Kw.And).Ident(node.SoftColumn).Kw(Kw.Is).Kw(Kw.Null);
        WriteVersionExists(state);
    }

    private void WriteVersionExists(HistoryStateQuery node)
    {
        W.Kw(Kw.And).Kw(Kw.Exists).Open().Kw(Kw.Select).Int(1).Kw(Kw.From).Bare("row_history").Bare("v").Kw(Kw.Where)
            .Bare("v.id").Atom("=");
        Write(node.Version);
        W.Kw(Kw.And).Bare("v.source_table").Atom("=");
        Write(node.SourceTable);
        W.Kw(Kw.And).Bare("v.row_id").Atom("=");
        Write(node.RowId);
        W.Close();
    }

    private void WriteHistoricalValue(HistoryStateQuery node, string column, string? live)
    {
        W.Kw(Kw.Case).Kw(Kw.When).Kw(Kw.Exists).Open();
        WriteFirstChange(node, column, false);
        W.Close().Kw(Kw.Then).Open();
        WriteFirstChange(node, column, true);
        W.Close().Kw(Kw.Else);
        if (live is not null) W.Bare(live).Dot();
        W.Ident(column).Kw(Kw.End);
    }

    private void WriteFirstChange(HistoryStateQuery node, string column, bool value)
    {
        W.Kw(Kw.Select);
        if (value) W.Bare("e.value");
        else W.Int(1);
        W.Kw(Kw.From).Bare("row_history").Bare("h").Comma().Func("json_each").Bare("h.old_values").Close().Bare("e");
        W.Kw(Kw.Where).Bare("h.source_table").Atom("=");
        Write(node.SourceTable);
        W.Kw(Kw.And).Bare("h.row_id").Atom("=");
        Write(node.RowId);
        W.Kw(Kw.And).Bare("h.id").Atom(">=");
        Write(node.Version);
        W.Kw(Kw.And).Bare("e.key").Atom("=").Str(column);
        if (value) W.Kw(Kw.Order).Kw(Kw.By).Bare("h.id").Kw(Kw.Limit).Int(1);
    }

    public override void Visit(ExistingTablesQuery node) =>
        Write(new Select(
            [new ColumnRef("name")],
            new TableRef(SqliteDataLiterals.SchemaTable),
            new Where(
            [
                new Compare(CompareOp.Equal, new ColumnRef("type"), new StringLit(SqliteDataLiterals.TableType)),
                new AnyOf(new ColumnRef("name"), node.Tables),
            ])));

    public override void Visit(ConstraintColumnsOf node)
    {
        W.Kw(Kw.Select).Bare("x.name").Kw(Kw.From).Func("pragma_index_xinfo");
        Write(node.Target);
        W.Close().Kw(Kw.As).Bare("x").Kw(Kw.Where);
        if (node.Source == ConstraintSource.Constraint)
        {
            W.Int(0).Atom("=").Int(1);
            return;
        }
        W.Bare("x.key").Atom("=").Int(1).Kw(Kw.And).Bare("x.cid").Atom(">=").Int(0)
            .Kw(Kw.Order).Kw(Kw.By).Bare("x.seqno");
    }

    public override void Visit(HistorySoftDeleteLookup node)
    {
        if (node.Tables is not { Value: Array tables } || node.Ids is not { Value: Array ids } || tables.Length != ids.Length)
            throw new NotSupportedException($"{nameof(SqliteSerializer)} binds the soft-delete lookup only for two arrays of the same length.");
        if (tables.Length * 2 > maxBindParameters)
            throw new NotSupportedException(
                $"a soft-delete lookup of {tables.Length} rows exceeds what SQLite binds in one statement; ask for a smaller page.");
        W.Kw(Kw.Select).Bare("h.source_table").Comma().Bare("h.row_id").Comma().Bare("h.actor_user_id").Comma()
            .Bare("h.actor_kind").Comma().Bare("h.xact_id").Comma().Bare("h.changed_at")
            .Kw(Kw.From).Bare("row_history").Bare("h").Kw(Kw.Where).Bare("h.op").Atom("=").Str(HistorySoftDelete)
            .Kw(Kw.And).Open().Bare("h.source_table").Comma().Bare("h.row_id").Close().Kw(Kw.In).Open().Kw(Kw.Values);
        for (var i = 0; i < tables.Length; i++)
        {
            if (i > 0) W.Comma();
            W.Open();
            Write(new Param(node.Tables.Ordinal, tables.GetValue(i), Item(node.Tables, i)));
            W.Comma();
            Write(new Param(node.Ids.Ordinal, ids.GetValue(i), Item(node.Ids, i)));
            W.Close();
        }
        W.Close().Kw(Kw.And).Bare("h.id").Atom("=").Open().Kw(Kw.Select).Func("max").Bare("x.id").Close().Kw(Kw.From)
            .Bare("row_history").Bare("x").Kw(Kw.Where).Bare("x.source_table").Atom("=").Bare("h.source_table")
            .Kw(Kw.And).Bare("x.row_id").Atom("=").Bare("h.row_id").Kw(Kw.And).Bare("x.op").Atom("=").Str(HistorySoftDelete).Close();
    }

    private const string HistorySoftDelete = "soft_delete";

    private static string Item(Param array, int index) =>
        (array.Alias ?? "p" + array.Ordinal.ToString(CultureInfo.InvariantCulture)) + "_" + index.ToString(CultureInfo.InvariantCulture);
}
