using Microsoft.CodeAnalysis.CSharp;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator;

internal sealed class OneToOneLoader(Entity entity, OneToOneModel pair, bool unit)
{
    private const string Runtime = "global::Neemle.OrmLite.";
    private bool Owner => entity.Full == pair.OwnerFull;
    private string Navigation => Owner ? pair.Intent.Navigation : pair.Intent.Inverse;
    private string Target => Owner ? pair.PrincipalToken : pair.OwnerToken;
    private string TargetType => Owner ? pair.PrincipalFull : pair.OwnerFull;
    private string Property => Owner ? pair.Intent.ForeignKey : pair.PrincipalKeyProperty;

    internal string Source() => Signature() + " { " + Runtime + "OneToOneReadGuard.Require(e, "
        + Literal(Navigation) + "); ct.ThrowIfCancellationRequested(); " + Absent()
        + " var value = await " + Lookup() + "; e." + Identifier(Navigation) + " = value; }";

    private string Signature()
    {
        var parameter = unit ? Runtime + "UnitOfWork unit" : "global::System.Data.Common.DbConnection conn";
        return "public static async global::System.Threading.Tasks.Task Load" + Navigation + "Async(" + Runtime
            + "OrmContext db, " + parameter + ", " + entity.Full
            + " e, global::System.Threading.CancellationToken ct = default)";
    }

    private string Absent() => Owner && pair.Nullable
        ? "if (e." + Identifier(Property) + " is null) { e." + Identifier(Navigation) + " = null; return; }" : "";

    private string Lookup()
    {
        var scope = unit ? "db, unit.Connection, unit.Transaction" : "db, conn, null";
        var direction = Owner ? "OwnerToPrincipal" : "PrincipalToOwner";
        var column = Owner ? pair.PrincipalKeyColumn : pair.ForeignKeyColumn;
        var key = "e." + Identifier(Property) + (Owner && pair.NullableValue ? ".Value" : "!");
        return Runtime + "OneToOneRows.ReadAsync(new " + Runtime + "OneToOneReadScope(" + scope + "), new "
            + Runtime + "OneToOneLookup<" + TargetType + ", " + pair.KeyType + ">(new " + Runtime
            + "OneToOneLookupTarget(" + Target + "Orm.Descriptor, " + Literal(column) + ", " + Literal(Navigation)
            + ", " + Runtime + "OneToOneDirection." + direction + "), " + key + ", " + Target + "Orm.Map), ct)";
    }

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);
    private static string Identifier(string value) => SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None
        ? value : "@" + value;
}
