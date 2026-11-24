using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Corpus.Cases;

internal static class DdlHelperCases
{
    private const string G = "ddl-helpers";

    public static void Register(CorpusRun run)
    {
        var rec = run.Rec;
        string[] one = ["owner_id"];
        string[] two = ["tenant_id", "owner_id"];
        string[] refOne = ["id"];
        string[] refTwo = ["tenant_id", "id"];

        rec.Text(G, "foreign-key-action-names-and-codes", () => CorpusFormat.Lines(
        [
            ForeignKeyActionDdl.Cascade + " -> " + PostgresCatalog.ForeignKeyCode(ForeignKeyActionDdl.Parse(ForeignKeyActionDdl.Cascade)),
            ForeignKeyActionDdl.SetNull + " -> " + PostgresCatalog.ForeignKeyCode(ForeignKeyActionDdl.Parse(ForeignKeyActionDdl.SetNull)),
            ForeignKeyActionDdl.NoAction + " -> " + PostgresCatalog.ForeignKeyCode(ForeignKeyActionDdl.Parse(ForeignKeyActionDdl.NoAction)),
            ForeignKeyActionDdl.Restrict + " -> " + PostgresCatalog.ForeignKeyCode(ForeignKeyActionDdl.Parse(ForeignKeyActionDdl.Restrict)),
        ]));
        rec.Text(G, "foreign-key-up-cascade", () => DdlText.Of(ForeignKeyActionDdl.Up("samples", "fk_samples_owner", one, "owners", refOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction)) + "\n");
        rec.Text(G, "foreign-key-down-cascade", () => DdlText.Of(ForeignKeyActionDdl.Down("samples", "fk_samples_owner", one, "owners", refOne, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction)) + "\n");
        rec.Text(G, "foreign-key-up-set-null-restrict", () => DdlText.Of(ForeignKeyActionDdl.Up("samples", "fk_samples_owner", one, "owners", refOne, ForeignKeyActionDdl.SetNull, ForeignKeyActionDdl.Restrict)) + "\n");
        rec.Text(G, "foreign-key-down-set-null-restrict", () => DdlText.Of(ForeignKeyActionDdl.Down("samples", "fk_samples_owner", one, "owners", refOne, ForeignKeyActionDdl.SetNull, ForeignKeyActionDdl.Restrict)) + "\n");
        rec.Text(G, "foreign-key-up-composite-with-restrict-on-update", () => DdlText.Of(ForeignKeyActionDdl.Up("samples", "fk_samples_scope", two, "owners", refTwo, ForeignKeyActionDdl.Cascade, ForeignKeyActionDdl.NoAction, ForeignKeyActionDdl.Restrict)) + "\n");
        rec.Text(G, "foreign-key-down-composite-with-restrict-on-update", () => DdlText.Of(ForeignKeyActionDdl.Down("samples", "fk_samples_scope", two, "owners", refTwo, ForeignKeyActionDdl.SetNull, ForeignKeyActionDdl.Restrict, ForeignKeyActionDdl.Restrict)) + "\n");
        rec.Refusal(G, "foreign-key-code-of-an-unknown-action", () => ForeignKeyActionDdl.Parse("SET DEFAULT"));
        rec.Refusal(G, "foreign-key-table-not-an-identifier", () => ForeignKeyActionDdl.Up("Samples", "fk", one, "owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-constraint-not-an-identifier", () => ForeignKeyActionDdl.Up("samples", "fk-bad", one, "owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-null-column-list", () => ForeignKeyActionDdl.Up("samples", "fk", null!, "owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-empty-column-list", () => ForeignKeyActionDdl.Up("samples", "fk", [], "owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-column-not-an-identifier", () => ForeignKeyActionDdl.Up("samples", "fk", ["a b"], "owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-referenced-table-not-an-identifier", () => ForeignKeyActionDdl.Down("samples", "fk", one, "1owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-column-count-mismatch", () => ForeignKeyActionDdl.Up("samples", "fk", two, "owners", refOne, "CASCADE", "NO ACTION"));
        rec.Refusal(G, "foreign-key-new-action-not-convertible", () => ForeignKeyActionDdl.Up("samples", "fk", one, "owners", refOne, "RESTRICT", "NO ACTION"));
        rec.Refusal(G, "foreign-key-old-action-not-restorable", () => ForeignKeyActionDdl.Down("samples", "fk", one, "owners", refOne, "CASCADE", "CASCADE"));
        rec.Refusal(G, "foreign-key-on-update-not-allowed", () => ForeignKeyActionDdl.Up("samples", "fk", one, "owners", refOne, "CASCADE", "NO ACTION", "CASCADE"));

        string[] plain = ["name"];
        string[] lowered = ["lower(name)", "scope"];
        rec.Text(G, "live-only-up-plain-key", () => DdlText.Of(LiveOnlyConversionDdl.Up("samples", plain, "deleted_at", "uq_samples_name", "ix_samples_name_live", true, "0200_live_only")) + "\n");
        rec.Text(G, "live-only-up-lowered-composite-key-undeclared-live-index", () => DdlText.Of(LiveOnlyConversionDdl.Up("samples", lowered, "deleted_at", "uq_samples_name", "ix_samples_name_live", false, "0200_live_only")) + "\n");
        rec.Text(G, "live-only-down-plain-key-declared-live-index", () => DdlText.Of(LiveOnlyConversionDdl.Down("samples", plain, "deleted_at", "uq_samples_name", "ix_samples_name_live", true, "0200_live_only")) + "\n");
        rec.Text(G, "live-only-down-lowered-composite-key-undeclared-live-index", () => DdlText.Of(LiveOnlyConversionDdl.Down("samples", lowered, "deleted_at", "uq_samples_name", "ix_samples_name_live", false, "0200_live_only")) + "\n");
        rec.Refusal(G, "live-only-table-not-an-identifier", () => LiveOnlyConversionDdl.Up("Samples", plain, "deleted_at", "a", "b", true, "v1"));
        rec.Refusal(G, "live-only-soft-column-not-an-identifier", () => LiveOnlyConversionDdl.Up("samples", plain, "Deleted", "a", "b", true, "v1"));
        rec.Refusal(G, "live-only-total-index-not-an-identifier", () => LiveOnlyConversionDdl.Down("samples", plain, "deleted_at", "A", "b", true, "v1"));
        rec.Refusal(G, "live-only-live-index-not-an-identifier", () => LiveOnlyConversionDdl.Down("samples", plain, "deleted_at", "a", "B", true, "v1"));
        rec.Refusal(G, "live-only-version-not-a-version-name", () => LiveOnlyConversionDdl.Up("samples", plain, "deleted_at", "a", "b", true, "V-1"));
        rec.Refusal(G, "live-only-null-key-terms", () => LiveOnlyConversionDdl.Up("samples", null!, "deleted_at", "a", "b", true, "v1"));
        rec.Refusal(G, "live-only-empty-key-terms", () => LiveOnlyConversionDdl.Up("samples", [], "deleted_at", "a", "b", true, "v1"));
        rec.Refusal(G, "live-only-key-term-not-a-term", () => LiveOnlyConversionDdl.Up("samples", ["upper(name)"], "deleted_at", "a", "b", true, "v1"));
        rec.Refusal(G, "live-only-null-key-term", () => LiveOnlyConversionDdl.Up("samples", [null!], "deleted_at", "a", "b", true, "v1"));

        rec.Text(G, "soft-delete-column-name", () => SoftDeleteColumnDdl.Column + "\n");
        rec.Text(G, "soft-delete-column-up", () => DdlText.Of(SoftDeleteColumnDdl.Up("samples", "0300_soft_column")) + "\n");
        rec.Text(G, "soft-delete-column-down", () => DdlText.Of(SoftDeleteColumnDdl.Down("samples", "0300_soft_column")) + "\n");
        rec.Refusal(G, "soft-delete-column-up-table-not-a-name", () => SoftDeleteColumnDdl.Up("Samples", "v1"));
        rec.Refusal(G, "soft-delete-column-down-version-not-a-name", () => SoftDeleteColumnDdl.Down("samples", "V 1"));
    }
}
