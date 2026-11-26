using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteSerializer(int maxBindParameters = SqliteProvider.BindParameterLimit)
    : StandardSerializer, ISchemaDiffDdlSerializer
{
    protected override void WritePaging(Limit? limit, Offset? offset)
    {
        if (limit is not null) Write(limit);
        else if (offset is not null) W.Kw(Kw.Limit).Int(-1);
        if (offset is not null) Write(offset);
    }

    protected override void WriteCastType(CastType type)
    {
        switch (type)
        {
            case CastType.Text:
            case CastType.Uuid:
                W.Atom("text");
                break;
            case CastType.Boolean:
                W.Atom("integer");
                break;
            case CastType.Double:
                W.Atom("real");
                break;
            case CastType.Decimal:
                throw SqliteDecimal.CastRefusal();
            default:
                throw new NotSupportedException($"{nameof(SqliteSerializer)} has no spelling for a cast to {type}.");
        }
    }

    public override void Visit(ClockNow node)
    {
        W.Open().Func("strftime").Str(SqliteDataLiterals.ClockFormat).Comma().Str(SqliteDataLiterals.Now).Close();
        W.Atom("||").Str(SqliteDataLiterals.FractionTail).Close();
    }

    public override void Visit(ClockPlusMinutes node)
    {
        W.Open().Func("strftime").Str(SqliteDataLiterals.ClockFormat).Comma()
            .Func(SqliteDataLiterals.UnixEpoch).Str(SqliteDataLiterals.SubSecond).Close()
            .Atom("+");
        Write(node.Minutes);
        W.Atom("*").Int(60).Comma().Str(SqliteDataLiterals.UnixEpoch).Close();
        W.Atom("||").Str(SqliteDataLiterals.FractionTail).Close();
    }

    public override void Visit(RowLock node)
    {
    }

    public override void Visit(NamedLock node) => W.Int(1);

    public override void Visit(ILike node)
    {
        W.Func(SqliteFunctions.ILike);
        Write(node.Operand);
        W.Comma();
        Write(node.Pattern);
        W.Comma().Str(node.Escape.ToString()).Close();
    }

    public override void Visit(Aggregate node)
    {
        switch (node.Function)
        {
            case AggregateFn.DecimalSumOrZero:
                W.KwCall(Kw.Coalesce).Func(SqliteDecimal.SumFunction);
                Write(node.Operand);
                W.Close().Comma().Str(SqliteDecimal.ZeroText).Close();
                break;
            case AggregateFn.DecimalAvg:
                W.Func(SqliteDecimal.AverageFunction);
                Write(node.Operand);
                W.Comma().Int(node.Scale).Close();
                break;
            case AggregateFn.DecimalMin:
            case AggregateFn.DecimalMax:
                base.Visit(node);
                break;
            default:
                base.Visit(node);
                return;
        }
        W.Atom(SqliteDecimal.CollateKeyword).Atom(SqliteDecimal.Collation);
    }

    public override void Visit(AnyOf node)
    {
        if (node.Values is not Param { Value: System.Collections.IEnumerable values and not string } parameter)
            throw new NotSupportedException($"{nameof(SqliteSerializer)} binds an array parameter only as a collection value, and {node.Values.Name} is not one.");
        var name = parameter.Alias ?? ParameterName(parameter.Ordinal);
        var items = new List<Node>();
        foreach (var value in values)
        {
            if (items.Count >= maxBindParameters)
                throw new NotSupportedException(
                    $"an IN list of more than {maxBindParameters} values exceeds what SQLite binds in one statement; split the list into smaller batches.");
            items.Add(new Param(parameter.Ordinal, value, name + "_" + items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        Visit(new InList(node.Operand, NodeList.Of(items)));
    }
}
