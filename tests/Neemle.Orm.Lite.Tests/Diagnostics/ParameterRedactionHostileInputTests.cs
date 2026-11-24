using Xunit;

namespace Neemle.Orm.Lite.Tests.Diagnostics;

public sealed class ParameterRedactionHostileInputTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

    private static async Task<T> Bounded<T>(Func<T> work)
    {
        var task = Task.Run(work);
        var finished = await Task.WhenAny(task, Task.Delay(Guard));
        Assert.True(ReferenceEquals(finished, task), "the redaction did not finish inside the guard");
        return await task;
    }

    [Fact(DisplayName = "AC7 [SEC] two million characters of label-length runs and assignments finish and are kept")]
    public async Task Long_runs_of_label_length_words_finish()
    {
        var text = string.Concat(Enumerable.Repeat(new string('a', 63) + "=", 31_250));
        Assert.Equal(2_000_000, text.Length);
        Assert.False(await Bounded(() => ParameterRedactor.IsSecretShaped(text)));
    }

    [Fact(DisplayName = "AC7 [SEC] two million characters of long whitespace runs before separators finish and " +
        "are kept")]
    public async Task Long_whitespace_runs_before_separators_finish()
    {
        var text = string.Concat(Enumerable.Repeat(new string(' ', 1000) + ":", 1998));
        Assert.False(await Bounded(() => ParameterRedactor.IsSecretShaped(text)));
    }

    [Fact(DisplayName = "AC7 [SEC] a hundred thousand nested brackets finish whether or not a label closes them")]
    public async Task Deeply_nested_brackets_finish()
    {
        var open = new string('[', 100_000);
        Assert.False(await Bounded(() => ParameterRedactor.IsSecretShaped(open)));
        var closed = open + "x" + new string(']', 100_000);
        Assert.False(await Bounded(() => ParameterRedactor.IsSecretShaped(closed)));
    }

    [Fact(DisplayName = "AC7 [SEC] one two-million-character JSON literal is judged, token-shaped or not")]
    public async Task One_huge_JSON_literal_is_judged()
    {
        var plain = "{\"a\":\"" + new string('a', 2_000_000) + "\"}";
        var token = "{\"a\":\"" + new string('a', 1_999_999) + "1\"}";
        Assert.False(await Bounded(() => ParameterRedactor.IsSecretShaped(plain)));
        Assert.True(await Bounded(() => ParameterRedactor.IsSecretShaped(token)));
    }

    [Fact(DisplayName = "AC4 [SEC] a five-million-character char array ending in a secret is redacted")]
    public async Task A_huge_char_array_with_a_secret_at_the_end_is_redacted()
    {
        var chars = new char[5_000_000];
        Array.Fill(chars, 'a');
        "password=x".CopyTo(0, chars, chars.Length - 10, 10);
        Assert.Equal(ParameterRedactor.Redacted, await Bounded(() => ParameterRedactor.Describe(chars, 64)));
    }

    [Fact(DisplayName = "AC7 NUL, astral and unpaired surrogate characters are judged without throwing")]
    public void Unusual_characters_do_not_throw()
    {
        Assert.Equal(ParameterRedactor.Redacted, ParameterRedactor.Describe("\0password=\0", 64));
        Assert.Equal(ParameterRedactor.Redacted, ParameterRedactor.Describe("\U0001F600 password=\U0001F600", 64));
        Assert.Equal(ParameterRedactor.Redacted, ParameterRedactor.Describe("password:\uD800", 64));
        Assert.Equal("a\0b\U0001F600", ParameterRedactor.Describe("a\0b\U0001F600", 64));
        Assert.Equal("{\"a\":\"\0\"}", ParameterRedactor.Describe("{\"a\":\"\0\"}", 64));
        Assert.Equal("\uDC00x", ParameterRedactor.Describe("\uDC00x", 64));
    }
}
