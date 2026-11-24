using System.Text;

namespace Neemle.OrmLite.Sql;

public sealed class SqlWriter
{
    private static readonly string[] Words = BuildWords();

    private readonly StringBuilder _text = new();
    private bool _spaceNext;

    public SqlWriter Kw(Kw keyword) => Emit(Words[(int)keyword], true, true);

    public SqlWriter KwCall(Kw keyword) => Emit(Words[(int)keyword], true, false).Emit('(', false, false);

    public SqlWriter Atom(string atom) => Emit(atom, true, true);

    public SqlWriter Func(string atom) => Emit(atom, true, false).Emit('(', false, false);

    public SqlWriter Attach(string token) => Emit(token, false, false);

    public SqlWriter Ident(string name) => Emit(Quoted(name), true, true);

    public SqlWriter Bare(string name) => Emit(name, true, true);

    public SqlWriter Param(string prefix, string name) => Emit(prefix + name, true, true);

    public SqlWriter Int(long value) => Emit(value.ToString(System.Globalization.CultureInfo.InvariantCulture), true, true);

    public SqlWriter Str(string value) => Emit("'" + value.Replace("'", "''") + "'", true, true);

    public SqlWriter Open() => Emit('(', true, false);

    public SqlWriter CallOpen() => Emit('(', false, false);

    public SqlWriter Close() => Emit(')', false, true);

    public SqlWriter OpenBracket() => Emit('[', false, false);

    public SqlWriter CloseBracket() => Emit(']', false, true);

    public SqlWriter Semicolon() => Emit(';', false, true);

    public SqlWriter Blank() => Emit(" ", false, false);

    public SqlWriter Comma() => Emit(',', false, true);

    public SqlWriter Dot() => Emit('.', false, false);

    public override string ToString() => _text.ToString();

    private SqlWriter Emit(char token, bool spaceBefore, bool spaceAfter) =>
        Emit(token.ToString(), spaceBefore, spaceAfter);

    private SqlWriter Emit(string token, bool spaceBefore, bool spaceAfter)
    {
        if (_spaceNext && spaceBefore) _text.Append(' ');
        _text.Append(token);
        _spaceNext = spaceAfter;
        return this;
    }

    private static string Quoted(string identifier) =>
        '"' + identifier.Replace("\"", "\"\"") + '"';

    private static string[] BuildWords()
    {
        var names = Enum.GetNames<Kw>();
        var words = new string[names.Length];
        for (var i = 0; i < names.Length; i++) words[i] = Spell(names[i]);
        return words;
    }

    private static string Spell(string name)
    {
        var sb = new StringBuilder(name.Length + 2);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1])) sb.Append('_');
            sb.Append(char.ToUpperInvariant(name[i]));
        }
        return sb.ToString();
    }
}
