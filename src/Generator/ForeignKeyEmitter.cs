using System.Linq;
using Neemle.CSharpComposer;
using Neemle.CSharpComposer.Model;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.Syn;

namespace Neemle.OrmLite.Generator;

internal static class ForeignKeyEmitter
{
    internal static CsExpression Describe(CsExpression descriptor, Entity entity)
    {
        foreach (var key in entity.ForeignKeys)
        {
            var target = New("ForeignKeyTarget", Str(key.TargetTable), Str(key.TargetColumn));
            descriptor = descriptor.Dot("ForeignKey").Of(New("ForeignKeyDescriptor",
                Str(key.Name), Str(key.Column), target, Action(key.Action)));
        }
        foreach (var intent in entity.ForeignKeyIntents.Where(i => i.End is not null))
            descriptor = descriptor.Dot("LinkForeignKey").Of(Str(intent.Navigation),
                Static("LinkForeignKeyEnd", intent.End == 0 ? "Source" : "Target"),
                New("LinkForeignKeyDeclaration", Str(intent.Name), Action(intent.Action)));
        return descriptor;
    }

    private static CsExpression Action(int action) => Cs.Id("Nodes").Dot("ReferentialAction").Dot(action switch
    {
        0 => "NoAction", 1 => "Restrict", 2 => "Cascade", _ => "SetNull",
    });

    private static CsExpression New(string type, params CsExpression[] args) =>
        new CsExpression(ExpressionKind.New, "", new Values<CsExpression>(args)) { Type = Lite(type) };
}
