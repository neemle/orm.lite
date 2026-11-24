using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sql;

namespace Neemle.Orm.Lite.Postgres;

public sealed partial class PostgresSerializer : IPostgresVisitor
{
    private const string Dollar = "$";

    private const string TagSuffix = "x";

    public override void Visit(TypeRef node)
    {
        switch (node.Kind)
        {
            case ColumnType.Uuid:
                W.Atom("uuid");
                break;
            case ColumnType.Json:
                W.Atom("jsonb");
                break;
            case ColumnType.Timestamp:
                W.Atom("timestamp").Atom("with").Atom("time").Atom("zone");
                break;
            default:
                base.Visit(node);
                break;
        }
    }

    public override void Visit(SessionLock node)
    {
        W.Kw(Kw.Select).Func(node.Op switch
        {
            SessionLockOp.Wait => "pg_advisory_lock",
            SessionLockOp.TryAcquire => "pg_try_advisory_lock",
            SessionLockOp.Release => "pg_advisory_unlock",
            _ => throw new NotSupportedException($"no spelling for the session lock operation {node.Op}."),
        });
        Write(node.Key);
        W.Close();
    }

    public void Visit(PgName node) => W.Bare(node.Text);

    public void Visit(PgRelation node)
    {
        W.Bare(node.Relation);
        if (node.Alias is not null) W.Bare(node.Alias);
    }

    public void Visit(PgCall node)
    {
        W.Func(node.Function);
        WriteSeparated(node.Arguments);
        W.Close();
    }

    public void Visit(PgCast node)
    {
        Write(node.Operand);
        W.Attach("::");
        W.Atom(node.Type);
    }

    public void Visit(PgNullable node)
    {
        Write(node.Type);
        W.Kw(Kw.Null);
    }

    public void Visit(PgNegate node)
    {
        W.Kw(Kw.Not);
        Write(node.Operand);
    }

    public void Visit(PgAll node) => WriteConjunction(node.Terms);

    public void Visit(PgExists node)
    {
        if (node.Negated) W.Kw(Kw.Not);
        W.Kw(Kw.Exists).Open();
        Write(node.Query);
        W.Close();
    }

    public void Visit(PgScalar node)
    {
        W.Open();
        Write(node.Query);
        W.Close();
    }

    public void Visit(PgQuery node)
    {
        W.Kw(Kw.Select);
        WriteSeparated(node.Columns);
        if (node.Into is not null) W.Atom("INTO").Bare(node.Into);
        W.Kw(Kw.From);
        Write(node.From);
        if (node.Where is not null) Write(node.Where);
        if (node.GroupBy is { Count: > 0 } groups)
        {
            W.Atom("GROUP").Kw(Kw.By);
            WriteSeparated(groups);
        }
        if (node.Having is not null)
        {
            W.Atom("HAVING");
            Write(node.Having);
        }
        if (node.Limit is { } limit) W.Kw(Kw.Limit).Int(limit);
    }

    public void Visit(PgDeclare node) => W.Bare(node.Variable).Atom(node.Type).Semicolon();

    public void Visit(PgBlock node)
    {
        var body = new PostgresSerializer();
        body.WriteBlockBody(node);
        var text = body.W.ToString();
        var tag = Dollar + FreeTag(node.Tag, text) + Dollar;
        W.Atom("DO").Atom(tag).Atom(text).Atom("END").Atom(tag);
    }

    private void WriteBlockBody(PgBlock node)
    {
        if (node.Declarations.Count > 0)
        {
            W.Atom("DECLARE");
            foreach (var declaration in node.Declarations) Write(declaration);
        }
        W.Atom("BEGIN");
        WriteStatements(node.Statements);
    }

    private static string FreeTag(string tag, string body)
    {
        while (body.Contains(Dollar + tag + Dollar, StringComparison.Ordinal)) tag += TagSuffix;
        return tag;
    }

    public void Visit(PgIf node)
    {
        W.Atom("IF");
        Write(node.Condition);
        W.Atom("THEN");
        WriteStatements(node.Then);
        if (node.Otherwise.Count > 0)
        {
            W.Atom("ELSE");
            WriteStatements(node.Otherwise);
        }
        W.Atom("END").Atom("IF");
    }

    public void Visit(PgPerform node)
    {
        W.Atom("PERFORM");
        Write(node.Call);
    }

    public void Visit(PgRaise node)
    {
        W.Atom("RAISE").Atom("EXCEPTION");
        Write(node.Message);
        foreach (var argument in node.Arguments)
        {
            W.Comma();
            Write(argument);
        }
    }

    public void Visit(PgExecuteFormat node)
    {
        W.Atom("EXECUTE").Func("format");
        Write(node.Template);
        W.Comma();
        Write(node.Argument);
        W.Close();
    }

    public void Visit(PgComment node)
    {
        W.Ddl(DdlKw.Comment).Ddl(DdlKw.On).Atom(node.ObjectKind);
        Write(node.Subject);
        if (node.On is not null)
        {
            W.Ddl(DdlKw.On);
            Write(node.On);
        }
        W.Kw(Kw.Is);
        Write(node.Text);
    }

    public void Visit(PgCreateTrigger node)
    {
        W.Ddl(DdlKw.Create).Atom("OR").Atom("REPLACE").Atom("TRIGGER").Bare(node.TriggerName).Atom("AFTER").Atom(node.Event);
        W.Ddl(DdlKw.On).Ident(node.Table).Kw(Kw.For).Atom("EACH").Atom("ROW");
        if (node.OnlyWhenChanged)
        {
            W.Atom("WHEN").Open().Atom("OLD").Kw(Kw.Is).Kw(Kw.Distinct).Kw(Kw.From).Atom("NEW").Close();
        }
        W.Atom("EXECUTE").Atom("FUNCTION").Func(node.Function);
        WriteSeparated(node.Arguments);
        W.Close();
    }

    public void Visit(PgSetLocal node)
    {
        W.Ddl(DdlKw.Set).Atom("LOCAL").Bare(node.Setting).Atom("=");
        Write(node.Value);
    }

    public void Visit(PgPlainIndex node)
    {
        W.Ddl(DdlKw.Create).Ddl(DdlKw.Index).Bare(node.IndexName).Ddl(DdlKw.On).Bare(node.Table).Open();
        WriteSeparated(node.Columns);
        W.Close();
    }

    public void Visit(PgReplaceConstraint node)
    {
        WriteAlterTable(node.Table);
        W.Ddl(DdlKw.Drop).Ddl(DdlKw.Constraint).Ident(node.Dropped).Comma();
        WriteAddConstraint(node.Added);
    }

    public void Visit(PgAnyArray node)
    {
        Write(node.Operand);
        W.Atom("=").Kw(Kw.Any).Open().Kw(Kw.Array).OpenBracket();
        WriteSeparated(node.Items);
        W.CloseBracket().Close();
    }

    public void Visit(PgExecuteStatement node)
    {
        var text = new PostgresSerializer().Serialize(node.Statement).Text;
        W.Atom("EXECUTE").Str(text);
    }

    public void Visit(PgDataLiteral node) => W.Atom(node.Text);

    private void WriteStatements(IEnumerable<Node> statements)
    {
        foreach (var statement in statements)
        {
            Write(statement);
            W.Semicolon();
        }
    }
}
