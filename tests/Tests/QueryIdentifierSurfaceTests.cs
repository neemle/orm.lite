using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Neemle.OrmLite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class QueryIdentifierSurfaceTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    private sealed record Covered(Type Type, string[] ParameterTypes, string[] StringParameters);

    private static readonly Covered[] Table =
    [
        new(
            typeof(Query<>),
            [
                "Action<ViewColumns<T, TView>>",
                "Boolean",
                "DbCommand",
                "DbConnection",
                "DbTransaction",
                "Expression<Func<T, Boolean>>",
                "Expression<Func<T, Object>>",
                "Expression<Func<T, TFirst>>",
                "Expression<Func<T, TKey>>",
                "Expression<Func<T, TOther, Boolean>>",
                "Expression<Func<T, TSecond>>",
                "Expression<Func<T, TThird>>",
                "Expression<Func<TOther, Object>>",
                "Func<DbDataReader, T>",
                "Func<DbDataReader, TOut>",
                "Func<Query<TOther>, Query<TOther>>",
                "IReadOnlyCollection<Guid>",
                "IReadOnlyList<Expression<Func<T, Object>>>",
                "IReadOnlyList<MovedFieldKey>",
                "IReadOnlyList<Row<T>>",
                "IReadOnlyList<UpsertSet<T>>",
                "Int32",
                "JsonKey",
                "JsonKeyOrdering",
                "Jsonb",
                "MovedFieldKey",
                "Nullable<Guid>",
                "Object",
                "OnConflict",
                "OrmContext",
                "Row<T>",
                "UnitOfWork",
            ],
            []),
        new(typeof(Row<>), ["Expression<Func<T, Object>>", "Object"], []),
        new(typeof(UpsertSet<>), ["Expression<Func<T, Object>>", "Int64"], []),
        new(typeof(JsonKey), ["JsonKey", "Object", "String"], [".ctor key"]),
        new(
            typeof(JsonKeyOrdering),
            ["Boolean", "IReadOnlyList<String>", "JsonKey", "JsonValueOrder"],
            [".ctor arrangement"]),
        new(typeof(MovedFieldKey), ["MovedFieldKey", "Object", "String"], [".ctor name"]),
    ];

    public static IEnumerable<object[]> CoveredTypes => Table.Select(c => new object[] { c.Type });

    private static Covered Of(Type type) => Table.Single(c => c.Type == type);

    private static IEnumerable<(string Member, ParameterInfo Parameter)> Parameters(Type type) =>
        type.GetConstructors(Declared).Cast<MethodBase>().Concat(type.GetMethods(Declared))
            .SelectMany(m => m.GetParameters().Select(p => (m.Name, p)));

    private static string Display(Type type) => type switch
    {
        { IsGenericParameter: true } => type.Name,
        { IsByRef: true } => Display(type.GetElementType()!) + "&",
        { IsArray: true } => Display(type.GetElementType()!) + "[]",
        { IsGenericType: true } => type.Name[..type.Name.IndexOf('`')]
            + "<" + string.Join(", ", type.GetGenericArguments().Select(Display)) + ">",
        _ => type.Name,
    };

    private static bool MentionsString(Type type) =>
        type == typeof(string)
        || (type.IsGenericType && type.GetGenericArguments().Any(MentionsString))
        || (type.HasElementType && MentionsString(type.GetElementType()!));

    private static string[] Sorted(IEnumerable<string> names) =>
        names.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact(DisplayName = "Query<T> is sealed, derives from Object, implements no interface "
        + "and has no public field or nested type")]
    public void The_query_type_is_a_closed_shape()
    {
        var type = typeof(Query<>);

        Assert.True(type.IsSealed);
        Assert.Equal(typeof(object), type.BaseType);
        Assert.Empty(type.GetInterfaces());
        Assert.Empty(type.GetFields(Declared));
        Assert.Empty(type.GetNestedTypes(BindingFlags.Public));
    }

    [Theory(DisplayName = "the parameter types over every public constructor and method "
        + "are exactly the frozen allowlist")]
    [MemberData(nameof(CoveredTypes))]
    public void The_parameter_types_are_the_frozen_allowlist(Type type) =>
        Assert.Equal(
            Sorted(Of(type).ParameterTypes),
            Sorted(Parameters(type).Select(p => Display(p.Parameter.ParameterType))));

    [Theory(DisplayName = "the string-typed parameters over every public constructor and method "
        + "are exactly the frozen roster")]
    [MemberData(nameof(CoveredTypes))]
    public void The_string_parameters_are_the_frozen_roster(Type type) =>
        Assert.Equal(
            Sorted(Of(type).StringParameters),
            Sorted(Parameters(type)
                .Where(p => MentionsString(p.Parameter.ParameterType))
                .Select(p => p.Member + " " + p.Parameter.Name)));

    [Theory(DisplayName = "Row<T> and UpsertSet<T> take, return and expose no string "
        + "and no type constructed over string")]
    [InlineData(typeof(Row<>))]
    [InlineData(typeof(UpsertSet<>))]
    public void The_typed_builders_carry_no_string(Type type)
    {
        var methods = type.GetMethods(Declared);
        Assert.NotEmpty(methods);

        var carried = methods.SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Concat(type.GetConstructors().SelectMany(c => c.GetParameters().Select(p => p.ParameterType)))
            .Concat(type.GetProperties(Declared).Select(p => p.PropertyType))
            .Concat(type.GetFields(Declared).Select(f => f.FieldType));

        Assert.All(carried, t => Assert.False(MentionsString(t), type.Name + " exposes " + t));
    }
}
