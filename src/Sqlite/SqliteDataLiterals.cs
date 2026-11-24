namespace Neemle.OrmLite.Sqlite;

internal static class SqliteDataLiterals
{
    internal const string TimestampFormat = ParamLiterals.MomentFormat;

    internal const string ClockFormat = "%Y-%m-%dT%H:%M:%f";

    internal const string FractionTail = "0000Z";

    internal const string Now = "now";

    internal const string UnixEpoch = "unixepoch";

    internal const string SubSecond = "subsec";

    internal const string RebuildSuffix = "__orm_rebuild";

    internal const string MarkerTable = "orm_object_markers";

    internal const string MarkerKeyColumn = "object_key";

    internal const string MarkerValueColumn = "marker";

    internal const string MarkerSeparator = ":";

    internal const string ColumnKind = "column";

    internal const string ViewKeyword = "VIEW";

    internal const string TriggerKeyword = "TRIGGER";

    internal const string IndexKind = "index";

    internal const string ViewKind = "view";

    internal const string HistoryTriggerPrefix = "orm_history_";

    internal const string BoundPrimaryKey = "belongs to the primary key";

    internal const string BoundUnique = "belongs to a unique constraint";

    internal const string BoundForeignKey = "takes part in a foreign key";

    internal const string NoAction = "NO ACTION";

    internal const string SetNull = "SET NULL";

    internal const string ListSeparator = ", ";

    internal const string ExpressionTerm = "<expression>";

    internal const string SchemaTable = "sqlite_schema";

    internal const string TableType = "table";

    internal const string IndexType = "index";

    internal const string TriggerType = "trigger";

    internal const string ViewType = "view";

    internal const string ExplicitIndexOrigin = "c";

    internal const string IfNotExists = "IF NOT EXISTS ";

    internal const string Collations = "BINARY, NOCASE, RTRIM";

    internal const string CollationSeparator = ", ";

    internal const string WordSeparator = " ";

    internal const string CollationMessage = "SQLite index term uses collation '{0}', which provider 'sqlite' does not have; its collations are {1}.";

    internal const string DecimalTermMessage =
        "Index '{0}' on '{1}': column '{2}' is a decimal, which provider 'sqlite' stores as text ordered and compared as a number (collation orm_decimal), so a collation or lower() on it would override that and is refused; index the column as it is.";

    internal const string DuplicateKeyMessage =
        "cannot recreate unique index {0} on {1} ({2}): a duplicate key exists, nothing was deleted";

    internal const string SoftRowsMessage =
        "cannot drop {0} from {1}: {2} soft-deleted row(s) would read as live";

    internal const string IndexOwner = "index";

    internal const string ConstraintOwner = "constraint";

    internal const string CollisionMessage =
        "Live-only index {0} on table {1} over columns ({2}) cannot take effect: {3} {4} is unique over the same columns without a live-row predicate. "
        + "Convert it with an explicit migration: create the partial index, then drop the total one.";
}
