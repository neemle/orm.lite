#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @GeneratedBridge117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @SqliteAddBridgeFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value50());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0300_bridge_add", "GeneratedBridge117", "SqliteAddBridgeFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"bridge items"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "main", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("bridge-v2",
                "892220cd1e146a83eb9a2e3b9faa5789968d4523c9ee49fe0a2f2afb8e3aa98d",
                "1090a19c4a2e4632de69b02a0b709d0c2eb2e5e95650dd8f7e73bdd603378630", "sqlite:schema-diff-v1");

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
        new R.SchemaObservationIdentity("4c5ff40839f30f9814a27fddf6cd23c1da22c64063facd8210aaafca640600da",
                "sqlite:schema-diff-v1", true);

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
        new R.SchemaDiffInspection(Value23(), "sqlite:schema-diff-v1");

    private static R.SchemaObjectStamp Value25() =>
        new R.SchemaObjectStamp("index:pk", "sqlite_autoindex_bridge items_1",
                "21e1cbf3024b04b4dd8315fd60edceb2b31a16e620dd64d533cd42343d32d303");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value26() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value25()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value27() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value26());

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value28() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value29() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value28());

    private static R.SchemaObservedTableFacts Value30() =>
        new R.SchemaObservedTableFacts(Value27(), Value29(), Value29(), Value29());

    private static R.SchemaStorageType Value31() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value32() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value31());

    private static R.SchemaFact<bool> Value33() =>
        R.SchemaFact<bool>.Known(false);

    private static R.SchemaFact<int> Value34() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaFact<R.SchemaDefault> Value35() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value36() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value37() =>
        new R.SchemaObservedColumnFacts(Value35(), Value33(), Value33(), Value36());

    private static R.SchemaObservedColumn Value38() =>
        new R.SchemaObservedColumn("id", Value32(), Value33(), Value34(), Value37());

    private static R.SchemaFact<int> Value39() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaObservedColumn Value40() =>
        new R.SchemaObservedColumn("title", Value32(), Value13(), Value39(), Value37());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value41() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value38(), Value40()});

    private static R.SchemaObservedTable Value42() =>
        new R.SchemaObservedTable("bridge items", Value13(), Value41(), Value30());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value43() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value42()});

    private static R.SchemaDiffObservation Value44() =>
        R.SchemaDiffObservation.FromFacts(Value12(), Value43(), Value16(), Value24());

    private static R.SchemaObservedColumn Value45() =>
        new R.SchemaObservedColumn("added", Value32(), Value13(), Value39(), Value37());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value46() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value45(), Value38(), Value40()});

    private static R.SchemaObservedTable Value47() =>
        new R.SchemaObservedTable("bridge items", Value13(), Value46(), Value30());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value48() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value47()});

    private static R.SchemaDiffObservation Value49() =>
        R.SchemaDiffObservation.FromFacts(Value12(), Value48(), Value16(), Value24());

    private static R.SchemaDiffRecipe Value50() =>
        new R.SchemaDiffRecipe(Value4(), Value11(), Value44(), Value49());
}
