using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedAliasTests
{
    [Fact(DisplayName = "[SEC][UC-001] quoted aliases remain read-only identifiers")]
    public void Quoted_alias_is_read_only_metadata()
    {
        var (diagnostics, _, output) = Run("""
            [Table("alias_notes")]
            [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
            [Index(nameof(Name))]
            public class Note
            {
                public Guid Id { get; set; }
                public string Name { get; set; } = "";
                public DateTime? DeletedAt { get; set; }
                [Column("lower\"label")]
                [CalculatedLower(nameof(Name))] public string Lower { get; set; } = "";
            }
            """);
        Assert.Empty(diagnostics);
        var descriptor = GeneratedModel.Of(output).Descriptor("Note");
        var field = Assert.Single(descriptor.Calculated);
        Assert.Equal("lower\"label", field.Alias);
        foreach (var provider in new ISqlProvider[] { PostgresProvider.Instance, SqliteProvider.Instance })
        {
            var sql = StatementCompiler.Compile(EntityStatements.Get(descriptor, Guid.NewGuid()),
                provider, StatementKind.Read).Text;
            Assert.Contains("lower(\"name\") AS \"lower\"\"label\"", sql);
        }
        Assert.DoesNotContain(descriptor.Columns, c => c.Property == "Lower");
        Assert.Equal(new[] { "name" }, Assert.Single(descriptor.Indexes).Columns);
        Assert.DoesNotContain(descriptor.ToSoftMeta()!.PropertyColumns!, pair => pair[0] == "Lower");
    }

    [Fact(DisplayName = "[UC-001] a text-backed enum is not a CLR text source for calculated Lower")]
    public void Enum_source_is_refused()
    {
        var (diagnostics, _, _) = Run("""
            public enum Kind { First }
            [Table("notes")] public class Note
            {
                public Guid Id { get; set; }
                public Kind Value { get; set; }
                [CalculatedLower(nameof(Value))] public string Lower { get; set; } = "";
            }
            """);
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE023"
            && d.GetMessage().Contains("physical text"));
    }
}
