using Microsoft.CodeAnalysis;

namespace Neemle.Orm.Lite;

/// <summary>The column shape (how it is read, its column type and its C# type) of a property type.</summary>
internal static class ColumnTypes
{
    /// <summary>The shape of the type, or null when the type has no column mapping.</summary>
    internal static (string read, string type, string cs)? Classify(ITypeSymbol t)
    {
        var u = t;
        if (t is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            u = nt.TypeArguments[0];
        if (u.TypeKind == TypeKind.Enum) return ("Enum", "Text", "global::" + u.ToDisplayString());
        var display = u.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString();
        return ByDisplayName(display) ?? BySpecialType(u.SpecialType);
    }

    private static (string read, string type, string cs)? ByDisplayName(string display) => display switch
    {
        "System.Guid" => ("Guid", "Uuid", "global::System.Guid"),
        "System.DateOnly" => ("Date", "Date", "global::System.DateOnly"),
        "System.DateTime" => ("DateTime", "Timestamp", "global::System.DateTime"),
        OrmGenerator.JsonbClrTypeName => ("Jsonb", "Json", "global::" + OrmGenerator.JsonbClrTypeName),
        _ => ((string, string, string)?)null,
    };

    private static (string read, string type, string cs)? BySpecialType(SpecialType type) => type switch
    {
        SpecialType.System_String => ("String", "Text", "string"),
        SpecialType.System_Int32 => ("Int32", "Int32", "int"),
        SpecialType.System_Int64 => ("Int64", "Int64", "long"),
        SpecialType.System_Boolean => ("Boolean", "Bool", "bool"),
        SpecialType.System_Double => ("Double", "Double", "double"),
        SpecialType.System_Decimal => ("Decimal", "Decimal", "decimal"),
        _ => ((string, string, string)?)null,
    };
}
