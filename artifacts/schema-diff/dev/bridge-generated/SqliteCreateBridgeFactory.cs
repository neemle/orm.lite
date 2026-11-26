#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @GeneratedBridge117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @SqliteCreateBridgeFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value55());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0310_bridge_create", "GeneratedBridge117", "SqliteCreateBridgeFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"bridge created"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "main", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("bridge-v2",
                "f3f5b9e78f53f5388c36deca1f24426922130a6b2f9decaea74b5c69470471ec",
                "247d3e4322afbc6082ca8740ef60f621a2f0d93db1c34ed6a469d6d0f5d23bd7", "sqlite:schema-diff-v1");

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
        new R.SchemaObservationIdentity("1065cbcdb2f392b9e555ead2cc7c8426568214bd98b7777119993a911326a80e",
                "sqlite:schema-diff-v1", true);

    private static R.SchemaFact<bool> Value17() =>
        R.SchemaFact<bool>.Known(true);

    private static G.IReadOnlyList<string> Value18() =>
        global::System.Array.AsReadOnly(new string[] {"0000_baseline_bridge-create",
                "0001_orm_indexes_bridge-create", "9900_orm_index_reconcile_bridge-create_cbf29ce484222325"});

    private static R.SchemaFact<G.IReadOnlyList<string>> Value19() =>
        R.SchemaFact<G.IReadOnlyList<string>>.Known(Value18());

    private static R.SchemaObservationHistory Value20() =>
        new R.SchemaObservationHistory(Value17(), Value19());

    private static R.EntityDescriptor Value21() =>
        R.EntityDescriptor.Of("bridge created", "bridge created", true);

    private static R.EntityDescriptor Value22() =>
        Value21().Property("Id", "id", N.ColumnType.Text, false, true, 0, 0);

    private static R.EntityDescriptor Value23() =>
        Value22().Property("Title", "title", N.ColumnType.Text, true, false, 0, 0);

    private static R.EntityDescriptor Value24() =>
        Value23().Property("Added", "added", N.ColumnType.Text, true, false, 0, 0);

    private static R.OrmSchema Value25() =>
        R.OrmSchema.Of("bridge-create", new R.EntityDescriptor[] { Value24() });

    private static R.SchemaDiffLimits Value26() =>
        new R.SchemaDiffLimits(5, 50, 500, 50, 5000);

    private static R.SchemaDiffTarget Value27() =>
        new R.SchemaDiffTarget(Value25(), "bridge-v2", Value2(), Value26());

    private static R.SchemaDiffInspection Value28() =>
        new R.SchemaDiffInspection(Value27(), "sqlite:schema-diff-v1");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value29() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value30() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value29());

    private static R.SchemaObservedTableFacts Value31() =>
        new R.SchemaObservedTableFacts(Value30(), Value30(), Value30(), Value30());

    private static R.SchemaFact<bool> Value32() =>
        R.SchemaFact<bool>.Known(false);

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value33() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {});

    private static R.SchemaObservedTable Value34() =>
        new R.SchemaObservedTable("bridge created", Value32(), Value33(), Value31());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value35() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value34()});

    private static R.SchemaDiffObservation Value36() =>
        R.SchemaDiffObservation.FromFacts(Value16(), Value35(), Value20(), Value28());

    private static R.SchemaObjectStamp Value37() =>
        new R.SchemaObjectStamp("index:pk", "sqlite_autoindex_bridge created_1",
                "21e1cbf3024b04b4dd8315fd60edceb2b31a16e620dd64d533cd42343d32d303");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value38() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value37()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value39() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value38());

    private static R.SchemaObservedTableFacts Value40() =>
        new R.SchemaObservedTableFacts(Value39(), Value30(), Value30(), Value30());

    private static R.SchemaStorageType Value41() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value42() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value41());

    private static R.SchemaFact<int> Value43() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaFact<R.SchemaDefault> Value44() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value45() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value46() =>
        new R.SchemaObservedColumnFacts(Value44(), Value32(), Value32(), Value45());

    private static R.SchemaObservedColumn Value47() =>
        new R.SchemaObservedColumn("added", Value42(), Value17(), Value43(), Value46());

    private static R.SchemaFact<int> Value48() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaObservedColumn Value49() =>
        new R.SchemaObservedColumn("id", Value42(), Value32(), Value48(), Value46());

    private static R.SchemaObservedColumn Value50() =>
        new R.SchemaObservedColumn("title", Value42(), Value17(), Value43(), Value46());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value51() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value47(), Value49(), Value50()});

    private static R.SchemaObservedTable Value52() =>
        new R.SchemaObservedTable("bridge created", Value17(), Value51(), Value40());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value53() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value52()});

    private static R.SchemaDiffObservation Value54() =>
        R.SchemaDiffObservation.FromFacts(Value16(), Value53(), Value20(), Value28());

    private static R.SchemaDiffRecipe Value55() =>
        new R.SchemaDiffRecipe(Value4(), Value15(), Value36(), Value54());
}
