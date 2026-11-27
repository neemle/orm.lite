using System.Linq.Expressions;
using System.Reflection;

namespace Neemle.OrmLite;

public sealed class JoinSource<T> where T : class
{
    private readonly QueryJoinSnapshot _source;
    internal JoinSource(QueryJoinSnapshot source) => _source = source;

    public JoinColumn<TValue> Column<TValue>(Expression<Func<T, TValue>> property)
    {
        if (property is null) throw new JoinReadException(JoinError.UnsupportedExpression);
        _source.Current();
        var body = Unwrap(property.Body);
        if (body is not MemberExpression { Expression: ParameterExpression parameter } member)
            throw new JoinReadException(JoinError.UnsupportedExpression);
        if (!ReferenceEquals(parameter, property.Parameters[0]) || member.Member.MemberType != MemberTypes.Property)
            throw new JoinReadException(JoinError.UnsupportedExpression);
        if (!_source.Columns.TryGetValue(member.Member.Name, out var column))
            throw new JoinReadException(JoinError.UnsupportedType);
        if (column is not JoinRegisteredColumn<TValue>) throw new JoinReadException(JoinError.UnsupportedType);
        return new JoinColumn<TValue>(new JoinColumnReference(_source, column));
    }

    private static Expression Unwrap(Expression body) => body is UnaryExpression
        { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked, Method: null } convert
        && convert.Type == convert.Operand.Type ? convert.Operand : body;
}
