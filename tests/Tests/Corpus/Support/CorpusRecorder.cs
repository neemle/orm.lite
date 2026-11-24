using System.Text.RegularExpressions;

namespace Neemle.OrmLite.Tests.Corpus;

public sealed class CorpusRecorder
{
    private static readonly Regex Segment = new(@"\A[a-z0-9][a-z0-9.\-_]*\z", RegexOptions.CultureInvariant);

    private readonly List<CorpusEntry> _entries = [];
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public string CurrentGroup { get; private set; } = "";

    public IReadOnlyList<CorpusEntry> Entries => _entries;

    public void Text(string group, string name, Func<string> body) => Add(group, name, body);

    public void Statement(string group, string name, Func<(string Sql, IReadOnlyList<object?> Parameters)> body) =>
        Add(group, name, () =>
        {
            var (sql, parameters) = body();
            return CorpusFormat.Statement(sql, parameters);
        });

    public void Statements(string group, string name, Func<IEnumerable<string>> body) =>
        Add(group, name, () => CorpusFormat.Lines(body()));

    public void Refusal(string group, string name, Action body) =>
        Add(group, name, () =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                return CorpusFormat.Refusal(ex);
            }
            throw new InvalidOperationException("the corpus case " + group + "/" + name + " expected a refusal and none was thrown");
        });

    public void Async(string group, string name, Func<Task<string>> body) =>
        Add(group, name, () => body().GetAwaiter().GetResult());

    public void AsyncRefusal(string group, string name, Func<Task> body) =>
        Add(group, name, () =>
        {
            try
            {
                body().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return CorpusFormat.Refusal(ex);
            }
            throw new InvalidOperationException("the corpus case " + group + "/" + name + " expected a refusal and none was thrown");
        });

    private void Add(string group, string name, Func<string> body)
    {
        if (!Segment.IsMatch(group) || !Segment.IsMatch(name))
            throw new ArgumentException("a corpus group and name are lowercase path segments: " + group + "/" + name);
        if (!_keys.Add(group + "/" + name))
            throw new InvalidOperationException("duplicate corpus case " + group + "/" + name);
        CurrentGroup = group;
        string text;
        try
        {
            text = body();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("the corpus case " + group + "/" + name + " failed: " + ex.Message, ex);
        }
        _entries.Add(new CorpusEntry(group, name, text.Replace("\r\n", "\n")));
    }
}
