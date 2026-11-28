using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Model;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;

namespace Neemle.OrmLite.Generator.Joins;

internal static class JoinCodecEmitter
{
    private const string Runtime = "global::Neemle.OrmLite.";

    internal static bool Supported(Entity entity) => GraphEmitter.Supported(entity)
        && Scalar(OrmGenerator.FirstKey(entity)!);

    private static bool Scalar(Prop property) => property.Read is
        "Guid" or "String" or "Boolean" or "Int32" or "Int64" or "DateTime";

    internal static IEnumerable<string> Statements(Entity entity)
    {
        var key = OrmGenerator.FirstKey(entity)!;
        var properties = entity.Props.Where(Scalar).Concat(entity.Calculated.Select(c => c.Property));
        foreach (var property in properties)
        {
            var type = property.Cs + (property.Nullable ? "?" : "");
            var read = MapEmitter.ReadOf(property, Syn.Sym("o"), entity.Table, key.Column);
            CsNode value = property.Nullable
                ? Syn.SymChoose(Syn.SymCall(Syn.Sym("r"), "IsDBNull", Syn.Sym("o")), Syn.SymNull, read) : read;
            var body = Cs.Tree(Syn.SymReturn(value)).Render();
            var binding = Runtime + "JoinColumnBinding<" + entity.Full + ", " + type + ">";
            yield return Runtime + "JoinModel.RegisterColumn<" + entity.Full + ", " + type + ">(" + entity.Type
                + "Orm.Descriptor, new " + binding + "(" + SymbolDisplay.FormatLiteral(property.Name, true)
                + ", static (r, o) => { " + body + " }));";
        }
    }
}
