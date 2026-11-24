using System.Globalization;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteSerializer
{
    public override void Visit(Cast node)
    {
        if (node.Type == CastType.Json)
        {
            Call(SqliteJsonFunctions.Normalize, node.Operand);
            return;
        }
        if (node.Type == CastType.Text && node.Operand is Param { Value: bool or decimal } parameter)
        {
            base.Visit(node with { Operand = parameter with { Value = TextOf(parameter.Value) } });
            return;
        }
        base.Visit(node);
    }

    public override void Visit(Comparable node)
    {
        switch (node.Order)
        {
            case JsonValueOrder.Text:
                Write(node.Operand);
                break;
            case JsonValueOrder.Number:
                Call(SqliteJsonFunctions.Number, node.Operand);
                break;
            case JsonValueOrder.Boolean:
                Call(SqliteJsonFunctions.Boolean, node.Operand);
                break;
            default:
                throw new NotSupportedException(
                    $"{nameof(SqliteSerializer)} has no rule for JsonValueOrder.{node.Order}; Text is compared as the extracted text, so an arrival here is a new member of the enum that this serializer was not given an arm for.");
        }
    }

    public override void Visit(JsonKeyText node) => Call(SqliteJsonFunctions.KeyText, node.Document, node.Key);

    public override void Visit(JsonKeyExists node) => Call(SqliteJsonFunctions.HasKey, node.Document, node.Key);

    public override void Visit(JsonArrayContainsString node) =>
        Call(SqliteJsonFunctions.ArrayHas, node.Document, node.Key, node.Value);

    public override void Visit(JsonArrayContainsAny node)
    {
        W.Open();
        var first = true;
        foreach (var value in node.Values)
        {
            if (!first) W.Kw(Kw.Or);
            first = false;
            Call(SqliteJsonFunctions.ArrayHas, node.Document, node.Key, value);
        }
        W.Close();
    }

    public override void Visit(OptionRank node)
    {
        if (node.Options.Count == 0)
            throw new ArgumentException(
                "an arrangement with no options ranks every row NULL, which orders nothing; "
                + "Query<T> compares the extracted value instead of building this node.",
                nameof(node));
        W.Kw(Kw.Case);
        Write(node.Needle);
        var rank = 0;
        foreach (var option in node.Options)
        {
            W.Kw(Kw.When);
            Write(option);
            W.Kw(Kw.Then).Int(++rank);
        }
        W.Kw(Kw.End);
    }

    public override void Visit(MovedNumberOrEpochMillis node)
    {
        W.KwCall(Kw.Coalesce);
        Call(SqliteJsonFunctions.Double, new JsonKeyText(node.Document, node.Key));
        W.Comma();
        Call(SqliteJsonFunctions.EpochMillis, node.Timestamp);
        W.Close();
    }

    public override void Visit(JsonDocumentOrEmpty node)
    {
        W.KwCall(Kw.Coalesce);
        Write(node.Document);
        W.Comma().Str(SqliteJsonDataLiterals.EmptyDocument).Close();
    }

    public override void Visit(JsonConcat node) => Call(SqliteJsonFunctions.Concat, node.Left, node.Right);

    public override void Visit(JsonRemoveKey node) => Call(SqliteJsonFunctions.Remove, node.Document, node.Key);

    public override void Visit(JsonObjectOf node) => Call(SqliteJsonFunctions.ObjectOf, node.Key, node.Value);

    public override void Visit(JsonIntOrZero node) => Call(SqliteJsonFunctions.IntOrZero, node.Text);

    private void Call(string function, params Node[] arguments)
    {
        W.Func(function);
        WriteSeparated(arguments);
        W.Close();
    }

    private static string TextOf(object? value) => value switch
    {
        bool flag => flag ? "true" : "false",
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException($"{nameof(SqliteSerializer)} has no text spelling for a {value?.GetType().Name} parameter."),
    };
}
