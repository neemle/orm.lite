using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Diagnostics;

public sealed class ParameterRedactionTests
{
    private const string Jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln";

    [Theory(DisplayName = "AC1 [SEC] a JSON document that names a secret key is redacted whatever the key is " +
        "spelled like")]
    [InlineData("""{"password":"hunter2"}""")]
    [InlineData("""{"token":"x"}""")]
    [InlineData("""{"api_key":"abc"}""")]
    [InlineData("""{"apiKey":"abc"}""")]
    [InlineData("""{"x-api-key":"abc"}""")]
    [InlineData("""{"clientSecret":"s"}""")]
    [InlineData("""{"refresh_token":"r"}""")]
    [InlineData("""{"private-key":"k"}""")]
    [InlineData("""{"authorization":"Zm9vOmJhcg"}""")]
    [InlineData("""{"user":"u","db":{"Password" : "p"}}""")]
    [InlineData("""{"a":"x\"y","passwd":"p"}""")]
    [InlineData("""[{"id":1},{"credentials":{"user":"u"}}]""")]
    public void A_secret_key_in_a_JSON_document_is_redacted(string json)
    {
        Assert.True(ParameterRedactor.IsSecretShaped(json));
        Assert.Equal(ParameterRedactor.Redacted, ParameterRedactor.Describe(json, 4096));
    }

