using System.Data.Common;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

internal static class SdetHostileIdentifiers
{
    private const string ParentTable = "p_\"; DROP TABLE sentinel;--";
    private const string ChildTable = "c_\"; DELETE FROM sentinel;--";
    private const string ParentColumn = "pk_\";--";
    private const string ChildColumn = "owner_\";--";

    internal static async Task VerifyAsync(ForeignKeyDatabase database)
    {
        await database.ExecuteAsync("CREATE TABLE sentinel(id integer)");
        await database.ExecuteAsync("INSERT INTO sentinel(id) VALUES (42)");
        var (diagnostics, _, output) = Run(Source());
        Assert.Empty(diagnostics);
        await database.Context.MigrateAsync(GeneratedModel.Of(output).Contributions);
        var key = Assert.Single(await database.ForeignKeysAsync(ChildTable));
        Assert.Equal("fk_quoted_endpoints", key.Name);
        Assert.Equal(ParentTable, key.ReferencedTable);
        Assert.Equal([ParentColumn], key.ReferencedColumns);
        Assert.Equal([ChildColumn], key.Columns);
        var parent = Guid.NewGuid();
        await database.ExecuteAsync($"INSERT INTO {Quote(ParentTable)}({Quote(ParentColumn)}) VALUES (@p0)", parent);
        var insert = $"INSERT INTO {Quote(ChildTable)}(id,{Quote(ChildColumn)}) VALUES (@p0,@p1)";
        Assert.Equal(1, await database.ExecuteAsync(insert, Guid.NewGuid(), parent));
        await Assert.ThrowsAnyAsync<DbException>(() => database.ExecuteAsync(insert, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(42L, Convert.ToInt64(await database.ScalarAsync("SELECT id FROM sentinel")));
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    private static string Source() => $$"""
        using Neemle.OrmLite.Nodes;
        [Table({{Literal(ParentTable)}})] public class HostileParent {
            [Column({{Literal(ParentColumn)}})] public Guid Id { get; set; }
        }
        [Table({{Literal(ChildTable)}})] public class HostileChild {
            public Guid Id { get; set; }
            [Column({{Literal(ChildColumn)}})] public Guid? HostileParentId { get; set; }
            [ForeignKey("fk_quoted_endpoints", ReferentialAction.Restrict)]
            public HostileParent? HostileParent { get; set; }
        }
        """;
}
