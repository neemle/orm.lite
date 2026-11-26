namespace Neemle.OrmLite.Tests.SchemaDiff;

public enum SqliteKeyForm
{
    NullableText,
    ExplicitNotNullText,
    IntegerAlias,
    InlineIntegerDescending,
    TableIntegerDescending,
    StrictText,
    WithoutRowidText,
}
