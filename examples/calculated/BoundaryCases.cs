using Neemle.OrmLite;
using Neemle.OrmLite.Generated;

namespace CalculatedDemo;

internal static class BoundaryCases
{
    internal static async Task RunAsync(Fixture f, string provider)
    {
        await NoteOrm.UpdateAsync(f.Db, new Note { Id = Fixture.Id(1), Label = "CHANGED", LowerLabel = "FALSE" });
        var read = await NoteOrm.GetAsync(f.Db, Fixture.Id(1));
        Results.Require(read?.LowerLabel == "changed" && read.Label == "CHANGED", "virtual update is ignored");
        Results.Case("write", read!.LowerLabel!);
        Results.Require(NoteOrm.Descriptor.ColumnNames.SequenceEqual(new[] { "id", "label" }), "physical metadata");
        Results.Require(NoteOrm.Descriptor.Indexes.All(i => i.Columns.SequenceEqual(new[] { "label" })), "physical index");
        var statements = EntityStatements.CreateTables(NoteOrm.Descriptor);
        Results.Require(statements.All(s => !StatementCompiler.Compile(s, f.Db.Provider, StatementKind.Write).Text
            .Contains("lower\"label", StringComparison.Ordinal)), "calculation absent from generated baseline DDL");
        await using var command = f.Db.DataSource.CreateCommand(provider == "sqlite"
            ? "SELECT name FROM pragma_table_info('demo_calculated_notes') ORDER BY name"
            : "SELECT column_name FROM information_schema.columns "
                + "WHERE table_schema='public' AND table_name='demo_calculated_notes' ORDER BY column_name");
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        Results.Require(columns.SequenceEqual(new[] { "id", "label" }), "actual schema has no virtual column");
        Results.Case("schema", string.Join("|", columns));
    }
}
