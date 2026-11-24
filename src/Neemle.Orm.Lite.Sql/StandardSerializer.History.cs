using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sql;

public abstract partial class StandardSerializer
{
    public const int TrashLabelChars = 200;

    public abstract void Visit(HistoryChangedKeys node);

    public abstract void Visit(HistoryStateQuery node);

    public abstract void Visit(HistoryRevertUpdate node);

    public abstract void Visit(ExistingTablesQuery node);

    public abstract void Visit(ConstraintColumnsOf node);

    public abstract void Visit(HistorySoftDeleteLookup node);

    protected virtual void WriteTrashLabel(string column)
    {
        W.Func("substr").KwCall(Kw.Cast).Ident(column).Kw(Kw.As).Atom("text").Close();
        W.Comma().Int(1).Comma().Int(TrashLabelChars).Close();
    }

    protected virtual void WriteTypedNull(CastType type) => W.Kw(Kw.Null);

    public virtual void Visit(SoftTrashBranch node)
    {
        W.Kw(Kw.Select).Str(node.Entity).Kw(Kw.As).Bare("entity").Comma();
        W.Str(node.Table).Kw(Kw.As).Bare("source_table").Comma();
        W.Ident(node.KeyColumn).Kw(Kw.As).Bare("id").Comma();
        if (node.LabelColumn is { } label) WriteTrashLabel(label);
        else WriteTypedNull(CastType.Text);
        W.Kw(Kw.As).Bare("label").Comma();
        W.Ident(node.SoftColumn).Kw(Kw.As).Bare("deleted_at").Comma();
        if (node.ProjectColumn is { } project) W.Ident(project);
        else WriteTypedNull(CastType.Uuid);
        W.Kw(Kw.As).Bare("project_id").Kw(Kw.From).Ident(node.Table);
        Write(node.Filter);
    }

    public virtual void Visit(SoftTrashUnion node)
    {
        W.Kw(Kw.Select).Bare("entity").Comma().Bare("source_table").Comma().Bare("id").Comma()
            .Bare("label").Comma().Bare("deleted_at").Comma().Bare("project_id");
        W.Kw(Kw.From).Open();
        var first = true;
        foreach (var branch in node.Branches)
        {
            if (!first) W.Atom("UNION").Atom("ALL");
            first = false;
            Write(branch);
        }
        W.Close().Bare("u");
        W.Kw(Kw.Order).Kw(Kw.By).Bare("deleted_at").Kw(Kw.Desc).Comma().Bare("entity").Comma().Bare("id");
        W.Kw(Kw.Limit);
        Write(node.Limit);
        W.Kw(Kw.Offset);
        Write(node.Offset);
    }
}
