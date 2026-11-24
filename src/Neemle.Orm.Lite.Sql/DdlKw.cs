namespace Neemle.Orm.Lite.Sql;

public enum DdlKw
{
    Create,
    Table,
    If,
    Index,
    Unique,
    On,
    Drop,
    Alter,
    Add,
    Column,
    Constraint,
    Foreign,
    Primary,
    References,
    Default,
    Insert,
    Into,
    Values,
    Delete,
    Rename,
    To,
    Comment,
    Set,
    Action,
    Cascade,
    Restrict,
    View,
    Replace,
    Collate,
}

public static class DdlKwWriter
{
    private static readonly string[] Words = BuildWords();

    public static SqlWriter Ddl(this SqlWriter writer, DdlKw keyword) => writer.Atom(Words[(int)keyword]);

    private static string[] BuildWords()
    {
        var names = Enum.GetNames<DdlKw>();
        var words = new string[names.Length];
        for (var i = 0; i < names.Length; i++) words[i] = names[i].ToUpperInvariant();
        return words;
    }
}
