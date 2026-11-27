using System.Data.Common;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveOriginTests
{
    [Theory(DisplayName = "[UC-006][SEC-112-01/02] a known partial reference never becomes a full-value save")]
    [InlineData(false)] [InlineData(true)]
    public async Task Partial_wins_over_later_exact_full_mapper(bool fullFirst)
    {
        SaveProbeModel.Register();
        var root = SaveProbeModel.Node(71);
        Func<DbDataReader, SaveProbeNode> mapper = SaveProbeModel.MapNode;
        if (fullFirst) GraphSaveOrigins.Observe(root, mapper, false);
        GraphSaveOrigins.Observe(root, mapper, true);
        GraphSaveOrigins.Observe(root, mapper, false);
        await SavePreflightTests.Admission(new(root, GraphSaveIntent.Insert,
            new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room)),
            GraphSaveError.UnsupportedMapping);
    }
}
