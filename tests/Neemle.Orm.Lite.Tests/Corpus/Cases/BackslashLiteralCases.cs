namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class BackslashLiteralCases
{
    public static void Register(CorpusRun run)
    {
        run.Rec.Statements("ddl-builder", "insert-data-backslash-and-quote-text", () => new MigrationBuilder()
            .InsertData(
                "samples",
                ["a", "b", "c"],
                [@"a\b", @"x\'); DROP TABLE t; --", '\\'],
                ["plain", @"\", @"{""k"":""a\""b""}"])
            .Operations);
        run.Rec.Statements("ddl-builder", "delete-data-backslash-text", () => new MigrationBuilder()
            .DeleteData("samples", "k", @"a\b", "plain", @"end\")
            .Operations);
        run.Rec.Refusal("refusals", "ddl-literal-string-with-nul",
            () => new MigrationBuilder().InsertData("t", ["c"], ["a\0b"]));
        run.Rec.Refusal("refusals", "ddl-literal-string-with-a-lone-surrogate",
            () => new MigrationBuilder().InsertData("t", ["c"], ["a\uD83Db"]));
    }
}
