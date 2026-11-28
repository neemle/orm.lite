using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

public sealed class OrphanCalculatedCopyTests
{
    [Fact(DisplayName = "[UC-007][AC-113-01] frozen orphan metadata copies a calculated Lower field whose source property and column differ")]
    public void Calculated_source_column_is_mapped_back_to_its_property()
    {
        var descriptor = EntityDescriptor.Of("CalculatedOwner", "orep_calculated", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("DisplayName", "display_name", ColumnType.Text, false, false)
            .CalculatedLower("Lower", "lower\"label", "DisplayName");
        var metadata = new OrphanMetadata(descriptor);
        var field = Assert.Single(metadata.Copy().Calculated);
        Assert.Equal(("Lower", "lower\"label", "display_name"), (field.Property, field.Alias, field.SourceColumn));
        Assert.True(OrphanDescriptorEquality.Same(metadata.Copy(), descriptor));
    }
}
