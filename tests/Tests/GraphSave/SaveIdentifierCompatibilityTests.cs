using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveIdentifierCompatibilityTests
{
    [Fact(DisplayName = "[UC-006][AC-112-02][SEC] an ordinary legal navigation name cannot make existing CRUD consumers uncompilable")]
    public void Existing_crud_consumer_with_save_relations_navigation_still_compiles()
    {
        var source = GeneratedSaveConsumerTests.Source;
        source = source[..source.IndexOf("public static class SaveConsumer", StringComparison.Ordinal)]
            .Replace("Children", "SaveRelations", StringComparison.Ordinal);
        source += "public static class ExistingCrudConsumer { public static Query<SaveParent> Read(OrmContext db) "
            + "=> SaveParentOrm.Query(db); }";
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(d => d.ToString())));
        GeneratedModel.Of(output).RunModuleInitializer();
    }
}
