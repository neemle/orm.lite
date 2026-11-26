using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Corpus;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedMaskingTests
{
    private sealed class Note
    {
        public string? Title { get; set; }
        public string? Secret { get; set; }
        public string? Lower { get; set; }
    }

    [Fact(DisplayName = "[SEC][UC-001] masking an unrelated column keeps its mask and the allowed calculated value")]
    public void Other_column_masks_do_not_refuse_safe_calculations()
    {
        var descriptor = EntityDescriptor.Of("Note", "masked_notes", true)
            .Property("Title", "title", ColumnType.Text, true, false)
            .Property("Secret", "secret", ColumnType.Text, true, false)
            .CalculatedLower("Lower", "lower", "Title");
        OrmMeta.Register<Note>(descriptor);
        using var source = new RecordingDataSource(new RecordingLog(), "primary");
        var db = new OrmContext(source, PostgresProvider.Instance);
        var sql = new Query<Note>(db, _ => throw new NotSupportedException()).Redact(n => n.Secret).RenderSelect();
        Assert.Contains("NULL AS \"secret\"", sql);
        Assert.Contains("lower(\"title\") AS \"lower\"", sql);
    }

    [Fact(DisplayName = "[UC-001] legacy linked statements preserve target qualification and live-row filtering")]
    public void Legacy_linked_statement_keeps_the_soft_target_filter()
    {
        var owner = EntityDescriptor.Of("Owner", "owners", true);
        var link = new ManyToManyDescriptor("Notes", "owner_notes", "owner_id", "note_id",
            "notes", "id", "deleted_at", true);
        var sql = GeneratedModel.Text(EntityStatements.LoadLinked(owner, link, Guid.Empty));
        Assert.Contains("SELECT o.*", sql);
        Assert.Contains("o.\"deleted_at\" IS NULL", sql);
    }
}
