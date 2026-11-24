using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Nodes;
namespace CorpusEntities;

[Migration("0100_reversible_with_down", Reversibility.Reversible)]
public sealed class ReversibleWithDown : Migration
{
    public override void Up(MigrationBuilder b) => b
        .CreateTable("ce_extra", t => t.Column("id", ColumnType.Uuid, false, true).JsonbColumn("doc"))
        .AddColumn("ce_extra", "note", ColumnType.Text)
        .AddJsonbColumn("ce_extra", "more", false)
        .CreateIndex("ix_extra_note", "ce_extra", "note")
        .CreateUniqueIndex("uq_extra_note", "ce_extra", "note");

    public override void Down(MigrationBuilder b) => b
        .DropIndex("uq_extra_note")
        .DropIndex("ix_extra_note")
        .DropTable("ce_extra");
}

[Migration("0200_reversible_without_down", Reversibility.Reversible)]
public sealed class ReversibleWithoutDown : Migration
{
    public override void Up(MigrationBuilder b) => b
        .RenameColumn("ce_extra", "note", "remark")
        .AddForeignKey("fk_extra_owner", "ce_extra", "owner_id", "ce_accounts", "id")
        .InsertData("ce_labels", ["id", "text"], ["00000000-0000-0000-0000-0000000000aa", "seed"], ["00000000-0000-0000-0000-0000000000bb", "it's"])
        .DeleteData("ce_labels", "text", "stale", "older");
}

[Migration("0300_irreversible", Reversibility.Irreversible)]
public sealed class Irreversible : Migration
{
    public override void Up(MigrationBuilder b) => b
        .DropColumn("ce_extra", "remark")
        .Raw("postgres", "SELECT 'irreversible'");
}

[Migration("0400_helper_blocks", Reversibility.Reversible)]
public sealed class HelperBlocks : Migration
{
    public override void Up(MigrationBuilder b) => b
        .Add(SoftDeleteColumnDdl.Up("ce_labels", "0400_helper_blocks"))
        .Add(LiveOnlyConversionDdl.Up("ce_projects", ["tenant_id", "lower(title)"], "deleted_at", "uq_projects_title", "ix_projects_title_live", true, "0400_helper_blocks"))
        .Add(ForeignKeyActionDdl.Up("ce_tasks", "fk_tasks_project", ["project_id"], "ce_projects", ["id"], ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction));

    public override void Down(MigrationBuilder b) => b
        .Add(ForeignKeyActionDdl.Down("ce_tasks", "fk_tasks_project", ["project_id"], "ce_projects", ["id"], ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction))
        .Add(LiveOnlyConversionDdl.Down("ce_projects", ["tenant_id", "lower(title)"], "deleted_at", "uq_projects_title", "ix_projects_title_live", true, "0400_helper_blocks"))
        .Add(SoftDeleteColumnDdl.Down("ce_labels", "0400_helper_blocks"));
}
