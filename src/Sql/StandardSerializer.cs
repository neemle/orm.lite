using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sql;

public abstract partial class StandardSerializer : INodeVisitor, ISerializer
{
    private readonly List<StatementParameter> _parameters = new();

    private bool _inlineParameters;

    protected SqlWriter W { get; } = new();

    public SerializedStatement Serialize(Node root)
    {
        root.Accept(this);
        return new SerializedStatement(W.ToString(), _parameters.ToList());
    }

    public string Fragment(Node node)
    {
        node.Accept(this);
        return W.ToString();
    }

    protected virtual string ParameterPrefix => "@";

    protected virtual string ParameterName(int ordinal) => "p" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected virtual string FunctionName(Fn function) => function switch
    {
        Fn.Count => "count",
        Fn.Sum => "sum",
        Fn.Min => "min",
        Fn.Lower => "lower",
        Fn.Coalesce => "coalesce",
        Fn.Max => "max",
        _ => throw new NotSupportedException($"no spelling for {function}."),
    };

    protected virtual string OperatorOf(CompareOp op) => op switch
    {
        CompareOp.Equal => "=",
        CompareOp.NotEqual => "<>",
        CompareOp.Less => "<",
        CompareOp.LessOrEqual => "<=",
        CompareOp.Greater => ">",
        CompareOp.GreaterOrEqual => ">=",
        _ => throw new NotSupportedException($"no spelling for {op}."),
    };

    protected void Write(Node node) => node.Accept(this);

    protected void WriteSeparated(IEnumerable<Node> nodes)
    {
        var first = true;
        foreach (var node in nodes)
        {
            if (!first) W.Comma();
            first = false;
            Write(node);
        }
    }

    protected void WriteConjunction(IEnumerable<Node> nodes)
    {
        var first = true;
        foreach (var node in nodes)
        {
            if (!first) W.Kw(Kw.And);
            first = false;
            Write(node);
        }
    }

    protected virtual void WriteCastType(CastType type)
    {
        switch (type)
        {
            case CastType.Text:
                W.Atom("text");
                break;
            case CastType.Boolean:
                W.Atom("boolean");
                break;
            case CastType.Double:
                W.Atom("double").Atom("precision");
                break;
            case CastType.Decimal:
                W.Atom("numeric");
                break;
            case CastType.Uuid:
                W.Atom("uuid");
                break;
            default:
                throw new NotSupportedException($"{GetType().Name} has no spelling for a cast to {type}.");
        }
    }

    public virtual void Visit(Star node)
    {
        if (node.Qualifier is null)
        {
            W.Atom("*");
            return;
        }
        W.Bare(node.Qualifier).Dot().Atom("*");
    }

    public virtual void Visit(NullLit node) => W.Kw(Kw.Null);

    public virtual void Visit(ClockNow node) => W.Kw(Kw.CurrentTimestamp);

    public virtual void Visit(BoolLit node) => W.Atom(node.Value ? "true" : "false");

    public virtual void Visit(IntLit node) => W.Int(node.Value);

    public virtual void Visit(StringLit node) => W.Str(node.Value);

    public virtual void Visit(AliasRef node) => W.Bare(node.Alias);

    public virtual void Visit(TableRef node)
    {
        if (node.Bare) W.Bare(node.Table);
        else W.Ident(node.Table);
        if (node.Alias is not null) W.Bare(node.Alias);
    }

    public virtual void Visit(Param node)
    {
        if (_inlineParameters)
        {
            Write(ParamLiterals.Of(node.Value));
            return;
        }
        var name = node.Alias ?? ParameterName(node.Ordinal);
        W.Param(ParameterPrefix, name);
        _parameters.Add(new StatementParameter(node.Ordinal, name, node.Value));
    }

    public virtual void Visit(RowLock node)
    {
        if (node.Strength == RowLockStrength.Share)
        {
            W.Kw(Kw.For).Atom("SHARE");
            return;
        }
        if (node.Strength != RowLockStrength.Update)
            throw new NotSupportedException($"{GetType().Name} has no spelling for a {node.Strength} row lock.");
        W.Kw(Kw.For).Kw(Kw.Update);
    }

    public virtual void Visit(ColumnRef node)
    {
        if (node.Owner is not null)
        {
            Write(node.Owner);
            W.Dot();
        }
        if (node.Bare) W.Bare(node.Column);
        else W.Ident(node.Column);
    }

    public virtual void Visit(And node)
    {
        W.Open();
        Write(node.Left);
        W.Kw(Kw.And);
        Write(node.Right);
        W.Close();
    }

    public virtual void Visit(Or node)
    {
        W.Open();
        Write(node.Left);
        W.Kw(Kw.Or);
        Write(node.Right);
        W.Close();
    }

    public virtual void Visit(Not node)
    {
        W.Kw(Kw.Not).Open();
        Write(node.Operand);
        W.Close();
    }

