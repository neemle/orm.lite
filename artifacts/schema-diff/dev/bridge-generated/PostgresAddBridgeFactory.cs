#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @GeneratedBridge117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @PostgresAddBridgeFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value60());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0300_bridge_add", "GeneratedBridge117", "PostgresAddBridgeFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"bridge items"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "public", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("bridge-v2",
                "892220cd1e146a83eb9a2e3b9faa5789968d4523c9ee49fe0a2f2afb8e3aa98d",
                "8ba530756deb29dcfbabd7ea7875b5cb386cb2a1a0607cc10c76917b6bf126bd", "postgres:schema-diff-v1");

    private static R.SchemaDiffManifest Value4() =>
        new R.SchemaDiffManifest(Value0(), Value2(), Value3(), "bridge-add");

    private static N.TypeRef Value5() =>
        new N.TypeRef(N.ColumnType.Text);

    private static N.ColumnDef Value6() =>
        new N.ColumnDef("added", Value5(), true, false, null);

    private static N.AddColumn Value7() =>
        new N.AddColumn("bridge items", Value6(), false);

    private static G.IReadOnlyList<N.Node> Value8() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value7()});

    private static N.DropColumn Value9() =>
        new N.DropColumn("bridge items", "added", false);

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
        global::System.Array.AsReadOnly(new string[] {"0000_baseline_bridge-add",
                "0001_orm_indexes_bridge-add", "9900_orm_index_reconcile_bridge-add_cbf29ce484222325"});

    private static R.SchemaFact<G.IReadOnlyList<string>> Value15() =>
        R.SchemaFact<G.IReadOnlyList<string>>.Known(Value14());

    private static R.SchemaObservationHistory Value16() =>
        new R.SchemaObservationHistory(Value13(), Value15());

    private static R.EntityDescriptor Value17() =>
        R.EntityDescriptor.Of("bridge items", "bridge items", true);

    private static R.EntityDescriptor Value18() =>
        Value17().Property("Id", "id", N.ColumnType.Text, false, true, 0, 0);

    private static R.EntityDescriptor Value19() =>
        Value18().Property("Title", "title", N.ColumnType.Text, true, false, 0, 0);

    private static R.EntityDescriptor Value20() =>
        Value19().Property("Added", "added", N.ColumnType.Text, true, false, 0, 0);

    private static R.OrmSchema Value21() =>
        R.OrmSchema.Of("bridge-add", new R.EntityDescriptor[] { Value20() });

    private static R.SchemaDiffLimits Value22() =>
        new R.SchemaDiffLimits(5, 50, 500, 50, 5000);

    private static R.SchemaDiffTarget Value23() =>
        new R.SchemaDiffTarget(Value21(), "bridge-v2", Value2(), Value22());

    private static R.SchemaDiffInspection Value24() =>
        new R.SchemaDiffInspection(Value23(), "postgres:schema-diff-v1");

    private static R.SchemaObjectStamp Value25() =>
        new R.SchemaObjectStamp("index:pk", "bridge items_pkey",
                "dd537a633cb9eb3b361d176fa60fd13f996fbfe667e8296147ed720588dfe1a8");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value26() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value25()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value27() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value26());

    private static R.SchemaObjectStamp Value28() =>
        new R.SchemaObjectStamp("constraint:n", "bridge items_id_not_null",
                "a66d15a21fc0fc6f85593a0f49b5ca2d961d975252009ce85f792542f7b3a0a4");

    private static R.SchemaObjectStamp Value29() =>
        new R.SchemaObjectStamp("constraint:p", "bridge items_pkey",
                "49df1c3374799578109fdee127ac90a6c9f865bc792f818f57df7794198af56e");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value30() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value28(), Value29()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value31() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value30());

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value32() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value33() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value32());

    private static R.SchemaObjectStamp Value34() =>
        new R.SchemaObjectStamp("dependent-local:constraint", "bridge items_id_not_null",
                "a66d15a21fc0fc6f85593a0f49b5ca2d961d975252009ce85f792542f7b3a0a4");

    private static R.SchemaObjectStamp Value35() =>
        new R.SchemaObjectStamp("dependent-local:constraint", "bridge items_pkey",
                "49df1c3374799578109fdee127ac90a6c9f865bc792f818f57df7794198af56e");

    private static R.SchemaObjectStamp Value36() =>
        new R.SchemaObjectStamp("dependent-local:row-type", "row-type",
                "be70118ee0b3e0698b6908b2aa92b6067d5090fa93ef4432d9e466ea0638d249");

    private static R.SchemaObjectStamp Value37() =>
        new R.SchemaObjectStamp("dependent-local:toast", "toast-storage",
                "d6bc7b58f97d4a7841067ec2c3e2f99f541d161732f53cfe83f61d7d7bee2077");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value38() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value34(), Value35(), Value36(),
                Value37()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value39() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value38());

    private static R.SchemaObservedTableFacts Value40() =>
        new R.SchemaObservedTableFacts(Value27(), Value31(), Value33(), Value39());

    private static R.SchemaStorageType Value41() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value42() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value41());

    private static R.SchemaFact<bool> Value43() =>
        R.SchemaFact<bool>.Known(false);

    private static R.SchemaFact<int> Value44() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaFact<R.SchemaDefault> Value45() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value46() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value47() =>
        new R.SchemaObservedColumnFacts(Value45(), Value43(), Value43(), Value46());

    private static R.SchemaObservedColumn Value48() =>
        new R.SchemaObservedColumn("id", Value42(), Value43(), Value44(), Value47());

    private static R.SchemaFact<int> Value49() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaObservedColumn Value50() =>
        new R.SchemaObservedColumn("title", Value42(), Value13(), Value49(), Value47());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value51() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value48(), Value50()});

    private static R.SchemaObservedTable Value52() =>
        new R.SchemaObservedTable("bridge items", Value13(), Value51(), Value40());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value53() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value52()});

    private static R.SchemaDiffObservation Value54() =>
        R.SchemaDiffObservation.FromFacts(Value12(), Value53(), Value16(), Value24());

    private static R.SchemaObservedColumn Value55() =>
        new R.SchemaObservedColumn("added", Value42(), Value13(), Value49(), Value47());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value56() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value55(), Value48(), Value50()});

    private static R.SchemaObservedTable Value57() =>
        new R.SchemaObservedTable("bridge items", Value13(), Value56(), Value40());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value58() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value57()});

    private static R.SchemaDiffObservation Value59() =>
        R.SchemaDiffObservation.FromFacts(Value12(), Value58(), Value16(), Value24());

    private static R.SchemaDiffRecipe Value60() =>
        new R.SchemaDiffRecipe(Value4(), Value11(), Value54(), Value59());
}
