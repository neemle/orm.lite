using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.ForeignKeys.ForeignKeyPlans;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class ForeignKeyRefusals
{
    [Theory(DisplayName = "[SEC][UC-003] invalid names refuse without diagnostic data/control disclosure")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("fk\0payload")]
    [InlineData("fk\npassword=private-row")]
    public void Invalid_names_have_sanitized_errors(string name)
    {
        var child = Child().ForeignKey(Key(name));
        var error = Assert.Throws<ForeignKeyDeclarationException>(
            () => OrmSchema.Of("Bad", child, Parent()).Baseline());
        Assert.Equal(ForeignKeyDeclarationError.InvalidName, error.Error);
        Assert.DoesNotContain("private-row", error.Message);
        Assert.DoesNotContain('\0', error.Message);
    }

    [Theory(DisplayName = "[UC-003] invalid action, nullability and column/key shapes are refused before planning")]
    [InlineData("action", ForeignKeyDeclarationError.InvalidDeleteAction)]
    [InlineData("nullable", ForeignKeyDeclarationError.NonNullableSetNull)]
    [InlineData("source", ForeignKeyDeclarationError.UnmappedColumn)]
    [InlineData("key", ForeignKeyDeclarationError.UnsupportedKey)]
    [InlineData("type", ForeignKeyDeclarationError.IncompatibleKey)]
    [InlineData("external", ForeignKeyDeclarationError.ExternalBaseline)]
    [InlineData("unmanaged", ForeignKeyDeclarationError.UnmanagedEndpoint)]
    public void Invalid_physical_metadata_has_specific_categories(string mode, ForeignKeyDeclarationError expected)
    {
        var parent = mode == "unmanaged" ? EntityDescriptor.Of("Parent", "parents", false)
            .Property("Id", "id", ColumnType.Uuid, false, true) : Parent();
        var key = mode switch
        {
            "action" => Key(action: (ReferentialAction)99),
            "nullable" => Key(action: ReferentialAction.SetNull),
            "source" => Key() with { LocalColumn = "missing" },
            "key" => Key() with { Target = new("parents", "other") },
            "external" => Key() with { Target = new("external", "id") },
            _ => Key(),
        };
        var child = mode == "type" ? EntityDescriptor.Of("Child", "children", true)
            .Property("ParentId", "parent_id", ColumnType.Int32, false, false) : Child();
        var error = Assert.Throws<ForeignKeyDeclarationException>(
            () => OrmSchema.Of("Invalid", child.ForeignKey(key), parent).Baseline());
        Assert.Equal(expected, error.Error);
    }

    [Theory(DisplayName = "[SEC][UC-003] self references and directed dependency cycles are refused as a complete set")]
    [InlineData(true)]
    [InlineData(false)]
    public void Cycles_never_publish_partial_schema(bool self)
    {
        var parent = Parent().Property("NextId", "next_id", ColumnType.Uuid, true, false)
            .ForeignKey(new("fk_parent_next", "next_id", new(self ? "parents" : "children", "id"),
                ReferentialAction.NoAction));
        var error = Assert.Throws<ForeignKeyDeclarationException>(
            () => OrmSchema.Of("Cycle", parent, Child().ForeignKey(Key())).Baseline());
        Assert.Equal(ForeignKeyDeclarationError.DependencyCycle, error.Error);
    }
}
