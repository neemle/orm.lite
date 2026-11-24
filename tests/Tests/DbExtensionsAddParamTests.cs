using System.Data.Common;
using Neemle.OrmLite.Tests.Corpus;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class DbExtensionsAddParamTests
{
    private static DbParameter Added(object? value)
    {
        var source = new RecordingDataSource(new RecordingLog(), "add-param");
        using var cmd = source.CreateCommand("SELECT 1");
        return cmd.AddParam("p", value);
    }

    [Fact(DisplayName = "AddParam binds a null as DBNull, so the driver sends SQL NULL")]
    public void A_null_binds_as_DBNull()
    {
        Assert.Equal(DBNull.Value, Added(null).Value);
    }

    [Fact(DisplayName = "AddParam binds an enum as its name, the form the text column stores")]
    public void An_enum_binds_as_its_name()
    {
        Assert.Equal("Active", Assert.IsType<string>(Added(CorpusStatus.Active).Value));
    }

    [Fact(DisplayName = "AddParam binds a Jsonb as its JSON text")]
    public void A_jsonb_binds_as_its_text()
    {
        Assert.Equal("{\"a\":1}", Assert.IsType<string>(Added(Jsonb.Parse("{\"a\":1}")).Value));
    }

    [Fact(DisplayName = "AddParam passes a string and a Guid to the driver unchanged and names the parameter")]
    public void A_string_and_a_guid_pass_through()
    {
        var id = Guid.Parse("a0000000-0000-0000-0000-00000000000a");

        Assert.Equal("text", Added("text").Value);
        Assert.Equal(id, Added(id).Value);
        Assert.Equal("p", Added("x").ParameterName);
    }
}
