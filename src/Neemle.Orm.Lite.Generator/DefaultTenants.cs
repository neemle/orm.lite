using System.Collections.Immutable;
using System.Linq;
using Entity = Neemle.Orm.Lite.OrmGenerator.Entity;

namespace Neemle.Orm.Lite;

/// <summary>
/// Gives each [SoftDelete] entity that did not name its tenant the default tenant of the project: the value of the
/// OrmLiteDefaultTenant build property when it is a C# identifier, and TenantId otherwise.
/// </summary>
internal static class DefaultTenants
{
    internal static ImmutableArray<Entity> Resolve(ImmutableArray<Entity> entities, string option)
    {
        var name = option.Length > 0 && IsIdentifier(option) ? option : OrmGenerator.DefaultTenant;
        return entities.Select(e => e.Soft is not { } soft ? e : e with
        {
            Soft = soft with
            {
                DefaultName = name,
                TenantName = soft.TenantDeclared ? soft.TenantName : name,
                TenantProp = soft.TenantDeclared ? soft.TenantProp : e.Props.FirstOrDefault(p => p.Name == name),
            },
        }).ToImmutableArray();
    }

    internal static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        foreach (var ch in value)
            if (!(char.IsLetterOrDigit(ch) || ch == '_')) return false;
        var kind = Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(value);
        return kind == Microsoft.CodeAnalysis.CSharp.SyntaxKind.None;
    }
}
