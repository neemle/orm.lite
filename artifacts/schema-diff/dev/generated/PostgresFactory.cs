#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @Generated117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @PostgresFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value62());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0200_add", "Generated117", "PostgresFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"recipe quoted"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "public", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("model-v2",
                "6268014e295b73b0b6f7e49e92dfed8970751077b78bb70a2e22c6fd15eb1d8f",
                "87f39c3291ee1079bd5ea59a0bb5a51ee9d31a3879640039601ed4b5b34d2011", "postgres:schema-diff-v1");

    private static R.SchemaDiffManifest Value4() =>
        new R.SchemaDiffManifest(Value0(), Value2(), Value3(), "recipe");

    private static N.TypeRef Value5() =>
        new N.TypeRef(N.ColumnType.Text);

    private static N.ColumnDef Value6() =>
        new N.ColumnDef("added", Value5(), true, false, null);

    private static N.AddColumn Value7() =>
        new N.AddColumn("recipe quoted", Value6(), false);

    private static G.IReadOnlyList<N.Node> Value8() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value7()});

    private static N.DropColumn Value9() =>
        new N.DropColumn("recipe quoted", "added", false);

    private static G.IReadOnlyList<N.Node> Value10() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value9()});

    private static R.SchemaDiffUnitNodes Value11() =>
        new R.SchemaDiffUnitNodes(Value8(), Value10());

    private static R.SchemaObservationIdentity Value12() =>
        new R.SchemaObservationIdentity("98369f334e1c5dce83eba5e9afc1e3bca430950944387b80c429690c729a6c17",
                "postgres:schema-diff-v1", true);

    private static R.SchemaFact<bool> Value13() =>
        R.SchemaFact<bool>.Known(true);

    private static G.IReadOnlyList<string> Value14() =>
        global::System.Array.AsReadOnly(new string[] {"0000_baseline_recipe", "0001_orm_indexes_recipe",
                "9900_orm_index_reconcile_recipe_cbf29ce484222325"});

    private static R.SchemaFact<G.IReadOnlyList<string>> Value15() =>
        R.SchemaFact<G.IReadOnlyList<string>>.Known(Value14());

    private static R.SchemaObservationHistory Value16() =>
        new R.SchemaObservationHistory(Value13(), Value15());

    private static R.EntityDescriptor Value17() =>
        R.EntityDescriptor.Of("recipe quoted", "recipe quoted", true);

    private static R.EntityDescriptor Value18() =>
        Value17().Property("Id", "id", N.ColumnType.Text, false, true, 0, 0);

    private static R.EntityDescriptor Value19() =>
        Value18().Property("Title", "title", N.ColumnType.Text, true, false, 0, 0);

    private static R.EntityDescriptor Value20() =>
        Value19().Property("Added", "added", N.ColumnType.Text, true, false, 0, 0);

    private static R.OrmSchema Value21() =>
        R.OrmSchema.Of("recipe", new R.EntityDescriptor[] { Value20() });

    private static R.SchemaDiffLimits Value22() =>
        new R.SchemaDiffLimits(2, 10, 100, 10, 100);

    private static R.SchemaDiffTarget Value23() =>
        new R.SchemaDiffTarget(Value21(), "model-v2", Value2(), Value22());

    private static R.SchemaDiffInspection Value24() =>
        new R.SchemaDiffInspection(Value23(), "postgres:schema-diff-v1");

    private static R.SchemaObjectStamp Value25() =>
        new R.SchemaObjectStamp("index", "recipe quoted_pkey",
                "d63497cca31ca3bfd68c0698715b47a60ee291476ed7d44054e81cc8b85ae19c");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value26() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value25()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value27() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value26());

    private static R.SchemaObjectStamp Value28() =>
        new R.SchemaObjectStamp("constraint:n", "recipe quoted_id_not_null",
                "6d7506d8233cbea9d9fe6b8d8f27f360db0fbffb967f9b95f57bf6ecda6cbe3a");

    private static R.SchemaObjectStamp Value29() =>
        new R.SchemaObjectStamp("constraint:p", "recipe quoted_pkey",
                "8c8464f42472e42ee190fc91ca8db79b5351d3a4609040516578d229c56f6fa5");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value30() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value28(), Value29()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value31() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value30());

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value32() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value33() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value32());

    private static string Value34() =>
        "table constraint:\u0022recipe quoted_id_not_null\u0022 on public." +
            "\u0022recipe quoted\u0022";

    private static R.SchemaObjectStamp Value35() =>
        new R.SchemaObjectStamp("dependent-local:a", Value34(),
                "7a20ba049a7ec851994b8cc6ea195ab809ea09c148b8a3f81267115a60a54bb8");

    private static string Value36() =>
        "table constraint:\u0022recipe quoted_pkey\u0022 on public.\u0022recipe" +
            " quoted\u0022";

    private static R.SchemaObjectStamp Value37() =>
        new R.SchemaObjectStamp("dependent-local:a", Value36(),
                "f5395ad80d7c90ca31523ff0622b41452d587b28e78d7b26f4f577724b449636");

    private static R.SchemaObjectStamp Value38() =>
        new R.SchemaObjectStamp("dependent-local:row-type", "row-type",
                "be70118ee0b3e0698b6908b2aa92b6067d5090fa93ef4432d9e466ea0638d249");

    private static R.SchemaObjectStamp Value39() =>
        new R.SchemaObjectStamp("dependent-local:toast", "toast-storage",
                "d6bc7b58f97d4a7841067ec2c3e2f99f541d161732f53cfe83f61d7d7bee2077");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value40() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value35(), Value37(), Value38(),
                Value39()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value41() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value40());

    private static R.SchemaObservedTableFacts Value42() =>
        new R.SchemaObservedTableFacts(Value27(), Value31(), Value33(), Value41());

    private static R.SchemaStorageType Value43() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value44() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value43());

    private static R.SchemaFact<bool> Value45() =>
        R.SchemaFact<bool>.Known(false);

    private static R.SchemaFact<int> Value46() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaFact<R.SchemaDefault> Value47() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value48() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value49() =>
        new R.SchemaObservedColumnFacts(Value47(), Value45(), Value45(), Value48());

    private static R.SchemaObservedColumn Value50() =>
        new R.SchemaObservedColumn("id", Value44(), Value45(), Value46(), Value49());

    private static R.SchemaFact<int> Value51() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaObservedColumn Value52() =>
        new R.SchemaObservedColumn("title", Value44(), Value13(), Value51(), Value49());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value53() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value50(), Value52()});

    private static R.SchemaObservedTable Value54() =>
        new R.SchemaObservedTable("recipe quoted", Value13(), Value53(), Value42());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value55() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value54()});

    private static R.SchemaDiffObservation Value56() =>
        R.SchemaDiffObservation.FromFacts(Value12(), Value55(), Value16(), Value24());

    private static R.SchemaObservedColumn Value57() =>
        new R.SchemaObservedColumn("added", Value44(), Value13(), Value51(), Value49());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value58() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value57(), Value50(), Value52()});

    private static R.SchemaObservedTable Value59() =>
        new R.SchemaObservedTable("recipe quoted", Value13(), Value58(), Value42());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value60() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value59()});

    private static R.SchemaDiffObservation Value61() =>
        R.SchemaDiffObservation.FromFacts(Value12(), Value60(), Value16(), Value24());

    private static R.SchemaDiffRecipe Value62() =>
        new R.SchemaDiffRecipe(Value4(), Value11(), Value56(), Value61());
}
