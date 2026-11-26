#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @Generated117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @SqliteCreateFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value60());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0210_create", "Generated117", "SqliteCreateFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"emit quoted"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "main", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("model-v1",
                "9e9fc60e48684d6a3940a9df0519f23cf4b6726634010c074e1b251b2512598b",
                "6a50624eed1a2520fc3bb643dafb54548194bfd025dd1fff40dfe78890a1deb2", "sqlite:schema-diff-v1");

    private static R.SchemaDiffManifest Value4() =>
        new R.SchemaDiffManifest(Value0(), Value2(), Value3(), "emit");

    private static N.TypeRef Value5() =>
        new N.TypeRef(N.ColumnType.Text);

    private static N.ColumnDef Value6() =>
        new N.ColumnDef("id", Value5(), false, true, null);

    private static N.ColumnDef Value7() =>
        new N.ColumnDef("title", Value5(), true, false, null);

    private static N.TypeRef Value8() =>
        new N.TypeRef(N.ColumnType.Int32);

    private static N.ColumnDef Value9() =>
        new N.ColumnDef("required", Value8(), false, false, new N.IntLit(0));

    private static G.IReadOnlyList<N.Node> Value10() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value6(), Value7(), Value9()});

    private static N.NodeList Value11() =>
        N.NodeList.Of(Value10());

    private static N.CreateTable Value12() =>
        new N.CreateTable("emit quoted", Value11());

    private static G.IReadOnlyList<N.Node> Value13() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value12()});

    private static N.DropTable Value14() =>
        new N.DropTable("emit quoted");

    private static G.IReadOnlyList<N.Node> Value15() =>
        global::System.Array.AsReadOnly(new N.Node[] {Value14()});

    private static R.SchemaDiffUnitNodes Value16() =>
        new R.SchemaDiffUnitNodes(Value13(), Value15());

    private static R.SchemaObservationIdentity Value17() =>
        new R.SchemaObservationIdentity("400da62fd8acd17442a1213a05e744d7a6377488e7b2eeee2c17a3083a0d2cb9",
                "sqlite:schema-diff-v1", true);

    private static R.SchemaFact<bool> Value18() =>
        R.SchemaFact<bool>.Known(false);

    private static G.IReadOnlyList<string> Value19() =>
        global::System.Array.AsReadOnly(new string[] {});

    private static R.SchemaFact<G.IReadOnlyList<string>> Value20() =>
        R.SchemaFact<G.IReadOnlyList<string>>.Known(Value19());

    private static R.SchemaObservationHistory Value21() =>
        new R.SchemaObservationHistory(Value18(), Value20());

    private static R.EntityDescriptor Value22() =>
        R.EntityDescriptor.Of("emit quoted", "emit quoted", true);

    private static R.EntityDescriptor Value23() =>
        Value22().Property("Id", "id", N.ColumnType.Text, false, true, 0, 0);

    private static R.EntityDescriptor Value24() =>
        Value23().Property("Title", "title", N.ColumnType.Text, true, false, 0, 0);

    private static R.EntityDescriptor Value25() =>
        Value24().Property("Required", "required", N.ColumnType.Int32, false, false, 0, 0);

    private static R.OrmSchema Value26() =>
        R.OrmSchema.Of("emit", new R.EntityDescriptor[] { Value25() });

    private static R.SchemaDiffLimits Value27() =>
        new R.SchemaDiffLimits(2, 10, 100, 10, 100);

    private static R.SchemaDiffTarget Value28() =>
        new R.SchemaDiffTarget(Value26(), "model-v1", Value2(), Value27());

    private static R.SchemaDiffInspection Value29() =>
        new R.SchemaDiffInspection(Value28(), "sqlite:schema-diff-v1");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value30() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value31() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value30());

    private static R.SchemaObservedTableFacts Value32() =>
        new R.SchemaObservedTableFacts(Value31(), Value31(), Value31(), Value31());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value33() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {});

    private static R.SchemaObservedTable Value34() =>
        new R.SchemaObservedTable("emit quoted", Value18(), Value33(), Value32());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value35() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value34()});

    private static R.SchemaDiffObservation Value36() =>
        R.SchemaDiffObservation.FromFacts(Value17(), Value35(), Value21(), Value29());

    private static R.SchemaObjectStamp Value37() =>
        new R.SchemaObjectStamp("index:pk", "sqlite_autoindex_emit quoted_1",
                "21e1cbf3024b04b4dd8315fd60edceb2b31a16e620dd64d533cd42343d32d303");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value38() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value37()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value39() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value38());

    private static R.SchemaObservedTableFacts Value40() =>
        new R.SchemaObservedTableFacts(Value39(), Value31(), Value31(), Value31());

    private static R.SchemaFact<bool> Value41() =>
        R.SchemaFact<bool>.Known(true);

    private static R.SchemaStorageType Value42() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value43() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value42());

    private static R.SchemaFact<int> Value44() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaFact<R.SchemaDefault> Value45() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value46() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value47() =>
        new R.SchemaObservedColumnFacts(Value45(), Value18(), Value18(), Value46());

    private static R.SchemaObservedColumn Value48() =>
        new R.SchemaObservedColumn("id", Value43(), Value18(), Value44(), Value47());

    private static R.SchemaStorageType Value49() =>
        new R.SchemaStorageType("integer", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value50() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value49());

    private static R.SchemaFact<int> Value51() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaFact<R.SchemaDefault> Value52() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.Zero));

    private static R.SchemaObservedColumnFacts Value53() =>
        new R.SchemaObservedColumnFacts(Value52(), Value18(), Value18(), Value46());

    private static R.SchemaObservedColumn Value54() =>
        new R.SchemaObservedColumn("required", Value50(), Value18(), Value51(), Value53());

    private static R.SchemaObservedColumn Value55() =>
        new R.SchemaObservedColumn("title", Value43(), Value41(), Value51(), Value47());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value56() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value48(), Value54(), Value55()});

    private static R.SchemaObservedTable Value57() =>
        new R.SchemaObservedTable("emit quoted", Value41(), Value56(), Value40());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value58() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value57()});

    private static R.SchemaDiffObservation Value59() =>
        R.SchemaDiffObservation.FromFacts(Value17(), Value58(), Value21(), Value29());

    private static R.SchemaDiffRecipe Value60() =>
        new R.SchemaDiffRecipe(Value4(), Value16(), Value36(), Value59());
}
