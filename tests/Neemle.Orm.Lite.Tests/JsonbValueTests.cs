using System;
using System.Data.Common;
using System.Text.Json;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class JsonbValueTests
{
    [Fact]
    public void Parse_keeps_the_exact_text_it_was_given()
    {
        const string document = "{\"b\":1,\"a\":[2,3],\"n\":null}";

        Assert.Equal(document, Jsonb.Parse(document).Json);
        Assert.Equal(document, Jsonb.Parse(document).ToString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("\"a string is a JSON document\"")]
    [InlineData("{\"nested\":{\"deep\":[1,2,{\"x\":true}]}}")]
    public void Parse_accepts_every_JSON_document_and_not_only_an_object(string document) =>
        Assert.Equal(document, Jsonb.Parse(document).Json);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("{\"a\":}")]
    [InlineData("{'a':1}")]
    [InlineData("not json at all")]
    [InlineData("{} trailing")]
    public void Parse_REFUSES_text_that_is_not_a_JSON_document(string notJson)
    {
        Assert.ThrowsAny<JsonException>(() => Jsonb.Parse(notJson));
        Assert.False(Jsonb.TryParse(notJson, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Parse_refuses_null_by_naming_the_argument_rather_than_by_NullReferenceException() =>
        Assert.Throws<ArgumentNullException>(() => Jsonb.Parse(null!));

    [Fact]
    public void TryParse_reports_success_without_throwing()
    {
        Assert.True(Jsonb.TryParse("{\"a\":1}", out var value));
        Assert.NotNull(value);
        Assert.Equal("{\"a\":1}", value!.Json);
    }

    [Fact]
    public void FromStorage_does_NOT_re_parse_because_the_server_already_validated_the_column()
    {
        var fromStorage = Jsonb.FromStorage("this was never valid JSON");

        Assert.Equal("this was never valid JSON", fromStorage.Json);
        Assert.ThrowsAny<JsonException>(() => Jsonb.Parse("this was never valid JSON"));
    }

    [Fact]
    public void Two_documents_with_the_same_text_are_equal_and_hash_alike()
    {
        var left = Jsonb.Parse("{\"a\":1}");
        var right = Jsonb.Parse("{\"a\":1}");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, Jsonb.Parse("{\"a\":2}"));
        Assert.False(left.Equals(null));
    }

    [Fact]
    public void Equality_is_ORDINAL_text_equality_and_is_NOT_document_equivalence()
    {
        Assert.NotEqual(Jsonb.Parse("{\"a\":1,\"b\":2}"), Jsonb.Parse("{\"b\":2,\"a\":1}"));
        Assert.NotEqual(Jsonb.Parse("{\"a\":1}"), Jsonb.Parse("{ \"a\" : 1 }"));
    }

    [Fact]
    public async Task A_document_written_through_the_ORM_bind_reads_back_through_FromStorage()
    {
        const string document = "{\"tab\":\"details\",\"order\":3,\"labels\":[\"a\",\"b\"]}";

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_docs (id TEXT PRIMARY KEY, payload TEXT)";
            await create.ExecuteNonQueryAsync();
        }

        var written = Jsonb.Parse(document);

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO fixture_docs (id, payload) VALUES (@p0, @p1)";
            DbExtensions.AddParam(insert, "p0", "row-1");
            DbExtensions.AddParam(insert, "p1", (object?)written?.Json ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT payload FROM fixture_docs WHERE id = @k";
        DbExtensions.AddParam(read, "k", "row-1");

        await using var reader = await read.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var roundTripped = Jsonb.FromStorage(reader.GetString(0));

        Assert.Equal(written, roundTripped);
        Assert.Equal(document, roundTripped.Json);
    }

    [Fact]
    public async Task A_Jsonb_handed_to_AddParam_unwrapped_binds_as_its_document_text()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        var parameter = DbExtensions.AddParam(cmd, "p0", Jsonb.Parse("{\"a\":1}"));

        Assert.Equal("{\"a\":1}", parameter.Value);
        Assert.IsType<string>(parameter.Value);
    }

    [Fact]
    public async Task A_null_document_binds_as_DBNull_rather_than_throwing_inside_the_ORM()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE fixture_docs (id TEXT PRIMARY KEY, payload TEXT)";
            await create.ExecuteNonQueryAsync();
        }

        Jsonb? absent = null;

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO fixture_docs (id, payload) VALUES (@p0, @p1)";
            DbExtensions.AddParam(insert, "p0", "row-1");
            DbExtensions.AddParam(insert, "p1", (object?)absent?.Json ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT payload FROM fixture_docs WHERE id = @k";
        DbExtensions.AddParam(read, "k", "row-1");

        await using var reader = await read.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(await reader.IsDBNullAsync(0));
    }
}
