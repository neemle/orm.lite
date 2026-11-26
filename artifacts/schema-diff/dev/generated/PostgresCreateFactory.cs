#nullable enable
using R = global::Neemle.OrmLite;
using N = global::Neemle.OrmLite.Nodes;
using G = global::System.Collections.Generic;

namespace @Generated117;

// Down drops added columns/tables and loses post-Up data. Backup is required to retain that data.
// DownProofRequired is an obligation: establish actual Up/Down/Up fixture evidence before acceptance.
// Comparison proves supported physical schema facts, not historical logical-domain intent.
public static class @PostgresCreateFactory
{
    public static R.SchemaDiffArtifact Create() => R.SchemaDiffArtifact.FromRecipe(Value73());

    private static R.SchemaDiffIdentity Value0() =>
        new R.SchemaDiffIdentity("0210_create", "Generated117", "PostgresCreateFactory");

    private static G.IReadOnlyList<string> Value1() =>
        global::System.Array.AsReadOnly(new string[] {"emit quoted"});

    private static R.SchemaDiffScope Value2() =>
        new R.SchemaDiffScope("fixture117", "public", Value1());

    private static R.SchemaDiffProvenance Value3() =>
        new R.SchemaDiffProvenance("model-v1",
                "9e9fc60e48684d6a3940a9df0519f23cf4b6726634010c074e1b251b2512598b",
                "f4826017a9c6ad29a0c6433533c6ef4ea4ba85a4485685b6ab09ddb41cf09fa2", "postgres:schema-diff-v1");

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
        new R.SchemaObservationIdentity("98369f334e1c5dce83eba5e9afc1e3bca430950944387b80c429690c729a6c17",
                "postgres:schema-diff-v1", true);

    private static R.SchemaFact<bool> Value18() =>
        R.SchemaFact<bool>.Known(true);

