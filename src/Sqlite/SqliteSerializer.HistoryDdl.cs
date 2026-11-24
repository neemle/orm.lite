using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteSerializer : ISqliteHistoryVisitor
{
    private static readonly string[] InsertedColumns =
    [
        "tenant_id", "source_table", "row_id", "op", "old_values", "actor_user_id", "actor_kind", "xact_id", "changed_at",
    ];

    public void Visit(SqliteHistoryStore node)
    {
        W.Ddl(DdlKw.Create).Ddl(DdlKw.Table);
        WriteIfNotExists();
        W.Bare(SqliteHistoryDataLiterals.Table).Open();
        W.Bare("id").Atom("integer").Ddl(DdlKw.Primary).Kw(Kw.Key).Atom("AUTOINCREMENT").Comma();
        W.Bare("tenant_id").Atom("text").Kw(Kw.Null).Comma();
        W.Bare("source_table").Atom("text").Kw(Kw.Not).Kw(Kw.Null).Comma();
        W.Bare("row_id").Atom("text").Kw(Kw.Not).Kw(Kw.Null).Comma();
        WriteChecked("op", SqliteHistoryDataLiterals.OpUpdate, SqliteHistoryDataLiterals.OpSoftDelete, SqliteHistoryDataLiterals.OpRestore);
        W.Comma().Bare("old_values").Atom("text").Kw(Kw.Not).Kw(Kw.Null).Atom("CHECK").Open().Func("json_valid").Bare("old_values").Close().Close().Comma();
        W.Bare("actor_user_id").Atom("text").Kw(Kw.Null).Comma();
        WriteChecked("actor_kind", SqliteHistoryDataLiterals.KindUser, SqliteHistoryDataLiterals.KindSystem, SqliteHistoryDataLiterals.KindUnknown);
        W.Comma().Bare("xact_id").Atom("integer").Kw(Kw.Not).Kw(Kw.Null).Comma();
        W.Bare("changed_at").Atom("text").Kw(Kw.Not).Kw(Kw.Null).Ddl(DdlKw.Default);
        Write(new ClockNow());
        W.Close();
    }

    private void WriteChecked(string column, params string[] allowed)
    {
        W.Bare(column).Atom("text").Kw(Kw.Not).Kw(Kw.Null).Atom("CHECK").Open().Bare(column).Kw(Kw.In).Open();
        var first = true;
        foreach (var value in allowed)
        {
            if (!first) W.Comma();
            first = false;
            W.Str(value);
        }
        W.Close().Close();
    }

    public void Visit(SqliteHistoryTrigger node)
    {
        var suffix = node.Kind == SqliteTriggerKind.Update ? SqliteHistoryDataLiterals.TriggerSuffixUpdate : SqliteHistoryDataLiterals.TriggerSuffixDelete;
        W.Ddl(DdlKw.Create).Atom("TRIGGER").Ident(SqliteHistoryDataLiterals.TriggerPrefix + node.Table + suffix).Atom("AFTER");
        if (node.Kind == SqliteTriggerKind.Delete) WriteDeleteTrigger(node);
        else WriteUpdateTrigger(node);
    }

    private void WriteDeleteTrigger(SqliteHistoryTrigger node)
    {
        W.Kw(Kw.Delete).Ddl(DdlKw.On).Ident(node.Table).Atom("BEGIN");
        W.Kw(Kw.Delete).Kw(Kw.From).Bare(SqliteHistoryDataLiterals.Table).Kw(Kw.Where).Bare("source_table").Atom("=").Str(node.Table)
            .Kw(Kw.And).Bare("row_id").Atom("=");
        WriteOld(node.KeyColumn);
        W.Semicolon().Atom("END");
    }

    private void WriteUpdateTrigger(SqliteHistoryTrigger node)
    {
        W.Kw(Kw.Update).Ddl(DdlKw.On).Ident(node.Table).Atom("WHEN");
        var first = true;
        foreach (var column in node.Versioned)
        {
            if (!first) W.Kw(Kw.Or);
            first = false;
            WriteChanged(column);
        }
        W.Atom("BEGIN").Kw(Kw.Insert).Kw(Kw.Into).Bare(SqliteHistoryDataLiterals.Table).Open();
        for (var i = 0; i < InsertedColumns.Length; i++)
        {
            if (i > 0) W.Comma();
            W.Bare(InsertedColumns[i]);
        }
        W.Close().Kw(Kw.Select);
        if (node.TenantColumn is { } tenant) WriteNew(tenant);
        else W.Kw(Kw.Null);
        W.Comma().Str(node.Table).Comma();
        WriteOld(node.KeyColumn);
        W.Comma();
        WriteOperation(node.SoftColumn);
        W.Comma();
        WriteDelta(node.Versioned);
        W.Comma();
        WriteActor(user: true);
        W.Comma();
        WriteActor(user: false);
        W.Comma().Func(SqliteFunctions.Transaction).Close().Comma().Func(SqliteFunctions.TransactionTime).Close();
        W.Kw(Kw.From).Open().Kw(Kw.Select).Func(SqliteFunctions.Actor).Close().Kw(Kw.As).Bare("a").Close().Bare("x");
        W.Semicolon().Atom("END");
    }

    private void WriteOld(string column) => W.Bare("OLD").Dot().Ident(column);

    private void WriteNew(string column) => W.Bare("NEW").Dot().Ident(column);

    private void WriteChanged(string column)
    {
        WriteOld(column);
        W.Kw(Kw.Is).Kw(Kw.Not);
        WriteNew(column);
    }

    private void WriteOperation(string soft)
    {
        W.Kw(Kw.Case).Kw(Kw.When);
        WriteOld(soft);
        W.Kw(Kw.Is).Kw(Kw.Null).Kw(Kw.And);
        WriteNew(soft);
        W.Kw(Kw.Is).Kw(Kw.Not).Kw(Kw.Null).Kw(Kw.Then).Str(SqliteHistoryDataLiterals.OpSoftDelete).Kw(Kw.When);
        WriteOld(soft);
        W.Kw(Kw.Is).Kw(Kw.Not).Kw(Kw.Null).Kw(Kw.And);
        WriteNew(soft);
        W.Kw(Kw.Is).Kw(Kw.Null).Kw(Kw.Then).Str(SqliteHistoryDataLiterals.OpRestore)
            .Kw(Kw.Else).Str(SqliteHistoryDataLiterals.OpUpdate).Kw(Kw.End);
    }

    private void WriteDelta(IReadOnlyList<string> columns)
    {
        W.Open().Kw(Kw.Select).Func("json_group_object").Bare("d.k").Comma().Bare("d.v").Close().Kw(Kw.From).Open();
        var first = true;
        foreach (var column in columns)
        {
            if (!first) W.Atom("UNION").Atom("ALL");
            first = false;
            W.Kw(Kw.Select).Str(column).Kw(Kw.As).Bare("k").Comma();
            WriteOld(column);
            W.Kw(Kw.As).Bare("v").Kw(Kw.Where);
            WriteChanged(column);
        }
        W.Close().Kw(Kw.As).Bare("d").Close();
    }

    private void WriteActor(bool user)
    {
        W.Kw(Kw.Case).Kw(Kw.When).Func("lower").Bare("x.a").Close().Atom("GLOB").Str(SqliteHistoryDataLiterals.UuidPattern).Kw(Kw.Then);
        if (user)
        {
            W.Func("lower").Bare("x.a").Close().Kw(Kw.End);
            return;
        }
        W.Str(SqliteHistoryDataLiterals.KindUser).Kw(Kw.When).Bare("x.a").Atom("=").Str(SqliteHistoryDataLiterals.SystemActor)
            .Kw(Kw.Then).Str(SqliteHistoryDataLiterals.KindSystem).Kw(Kw.Else).Str(SqliteHistoryDataLiterals.KindUnknown).Kw(Kw.End);
    }
}
