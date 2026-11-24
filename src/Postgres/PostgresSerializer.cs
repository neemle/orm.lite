using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresSerializer : StandardSerializer
{
    public override void Visit(ClockNow node) => W.Func("now").Close();

    public override void Visit(RowLock node)
    {
        if (node.Strength == RowLockStrength.KeyPreserving)
        {
            W.Kw(Kw.For).Kw(Kw.No).Kw(Kw.Key).Kw(Kw.Update);
            return;
        }
        base.Visit(node);
    }

    public override void Visit(Cast node)
    {
        if (node.Type == CastType.Text)
        {
            base.Visit(node);
            return;
        }
        Write(node.Operand);
        W.Attach("::");
        WriteCastType(node.Type);
    }

    protected override void WriteCastType(CastType type)
    {
        if (type == CastType.Json) W.Atom("jsonb");
        else base.WriteCastType(type);
    }

    public override void Visit(ILike node)
    {
        Write(node.Operand);
        W.Atom("ILIKE");
        Write(node.Pattern);
        W.Kw(Kw.Escape).Str(node.Escape.ToString());
    }

    public override void Visit(AnyOf node)
    {
        Write(node.Operand);
        W.Atom("=").KwCall(Kw.Any);
        Write(node.Values);
        W.Close();
    }

    public override void Visit(Comparable node)
    {
        if (node.Order == JsonValueOrder.Text)
        {
            Write(node.Operand);
            return;
        }
        var type = TypeOf(node.Order);
        W.Open().Kw(Kw.Case).Kw(Kw.When).Func("pg_input_is_valid");
        Write(node.Operand);
        W.Comma().Str(type).Close().Kw(Kw.Then);
        Write(node.Operand);
        W.Kw(Kw.End).Close().Attach("::").Atom(type);
    }

    public override void Visit(JsonKeyText node)
    {
        W.Open();
        Write(node.Document);
        W.Atom("->>");
        Write(node.Key);
        W.Close();
    }

    public override void Visit(JsonKeyExists node)
    {
        W.Func("jsonb_exists");
        Write(node.Document);
        W.Comma();
        Write(node.Key);
        W.Close();
    }

    public override void Visit(JsonArrayContainsString node)
    {
        W.Open().Func("jsonb_typeof");
        WriteExtracted(node.Document, node.Key);
        W.Close().Atom("=").Str("array").Kw(Kw.And).Func("jsonb_exists");
        WriteExtracted(node.Document, node.Key);
        W.Comma();
        Write(node.Value);
        W.Close().Close();
    }

    public override void Visit(JsonArrayContainsAny node)
    {
        W.Open().Func("jsonb_typeof");
        WriteExtracted(node.Document, node.Key);
        W.Close().Atom("=").Str("array").Kw(Kw.And).Open();
        var first = true;
        foreach (var value in node.Values)
        {
            if (!first) W.Kw(Kw.Or);
            first = false;
            WriteExtracted(node.Document, node.Key);
            W.Atom("@>").Func("to_jsonb");
            Write(new Cast(value, CastType.Text));
            W.Close();
        }
        W.Close().Close();
    }

    public override void Visit(OptionRank node)
    {
        if (node.Options.Count == 0)
            throw new ArgumentException(
                "an arrangement with no options ranks every row NULL, which orders nothing; "
                + "Query<T> compares the extracted value instead of building this node.",
                nameof(node));
        W.Func("array_position").Kw(Kw.Array).OpenBracket();
        WriteSeparated(node.Options);
        W.CloseBracket().Attach("::").Atom("text").OpenBracket().CloseBracket().Comma();
        Write(node.Needle);
        W.Close();
    }

    public override void Visit(MovedNumberOrEpochMillis node)
    {
        var text = new JsonKeyText(node.Document, node.Key);
        W.KwCall(Kw.Coalesce).Open().Kw(Kw.Case).Kw(Kw.When).Func("pg_input_is_valid");
        Write(text);
        W.Comma().Str(PostgresDataLiterals.DoublePrecision).Close().Kw(Kw.Then);
        Write(text);
        W.Kw(Kw.End).Close().Attach("::").Atom("double").Atom("precision").Comma();
        W.KwCall(Kw.Floor).KwCall(Kw.Extract).Atom("EPOCH").Kw(Kw.From);
        Write(node.Timestamp);
        W.Close().Atom("*").Int(1000).Close().Attach("::").Atom("double").Atom("precision").Close();
    }

    public override void Visit(ClockPlusMinutes node)
    {
        Write(new ClockNow());
        W.Atom("+").Func("make_interval").Atom("mins").Atom("=>");
        Write(node.Minutes);
        W.Close();
    }

    public override void Visit(NamedLock node)
    {
        W.Func(node.Kind == NamedLockKind.Shared ? "pg_advisory_xact_lock_shared" : "pg_advisory_xact_lock")
            .Func("hashtextextended");
        Write(node.Key);
        W.Comma().Int(0).Close().Close();
    }

    public override void Visit(JsonDocumentOrEmpty node)
    {
        W.KwCall(Kw.Coalesce);
        Write(node.Document);
        W.Comma().Str("{}").Attach("::").Atom("jsonb").Close();
    }

    public override void Visit(JsonConcat node)
    {
        Write(node.Left);
        W.Atom("||");
        Write(node.Right);
    }

    public override void Visit(JsonRemoveKey node)
    {
        W.Open();
        Write(node.Document);
        W.Atom("-");
        Write(node.Key);
        W.Attach("::").Atom("text");
        W.Close();
    }

    public override void Visit(JsonObjectOf node)
    {
        W.Func("jsonb_build_object");
        Write(node.Key);
        W.Attach("::").Atom("text");
        W.Comma();
        Write(node.Value);
        W.Close();
    }

    public override void Visit(JsonIntOrZero node)
    {
        W.KwCall(Kw.Coalesce).KwCall(Kw.Nullif);
        Write(node.Text);
        W.Comma().Str("").Close().Attach("::").Atom("int").Comma().Int(0).Close();
    }

    private void WriteExtracted(Node document, Node key)
    {
        W.Open();
        Write(document);
        W.Atom("->");
        Write(key);
        W.Close();
    }

    private string TypeOf(JsonValueOrder order) => order switch
    {
        JsonValueOrder.Number => "numeric",
        JsonValueOrder.Boolean => "boolean",
        _ => throw new NotSupportedException(
            $"{nameof(PostgresSerializer)} renders no SQL type for JsonValueOrder.{order}; Text is compared as the "
            + "extracted text and never reaches this switch, so an arrival here is a new member of "
            + "the enum that this serializer was not given an arm for."),
    };
}