    private static G.IReadOnlyList<string> Value19() =>
        global::System.Array.AsReadOnly(new string[] {"0000_baseline_recipe", "0001_orm_indexes_recipe",
                "9900_orm_index_reconcile_recipe_cbf29ce484222325"});

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
        new R.SchemaObservedTable("emit quoted", Value33(), Value34(), Value32());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value36() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value35()});

    private static R.SchemaDiffObservation Value37() =>
        R.SchemaDiffObservation.FromFacts(Value17(), Value36(), Value21(), Value29());

    private static R.SchemaObjectStamp Value38() =>
        new R.SchemaObjectStamp("index:pk", "__orml_sd_pk_d3aede532352bfc7f81fac79a53679801b9a2802",
                "dd537a633cb9eb3b361d176fa60fd13f996fbfe667e8296147ed720588dfe1a8");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value39() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value38()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value40() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value39());

    private static R.SchemaObjectStamp Value41() =>
        new R.SchemaObjectStamp("constraint:n", "__orml_sd_nn_22278759670e37ada0d6231bfde560df00258309",
                "237548826521ecc02f08e8c123bcc20ec90678d8a5916a10e486729094cd4e5c");

    private static R.SchemaObjectStamp Value42() =>
        new R.SchemaObjectStamp("constraint:n", "__orml_sd_nn_60b00c7a7bdfd92e4e7e50f0305d2c73aaf7f819",
                "a66d15a21fc0fc6f85593a0f49b5ca2d961d975252009ce85f792542f7b3a0a4");

    private static R.SchemaObjectStamp Value43() =>
        new R.SchemaObjectStamp("constraint:p", "__orml_sd_pk_d3aede532352bfc7f81fac79a53679801b9a2802",
                "49df1c3374799578109fdee127ac90a6c9f865bc792f818f57df7794198af56e");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value44() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value41(), Value42(), Value43()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value45() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value44());

    private static R.SchemaObjectStamp Value46() =>
        new R.SchemaObjectStamp("dependent-local:constraint",
                "__orml_sd_nn_22278759670e37ada0d6231bfde560df00258309",
                "237548826521ecc02f08e8c123bcc20ec90678d8a5916a10e486729094cd4e5c");

    private static R.SchemaObjectStamp Value47() =>
        new R.SchemaObjectStamp("dependent-local:constraint",
                "__orml_sd_nn_60b00c7a7bdfd92e4e7e50f0305d2c73aaf7f819",
                "a66d15a21fc0fc6f85593a0f49b5ca2d961d975252009ce85f792542f7b3a0a4");

    private static R.SchemaObjectStamp Value48() =>
        new R.SchemaObjectStamp("dependent-local:constraint",
                "__orml_sd_pk_d3aede532352bfc7f81fac79a53679801b9a2802",
                "49df1c3374799578109fdee127ac90a6c9f865bc792f818f57df7794198af56e");

    private static R.SchemaObjectStamp Value49() =>
        new R.SchemaObjectStamp("dependent-local:default", "required",
                "26c5bbf0e13af3ec1370d6c4e4e8f8bdf598c24a84cbc255b665bbf1c56c18fd");

    private static R.SchemaObjectStamp Value50() =>
        new R.SchemaObjectStamp("dependent-local:row-type", "row-type",
                "be70118ee0b3e0698b6908b2aa92b6067d5090fa93ef4432d9e466ea0638d249");

    private static R.SchemaObjectStamp Value51() =>
        new R.SchemaObjectStamp("dependent-local:toast", "toast-storage",
                "d6bc7b58f97d4a7841067ec2c3e2f99f541d161732f53cfe83f61d7d7bee2077");

    private static G.IReadOnlyList<R.SchemaObjectStamp> Value52() =>
        global::System.Array.AsReadOnly(new R.SchemaObjectStamp[] {Value46(), Value47(), Value48(),
                Value49(), Value50(), Value51()});

    private static R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>> Value53() =>
        R.SchemaFact<G.IReadOnlyList<R.SchemaObjectStamp>>.Known(Value52());

    private static R.SchemaObservedTableFacts Value54() =>
        new R.SchemaObservedTableFacts(Value40(), Value45(), Value31(), Value53());

    private static R.SchemaStorageType Value55() =>
        new R.SchemaStorageType("text", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value56() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value55());

    private static R.SchemaFact<int> Value57() =>
        R.SchemaFact<int>.Known(1);

    private static R.SchemaFact<R.SchemaDefault> Value58() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.None));

    private static R.SchemaFact<string?> Value59() =>
        R.SchemaFact<string?>.Known(null);

    private static R.SchemaObservedColumnFacts Value60() =>
        new R.SchemaObservedColumnFacts(Value58(), Value33(), Value33(), Value59());

    private static R.SchemaObservedColumn Value61() =>
        new R.SchemaObservedColumn("id", Value56(), Value33(), Value57(), Value60());

    private static R.SchemaStorageType Value62() =>
        new R.SchemaStorageType("integer", null, null);

    private static R.SchemaFact<R.SchemaStorageType> Value63() =>
        R.SchemaFact<R.SchemaStorageType>.Known(Value62());

    private static R.SchemaFact<int> Value64() =>
        R.SchemaFact<int>.Known(0);

    private static R.SchemaFact<R.SchemaDefault> Value65() =>
        R.SchemaFact<R.SchemaDefault>.Known(new R.SchemaDefault(R.SchemaDefaultKind.Zero));

    private static R.SchemaObservedColumnFacts Value66() =>
        new R.SchemaObservedColumnFacts(Value65(), Value33(), Value33(), Value59());

    private static R.SchemaObservedColumn Value67() =>
        new R.SchemaObservedColumn("required", Value63(), Value33(), Value64(), Value66());

    private static R.SchemaObservedColumn Value68() =>
        new R.SchemaObservedColumn("title", Value56(), Value18(), Value64(), Value60());

    private static G.IReadOnlyList<R.SchemaObservedColumn> Value69() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedColumn[] {Value61(), Value67(), Value68()});

    private static R.SchemaObservedTable Value70() =>
        new R.SchemaObservedTable("emit quoted", Value18(), Value69(), Value54());

    private static G.IReadOnlyList<R.SchemaObservedTable> Value71() =>
        global::System.Array.AsReadOnly(new R.SchemaObservedTable[] {Value70()});

    private static R.SchemaDiffObservation Value72() =>
        R.SchemaDiffObservation.FromFacts(Value17(), Value71(), Value21(), Value29());

    private static R.SchemaDiffRecipe Value73() =>
        new R.SchemaDiffRecipe(Value4(), Value16(), Value37(), Value72());
}