    public virtual void Visit(Group node)
    {
        W.Open();
        Write(node.Inner);
        W.Close();
    }

    public virtual void Visit(Compare node)
    {
        Write(node.Left);
        W.Atom(OperatorOf(node.Op));
        Write(node.Right);
    }

    public virtual void Visit(IsNull node)
    {
        Write(node.Operand);
        W.Kw(Kw.Is).Kw(Kw.Null);
    }

    public virtual void Visit(IsNotNull node)
    {
        Write(node.Operand);
        W.Kw(Kw.Is).Kw(Kw.Not).Kw(Kw.Null);
    }

    public virtual void Visit(InList node)
    {
        Write(node.Operand);
        W.Kw(Kw.In).Open();
        WriteSeparated(node.Items);
        W.Close();
    }

    public virtual void Visit(InSubquery node)
    {
        Write(node.Operand);
        W.Kw(Kw.In).Open();
        Write(node.Query);
        W.Close();
    }

    public virtual void Visit(Like node)
    {
        Write(node.Operand);
        W.Kw(Kw.Like);
        Write(node.Pattern);
        W.Kw(Kw.Escape).Str(node.Escape.ToString());
    }

    public abstract void Visit(ILike node);

    public virtual void Visit(Exists node)
    {
        if (node.Negated) W.Kw(Kw.Not);
        W.Kw(Kw.Exists).Open();
        Write(node.Query);
        W.Close();
    }

    public virtual void Visit(ScalarSubquery node)
    {
        W.Open();
        Write(node.Query);
        W.Close();
    }

    public virtual void Visit(Call node)
    {
        W.Func(FunctionName(node.Function));
        WriteSeparated(node.Arguments);
        W.Close();
    }

    public virtual void Visit(Aggregate node)
    {
        switch (node.Function)
        {
            case AggregateFn.CountDistinct:
                W.Func(FunctionName(Fn.Count)).Kw(Kw.Distinct);
                Write(node.Operand);
                W.Close();
                break;
            case AggregateFn.SumOrZero:
            case AggregateFn.DecimalSumOrZero:
                W.KwCall(Kw.Coalesce).KwCall(Kw.Sum).KwCall(Kw.Coalesce);
                Write(node.Operand);
                W.Comma().Int(0).Close().Close().Comma().Int(0).Close();
                break;
            case AggregateFn.Min:
            case AggregateFn.DecimalMin:
                W.Func(FunctionName(Fn.Min));
                Write(node.Operand);
                W.Close();
                break;
            case AggregateFn.Max:
            case AggregateFn.DecimalMax:
                W.Func(FunctionName(Fn.Max));
                Write(node.Operand);
                W.Close();
                break;
            case AggregateFn.DecimalAvg:
                W.Func("round").Func("avg");
                Write(node.Operand);
                W.Close().Comma().Int(node.Scale).Close();
                break;
            case AggregateFn.Avg:
                W.Func("avg");
                Write(node.Operand);
                W.Close();
                break;
            default:
                throw new NotSupportedException($"no spelling for {node.Function}.");
        }
    }

    public virtual void Visit(CountFiltered node)
    {
        W.Func(FunctionName(Fn.Count)).Atom("*").Close();
        W.Kw(Kw.Filter).Open().Kw(Kw.Where);
        Write(node.Filter);
        W.Close();
    }

    public virtual void Visit(Cast node)
    {
        W.KwCall(Kw.Cast);
        Write(node.Operand);
        W.Kw(Kw.As);
        WriteCastType(node.Type);
        W.Close();
    }

    public virtual void Visit(CaseWhen node)
    {
        W.Kw(Kw.Case).Kw(Kw.When);
        Write(node.Condition);
        W.Kw(Kw.Then);
        Write(node.Result);
        if (node.Otherwise is not null)
        {
            W.Kw(Kw.Else);
            Write(node.Otherwise);
        }
        W.Kw(Kw.End);
    }

    public virtual void Visit(Aliased node)
    {
        Write(node.Operand);
        W.Kw(Kw.As).Ident(node.Alias);
    }

    public abstract void Visit(AnyOf node);

    public virtual void Visit(NullSafeEquals node)
    {
        Write(node.Left);
        W.Kw(Kw.Is).Kw(Kw.Not).Kw(Kw.Distinct).Kw(Kw.From);
        Write(node.Right);
    }

    public abstract void Visit(Comparable node);

    public abstract void Visit(JsonKeyText node);

    public abstract void Visit(JsonKeyExists node);

    public abstract void Visit(JsonArrayContainsString node);

    public abstract void Visit(JsonArrayContainsAny node);

    public abstract void Visit(OptionRank node);

    public abstract void Visit(MovedNumberOrEpochMillis node);

