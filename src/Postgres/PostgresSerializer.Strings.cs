using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresSerializer
{
    public override void Visit(StringLit node)
    {
        if (!node.Value.Contains(PostgresDataLiterals.Backslash, StringComparison.Ordinal))
        {
            base.Visit(node);
            return;
        }
        W.Atom(PostgresDataLiterals.EscapeMarker + PostgresNames.Text(DoubledBackslashes(node.Value)).Text);
    }

    // The quote is doubled by the one routine that quotes a string literal (PostgresNames.Text); the backslash is
    // doubled here because the text is written as an escape string (E'...').
    private static string DoubledBackslashes(string value) => value
        .Replace(PostgresDataLiterals.Backslash, PostgresDataLiterals.DoubledBackslash, StringComparison.Ordinal);
}
