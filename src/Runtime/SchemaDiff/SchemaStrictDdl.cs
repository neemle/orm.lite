using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaStrictDdl
{
    internal static IReadOnlyList<Statement> Compile(CreateTable operation, ISqlProvider provider)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(provider);
        _ = DdlCompiler.Compile(operation, provider);
        if (provider.Lower(operation) is not CreateTable lowered
            || provider.CreateSerializer() is not ISchemaDiffDdlSerializer serializer)
            throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        var written = serializer.SerializeStrictCreate(lowered);
        return Array.AsReadOnly(new[] { new Statement(written.Text, written.Parameters.ToArray(), StatementKind.Ddl) });
    }
}