    [Theory(DisplayName = "AC2 [SEC] header-style and assignment-style secrets are redacted, the old SecretKeys " +
        "list included")]
    [InlineData("authorization: Zm9vOmJhcg==")]
    [InlineData("Authorization: Token abc")]
    [InlineData("proxy-authorization:abc")]
    [InlineData("X-Api-Key: abc")]
    [InlineData("Cookie: sid=1")]
    [InlineData("Set-Cookie: a=b")]
    [InlineData("password : x")]
    [InlineData("PWD=x")]
    [InlineData("client_secret=x")]
    [InlineData("'token' => 'x'")]
    [InlineData("apikey=x")]
    [InlineData("api-key=x")]
    [InlineData("api_key=x")]
    [InlineData("SECRET=x")]
    [InlineData("pwd=x")]
    [InlineData("password=x")]
    [InlineData("token=x")]
    public void A_header_or_assignment_secret_is_redacted(string text) =>
        Assert.True(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC3 [SEC] a JSON document carrying a secret-shaped value under an innocent key is redacted")]
    [InlineData("""{"jwt":"eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln"}""")]
    [InlineData("""{"u":"postgres://user:pw@host/db"}""")]
    [InlineData("""{"h":"$2b$12$abcdefghijklmnopqrstuv"}""")]
    [InlineData("""{"k":"3f2a9c1de4b84a7e9f0a1b2c3d4e5f60"}""")]
    [InlineData("""["Bearer abc"]""")]
    [InlineData("""{"pem":"-----BEGIN PRIVATE KEY-----"}""")]
    [InlineData("\"" + Jwt + "\"")]
    [InlineData("""{"p":"Basic dXNlcjpwYXNz"}""")]
    [InlineData("""{"h":"$argon2id$v=19$m=65536"}""")]
    [InlineData("""{"h":"$pbkdf2-sha256$i=1000"}""")]
    [InlineData("""{"h":"$scrypt$ln=15,r=8,p=1"}""")]
    [InlineData("""{"d":"eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln"}""")]
    public void A_secret_shaped_value_in_a_JSON_document_is_redacted(string json) =>
        Assert.True(ParameterRedactor.IsSecretShaped(json));

    [Fact(DisplayName = "AC3 [SEC] an escaped quote before a secret literal does not shift the literals, and a " +
        "leading space does not hide the document, while text that is not JSON keeps its quoted token")]
    public void The_literal_scan_follows_escapes_and_the_leading_whitespace()
    {
        Assert.True(ParameterRedactor.IsSecretShaped("{\"a\":\"x\\\"y\",\"d\":\"" + Jwt + "\"}"));
        Assert.True(ParameterRedactor.IsSecretShaped("  \n{\"a\":\"" + Jwt + "\"}"));
        Assert.True(ParameterRedactor.IsSecretShaped("﻿{\"a\":\"" + Jwt + "\"}"));
        Assert.False(ParameterRedactor.IsSecretShaped("He said \"" + Jwt + "\" loudly"));
    }

    [Theory(DisplayName = "AC7 [SEC] truncated JSON that still carries a secret stays redacted")]
    [InlineData("""{"password":"hun""")]
    [InlineData("""{"a":"eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln unterminated""")]
    [InlineData("""[{"token":""")]
    public void Truncated_JSON_with_a_secret_is_redacted(string text) =>
        Assert.True(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC6 ordinary JSON and ordinary labelled text are not redacted")]
    [InlineData("""{"title":"hello","stars":3}""")]
    [InlineData("""{"kind":"red"}""")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"note":"Quarterly report"}""")]
    [InlineData("""{"email":"a@b.c","time":"10:30"}""")]
    [InlineData("""{"author":"Jane"}""")]
    [InlineData("""{"url":"https://example.com/a?b=c"}""")]
    [InlineData("Author: Jane")]
    [InlineData("ratio=3")]
    [InlineData("a -> b")]
    [InlineData("<b>bold</b>")]
    [InlineData("x[1]=2")]
    public void Ordinary_text_is_kept(string text) =>
        Assert.False(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC7 malformed or truncated JSON is judged without throwing")]
    [InlineData("{\"")]
    [InlineData("{\"\\")]
    [InlineData("\"")]
    [InlineData("{\"a\":\"b\\")]
    [InlineData("{\"a\":\"unterminated")]
    [InlineData("[\"a\",")]
    [InlineData("{\"password")]
    public void Malformed_JSON_is_judged_without_throwing(string text) =>
        Assert.False(ParameterRedactor.IsSecretShaped(text));

    [Fact(DisplayName = "AC4 [SEC] text held in a char array or another char container is converted and then checked")]
    public void Char_containers_are_converted_before_the_check()
    {
        var secret = "password=hunter2";
        Assert.Equal(ParameterRedactor.Redacted, Shown(secret.ToCharArray()));
        Assert.Equal(ParameterRedactor.Redacted, Shown(new ArraySegment<char>(secret.ToCharArray())));
        Assert.Equal(ParameterRedactor.Redacted, Shown(secret.AsMemory()));
        Assert.Equal(ParameterRedactor.Redacted, Shown(new Memory<char>(secret.ToCharArray())));
        Assert.Equal(ParameterRedactor.Redacted, Shown(new StringBuilder(secret)));
        Assert.Equal("hello", Shown("hello".ToCharArray()));
        Assert.Equal("hel...", ParameterRedactor.Describe(new StringBuilder("hello"), 3));
    }

    [Fact(DisplayName = "AC4 [SEC] a JSON value object and any other object are converted to their text and " +
        "then checked")]
    public void Other_objects_are_converted_before_the_check()
    {
        var json = "{\"password\":\"hunter2\"}";
        using var document = JsonDocument.Parse(json);
        Assert.Equal(ParameterRedactor.Redacted, Shown(document.RootElement));
        Assert.Equal(ParameterRedactor.Redacted, Shown(JsonNode.Parse(json)!));
        Assert.Equal(ParameterRedactor.Redacted, Shown(new Holder("token=abc")));
        Assert.Equal(ParameterRedactor.Redacted, Shown(new Holder(json)));
        Assert.Equal(ParameterRedactor.Redacted, Shown(new Uri("https://user:pw@host/db")));
        Assert.Equal("plain", Shown(new Holder("plain")));
        Assert.DoesNotContain("hunter2", Shown(new[] { "password=hunter2" }), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC5 numbers, ids, dates and enum values are still reported as they were, as exact text")]
    public void Plain_values_are_reported_unchanged()
    {
        var id = "3f2a9c1d-e4b8-4a7e-9f0a-1b2c3d4e5f60";
        var digits = "1234567890123456789012345678901234567890";
        Assert.Equal(id, Shown(Guid.Parse(id)));
        Assert.Equal("Monday", Shown(DayOfWeek.Monday));
        Assert.Equal("42", Shown(42));
        Assert.Equal("1234.5", Shown(1234.5m));
        Assert.Equal(digits, Shown(BigInteger.Parse(digits)));
        Assert.Equal("9223372036854775807", Shown(long.MaxValue));
        Assert.Equal("1.5", Shown(1.5d));
        Assert.Equal("True", Shown(true));
        Assert.Equal("x", Shown('x'));
    }

    [Fact(DisplayName = "AC5 dates and times are reported in the invariant culture, as exact text")]
    public void Dates_are_reported_in_the_invariant_culture()
    {
        Assert.Equal("11/24/2026 10:00:00", Shown(new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("11/24/2026 10:00:00 +00:00", Shown(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero)));
        Assert.Equal("11/24/2026", Shown(new DateOnly(2026, 11, 24)));
        Assert.Equal("10:00", Shown(new TimeOnly(10, 0)));
        Assert.Equal("01:02:03", Shown(new TimeSpan(1, 2, 3)));
    }

    [Fact(DisplayName = "AC6 [SEC] the whole text is checked before it is cut, so a secret past the cut still " +
        "redacts the value")]
    public void A_secret_past_the_cut_is_still_redacted()
    {
        var text = new string('a', 100) + " {\"password\":\"x\"}";
        Assert.Equal(ParameterRedactor.Redacted, ParameterRedactor.Describe(text, 5));
        Assert.Equal("{\"kin...", ParameterRedactor.Describe("{\"kind\":\"red\"}", 5));
    }

    [Fact(DisplayName = "AC7 an object whose text throws propagates, a null text or an empty container reports " +
        "empty text")]
    public void Hostile_objects_never_report_text_they_did_not_produce()
    {
        Assert.Throws<InvalidOperationException>(() => ParameterRedactor.Describe(new Thrower(), 64));
        Assert.Equal("", Shown(new NullText()));
        Assert.Equal("", Shown(new NullFormattable()));
        Assert.Equal("", Shown(default(ArraySegment<char>)));
        Assert.Equal("", Shown(Array.Empty<char>()));
        Assert.Equal("", Shown(new StringBuilder()));
    }

    private static string Shown(object value) => ParameterRedactor.Describe(value, 64) ?? "";

    private sealed class Holder(string text)
    {
        public override string ToString() => text;
    }

    private sealed class Thrower
    {
        public override string ToString() => throw new InvalidOperationException("hostile");
    }

    private sealed class NullText
    {
        public override string? ToString() => null;
    }

    private sealed class NullFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? provider) => null!;
    }
}
