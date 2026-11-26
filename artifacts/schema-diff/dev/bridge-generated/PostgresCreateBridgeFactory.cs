#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @GeneratedBridge117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @PostgresCreateBridgeFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value66());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0310_bridge_create", "GeneratedBridge117", "PostgresCreateBridgeFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"bridge created"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "public", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("bridge-v2",
                "f3f5b9e78f53f5388c36deca1f24426922130a6b2f9decaea74b5c69470471ec",
                "0f8ac1f7f10db089324afb140e7f3b9c4227b118565c884c78acafd6ea2ad6f2", "postgres:schema-diff-v1");

    private static R.SchemaDiffManifest Value4() =>
        new R.SchemaDiffManifest(Value0(), Value2(), Value3(), "bridge-create");

    private static N.TypeRef Value5() =>
        new N.TypeRef(N.ColumnType.Text);

    private static N.ColumnDef Value6() =>
        new N.ColumnDef("id", Value5(), false, true, null);

    private static N.ColumnDef Value7() =>
        new N.ColumnDef("title", Value5(), true, false, null);

    private static N.ColumnDef Value8() =>
        new N.ColumnDef("added", Value5(), true, false, null);

    private static G.IReadOnlyList<N.Node> Value9() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value6(), Value7(), Value8()});

    private static N.NodeList Value10() =>
        N.NodeList.Of(Value9());

    private static N.CreateTable Value11() =>
        new N.CreateTable("bridge created", Value10());

    private static G.IReadOnlyList<N.Node> Value12() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value11()});

    private static N.DropTable Value13() =>
        new N.DropTable("bridge created");

    private static G.IReadOnlyList<N.Node> Value14() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value13()});

    private static R.SchemaDiffUnitNodes Value15() =>
        new R.SchemaDiffUnitNodes(Value12(), Value14());

    private static R.SchemaObservationIdentity Value16() =>
        new R.SchemaObservationIdentity("98369f334e1c5dce83eba5e9afc1e3bca430950944387b80c429690c729a6c17",
                "postgres:schema-diff-v1", true);

    private static R.SchemaFact<bool> Value17() =>
        R.SchemaFact<bool>.Known(true);

    private static string Value18() =>
        "0300_bridge_add_36d76f349d17cabff19413f1bfb32ae90c284a901b1cfb904d82c3" +
            "dfeeb745c2";

    private static G.IReadOnlyList<string> Value19() =>
        global::System.Array.AsReadOnly(new string[] {"0000_baseline_bridge-add",
                "0000_baseline_bridge-create", "0001_orm_indexes_bridge-add", "0001_orm_indexes_bridge-create",
                Value18(), "9900_orm_index_reconcile_bridge-add_cbf29ce484222325",
                "9900_orm_index_reconcile_bridge-create_cbf29ce484222325"});

    private static R.SchemaFact<G.IReadOnlyList<string>> Value20() =>
        R.SchemaFact<G.IReadOnlyList<string>>.Known(Value19());

    private static R.SchemaObservationHistory Value21() =>
        new R.SchemaObservationHistory(Value17(), Value20());

    private static R.EntityDescriptor Value22() =>
        R.EntityDescriptor.Of("bridge created", "bridge created", true);

    private static R.EntityDescriptor Value23() =>
        Value22().Property("Id", "id", N.ColumnType.Text, false, true, 0, 0);

    private static R.EntityDescriptor Value24() =>
        Value23().Property("Title", "title", N.ColumnType.Text, true, false, 0, 0);

    private static R.EntityDescriptor Value25() =>
        Value24().Property("Added", "added", N.ColumnType.Text, true, false, 0, 0);

    private static R.OrmSchema Value26() =>
        R.OrmSchema.Of("bridge-create", new R.EntityDescriptor[] { Value25() });

    private static R.SchemaDiffLimits Value27() =>
        new R.SchemaDiffLimits(5, 50, 500, 50, 5000);

    private static R.SchemaDiffTarget Value28() =>
        new R.SchemaDiffTarget(Value26(), "bridge-v2", Value2(), Value27());

    private static R.SchemaDiffInspection Value29() =>
        new R.SchemaDiffInspection(Value28(), "postgres:schema-diff-v1");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value30() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value31() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value30());

    private static R.SchemaObservedTableFacts Value32() =>
        new R.SchemaObservedTableFacts(Value31(), Value31(), Value31(), Value31());

    private static R.SchemaFact<bool> Value33() =>
        R.SchemaFact<bool>.Known(false);

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value34() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {});

    private static R.SchemaObservedTable Value35() =>
        new R.SchemaObservedTable("bridge created", Value33(), Value34(), Value32());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value36() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value35()});

    private static R.SchemaDiffObservation Value37() =>
        R.SchemaDiffObservation.FromFacts(Value16(), Value36(), Value21(), Value29());

    private static R.SchemaObjectStamp Value38() =>
        new R.SchemaObjectStamp("index:pk", "__orml_sd_pk_7eb95c51a67543736326df5ccb50d5eafde65af3",
                "dd537a633cb9eb3b361d176fa60fd13f996fbfe667e8296147ed720588dfe1a8");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value39() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value38()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value40() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value39());

    private static R.SchemaObjectStamp Value41() =>
        new R.SchemaObjectStamp("constraint:n", "__orml_sd_nn_159499d83371e4c38b8b37477b5e4511ea3b032f",
                "a66d15a21fc0fc6f85593a0f49b5ca2d961d975252009ce85f792542f7b3a0a4");

    private static R.SchemaObjectStamp Value42() =>
        new R.SchemaObjectStamp("constraint:p", "__orml_sd_pk_7eb95c51a67543736326df5ccb50d5eafde65af3",
                "49df1c3374799578109fdee127ac90a6c9f865bc792f818f57df7794198af56e");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value43() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value41(), Value42()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value44() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value43());

    private static R.SchemaObjectStamp Value45() =>
        new R.SchemaObjectStamp("dependent-local:constraint",
                "__orml_sd_nn_159499d83371e4c38b8b37477b5e4511ea3b032f",
                "a66d15a21fc0fc6f85593a0f49b5ca2d961d975252009ce85f792542f7b3a0a4");

    private static R.SchemaObjectStamp Value46() =>
        new R.SchemaObjectStamp("dependent-local:constraint",
                "__orml_sd_pk_7eb95c51a67543736326df5ccb50d5eafde65af3",
                "49df1c3374799578109fdee127ac90a6c9f865bc792f818f57df7794198af56e");

    private static R.SchemaObjectStamp Value47() =>
        new R.SchemaObjectStamp("dependent-local:row-type", "row-type",
                "be70118ee0b3e0698b6908b2aa92b6067d5090fa93ef4432d9e466ea0638d249");

    private static R.SchemaObjectStamp Value48() =>
        new R.SchemaObjectStamp("dependent-local:toast", "toast-storage",
                "d6bc7b58f97d4a7841067ec2c3e2f99f541d161732f53cfe83f61d7d7bee2077");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value49() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value45(), Value46(), Value47(),
                Value48()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value50() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value49());

    private static R.SchemaObservedTableFacts Value51() =>
        new R.SchemaObservedTableFacts(Value40(), Value44(), Value31(), Value50());

    private static R.SchemaStorageType Value52() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value53() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value52());

    private static R.SchemaFact<int> Value54() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaFact<R.SchemaDefault> Value55() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value56() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value57() =>
        new R.SchemaObservedColumnFacts(Value55(), Value33(), Value33(), Value56());

    private static R.SchemaObservedColumn Value58() =>
        new R.SchemaObservedColumn("added", Value53(), Value17(), Value54(), Value57());

    private static R.SchemaFact<int> Value59() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaObservedColumn Value60() =>
        new R.SchemaObservedColumn("id", Value53(), Value33(), Value59(), Value57());

    private static R.SchemaObservedColumn Value61() =>
        new R.SchemaObservedColumn("title", Value53(), Value17(), Value54(), Value57());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value62() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value58(), Value60(), Value61()});

    private static R.SchemaObservedTable Value63() =>
        new R.SchemaObservedTable("bridge created", Value17(), Value62(), Value51());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value64() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value63()});

    private static R.SchemaDiffObservation Value65() =>
        R.SchemaDiffObservation.FromFacts(Value16(), Value64(), Value21(), Value29());

    private static R.SchemaDiffRecipe Value66() =>
        new R.SchemaDiffRecipe(Value4(), Value15(), Value37(), Value65());
}
