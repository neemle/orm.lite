using Xunit;

namespace Neemle.OrmLite.Tests.Diagnostics;

public sealed class ParameterRedactionEvasionTests
{
    [Theory(DisplayName = "AC1 [SEC] a key or a value written with JSON escapes is judged by its decoded text")]
    [InlineData("""{"\u0070assword":"x"}""")]
    [InlineData("""{"pass\u0077ord":"x"}""")]
    [InlineData("""{"to\u006ben":"x"}""")]
    [InlineData("""{"\u0050ASSWORD":"x"}""")]
    [InlineData("""{"password\n":"x"}""")]
    [InlineData("""{"password\t":"x"}""")]
    [InlineData("""{"v":"\u0065yJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln"}""")]
    [InlineData("""{"h":"\u0024argon2id\u0024v=19"}""")]
    public void An_escaped_secret_is_redacted(string json) => Assert.True(ParameterRedactor.IsSecretShaped(json));

    [Fact(DisplayName = "AC3 [SEC] a base64 token whose plus or slash System.Text.Json wrote as an escape is still " +
        "judged token-shaped")]
    public void An_escaped_base64_token_is_redacted()
    {
        var token = new string('a', 20) + "1234567890123456";
        Assert.True(ParameterRedactor.IsSecretShaped("{\"d\":\"" + token + "+abcd\"}"));
        Assert.True(ParameterRedactor.IsSecretShaped("{\"d\":\"" + token + "\\u002Babcd\"}"));
        Assert.True(ParameterRedactor.IsSecretShaped("{\"d\":\"" + token + "\\u002b\\/abcd\"}"));
        Assert.True(ParameterRedactor.IsSecretShaped("{\"d\":\"" + token + "\\/abcd\"}"));
        Assert.False(ParameterRedactor.IsSecretShaped("{\"d\":\"abc\\u002Bdef\"}"));
    }

    [Theory(DisplayName = "AC7 broken or truncated JSON escapes are kept as they are and never throw")]
    [InlineData("{\"a\":\"\\u00")]
    [InlineData("{\"a\":\"\\uZZZZ\"}")]
    [InlineData("{\"a\":\"\\x\"}")]
    [InlineData("{\"a\":\"\\")]
    [InlineData("\\")]
    [InlineData("\\u")]
    [InlineData("{\"a\":\"\\ud800\"}")]
    public void A_broken_escape_is_judged_without_throwing(string text) =>
        Assert.False(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC6 a benign escaped document is not redacted")]
    [InlineData("""{"path":"C:\\users\\x"}""")]
    [InlineData("""{"t":"line\nbreak","q":"say \"hi\""}""")]
    [InlineData("""{"s":"a\/b"}""")]
    public void A_benign_escaped_document_is_kept(string json) =>
        Assert.False(ParameterRedactor.IsSecretShaped(json));

    [Theory(DisplayName = "AC2 [SEC] whitespace, closing quotes and brackets, an encoded separator or a tag between " +
        "the label and the separator do not hide it")]
    [InlineData("""{"password ":"x"}""")]
    [InlineData("""{"password   ":"x"}""")]
    [InlineData("user[password]=x")]
    [InlineData("$_POST['password']='x'")]
    [InlineData("(\"token\") => 'x'")]
    [InlineData("<password>x</password>")]
    [InlineData("password%3Dx")]
    [InlineData("password%3dx")]
    [InlineData("token%3Ax")]
    [InlineData("token%3ax")]
    [InlineData("['password']")]
    [InlineData("[\"token\"]")]
    [InlineData("`password`=x")]
    [InlineData("password ) : x")]
    public void A_wrapped_label_is_redacted(string text) =>
        Assert.True(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC6 text with arrows, tags, brackets or encoded separators that name no secret is kept")]
    [InlineData("a -> b")]
    [InlineData("Author: Jane")]
    [InlineData("x[1]=2")]
    [InlineData("5%3D5")]
    [InlineData("<p>hello</p>")]
    [InlineData("a => b")]
    [InlineData("[ok]")]
    public void Unrelated_punctuation_is_kept(string text) =>
        Assert.False(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC2 [SEC] a two-word label is judged as one folded word")]
    [InlineData("api key: x")]
    [InlineData("API Key = x")]
    [InlineData("secret key: x")]
    [InlineData("private key=x")]
    [InlineData("access key:x")]
    [InlineData("pass word:x")]
    [InlineData("pass phrase: x")]
    [InlineData("""{"api key":"x"}""")]
    public void A_two_word_label_is_redacted(string text) =>
        Assert.True(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC6 a two-word label that names no secret is kept")]
    [InlineData("sort key: 3")]
    [InlineData("a key: x")]
    [InlineData("primary key=1")]
    [InlineData("the api: x")]
    [InlineData("foreign key = users")]
    public void A_two_word_label_without_a_secret_is_kept(string text) =>
        Assert.False(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC1 [SEC] a short secret word is redacted only when it is the whole label or its last word")]
    [InlineData("""{"pass":"x"}""")]
    [InlineData("""{"pw":"x"}""")]
    [InlineData("""{"psw":"x"}""")]
    [InlineData("""{"pin":"1234"}""")]
    [InlineData("""{"PIN":"1234"}""")]
    [InlineData("""{"otp":"123456"}""")]
    [InlineData("""{"auth":"x"}""")]
    [InlineData("""{"jwt":"x"}""")]
    [InlineData("""{"bearer":"x"}""")]
    [InlineData("""{"auth":{"pass":"p"}}""")]
    [InlineData("PIN: 1234")]
    [InlineData("user_pin=1")]
    [InlineData("one-time pin: 1")]
    public void A_short_secret_word_is_redacted_as_a_whole_word(string text) =>
        Assert.True(ParameterRedactor.IsSecretShaped(text));

    [Theory(DisplayName = "AC6 a label that merely contains a short secret word is kept")]
    [InlineData("""{"author":"x"}""")]
    [InlineData("""{"passenger":"x"}""")]
    [InlineData("""{"pinned":true}""")]
    [InlineData("""{"compass":"x"}""")]
    [InlineData("""{"bypass":"x"}""")]
    [InlineData("""{"pinball":1}""")]
    [InlineData("Passenger: Jane")]
    [InlineData("Compass=north")]
    [InlineData("authority: none")]
    public void A_label_containing_a_short_word_is_kept(string text) =>
        Assert.False(ParameterRedactor.IsSecretShaped(text));
}
