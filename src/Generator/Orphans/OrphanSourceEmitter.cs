using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.Orphans;

internal static class OrphanSourceEmitter
{
    internal static string Augment(string source, ImmutableArray<Entity> entities,
        HashSet<(string Entity, string Nav)> blocked)
    {
        var supported = entities.Where(OrphanEligibility.Supported).ToImmutableArray();
        if (supported.IsEmpty) return source;
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var owner = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "OrmMetaInit");
        return root.ReplaceNode(owner, new OrphanBootstrapEmitter(owner).Emit(supported, blocked)).ToFullString();
    }
}