    public virtual void Visit(OrderTerm node)
    {
        Write(node.Operand);
        if (node.Collation is not null) W.Ddl(DdlKw.Collate).Ident(node.Collation);
        W.Kw(node.Descending ? Kw.Desc : Kw.Asc);
        if (node.Nulls != NullsPlacement.Unspecified)
            W.Kw(Kw.Nulls).Kw(node.Nulls == NullsPlacement.First ? Kw.First : Kw.Last);
    }

    public virtual void Visit(OrderBy node)
    {
        W.Kw(Kw.Order).Kw(Kw.By);
        WriteSeparated(node.Terms);
    }

    public virtual void Visit(Where node)
    {
        W.Kw(Kw.Where);
        WriteConjunction(node.Terms);
    }

    public virtual void Visit(Limit node) => W.Kw(Kw.Limit).Int(node.Count);

    public virtual void Visit(Offset node) => W.Kw(Kw.Offset).Int(node.Count);

    public virtual void Visit(Select node)
    {
        W.Kw(Kw.Select);
        WriteSeparated(node.Columns);
        if (node.From is not null)
        {
            W.Kw(Kw.From);
            Write(node.From);
        }
        if (node.Joins is not null)
            foreach (var join in node.Joins) Write(join);
        if (node.Where is not null) Write(node.Where);
        if (node.GroupBy is { Count: > 0 })
        {
            W.Kw(Kw.Group).Kw(Kw.By);
            WriteSeparated(node.GroupBy);
        }
        if (node.Having is not null)
        {
            W.Kw(Kw.Having);
            Write(node.Having);
        }
        if (node.OrderBy is not null) Write(node.OrderBy);
        WritePaging(node.Limit, node.Offset);
        if (node.Lock is not null) Write(node.Lock);
    }

    protected virtual void WritePaging(Limit? limit, Offset? offset)
    {
        if (limit is not null) Write(limit);
        if (offset is not null) Write(offset);
    }

    public virtual void Visit(InnerJoin node)
    {
        W.Kw(Kw.Join);
        Write(node.Table);
        W.Kw(Kw.On);
        Write(node.On);
    }

    public virtual void Visit(Assignment node)
    {
        Write(node.Target);
        W.Atom("=");
        Write(node.Value);
    }

    public virtual void Visit(ValuesRow node)
    {
        W.Open();
        WriteSeparated(node.Values);
        W.Close();
    }

    public virtual void Visit(Returning node)
    {
        W.Kw(Kw.Returning);
        WriteSeparated(node.Columns);
    }

    public virtual void Visit(ConflictUpdate node)
    {
        W.Kw(Kw.Do).Kw(Kw.Update).Kw(Kw.Set);
        WriteSeparated(node.Assignments);
        if (node.Where is null) return;
        W.Kw(Kw.Where);
        Write(node.Where);
    }

    public virtual void Visit(Conflict node)
    {
        W.Kw(Kw.On).Kw(Kw.Conflict);
        if (node.Target.Count > 0)
        {
            W.Open();
            WriteSeparated(node.Target);
            W.Close();
        }
        if (node.TargetWhere is not null)
        {
            W.Kw(Kw.Where);
            Write(node.TargetWhere);
        }
        if (node.Update is null) W.Kw(Kw.Do).Kw(Kw.Nothing);
        else Write(node.Update);
    }

    public virtual void Visit(Insert node)
    {
        W.Kw(Kw.Insert).Kw(Kw.Into);
        Write(node.Into);
        W.Open();
        WriteSeparated(node.Columns);
        W.Close();
        W.Kw(Kw.Values);
        WriteSeparated(node.Rows);
        if (node.Conflict is not null) Write(node.Conflict);
        if (node.Returning is not null) Write(node.Returning);
    }

    public virtual void Visit(Update node)
    {
        W.Kw(Kw.Update);
        Write(node.Table);
        W.Kw(Kw.Set);
        WriteSeparated(node.Assignments);
        if (node.Where is not null) Write(node.Where);
        if (node.Returning is not null) Write(node.Returning);
    }

    public virtual void Visit(Delete node)
    {
        W.Kw(Kw.Delete).Kw(Kw.From);
        Write(node.From);
        if (node.Where is not null) Write(node.Where);
        if (node.Returning is not null) Write(node.Returning);
    }

    public virtual void Visit(Plus node)
    {
        Write(node.Left);
        W.Atom("+");
        Write(node.Right);
    }

    public virtual void Visit(Coalesce node)
    {
        W.KwCall(Kw.Coalesce);
        Write(node.Value);
        W.Comma();
        Write(node.Fallback);
        W.Close();
    }

    public abstract void Visit(ClockPlusMinutes node);

    public abstract void Visit(NamedLock node);

    public abstract void Visit(JsonDocumentOrEmpty node);

    public abstract void Visit(JsonConcat node);

    public abstract void Visit(JsonRemoveKey node);

    public abstract void Visit(JsonObjectOf node);

    public abstract void Visit(JsonIntOrZero node);
}
