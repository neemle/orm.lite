namespace Neemle.Orm.Lite.Tests.Corpus;

public abstract class DialectForwarder(Action<string> touch) : ISqlDialect
{
    private readonly PgDialect _pg = new();

    protected T Seen<T>(string member, T value)
    {
        touch(member);
        return value;
    }

    public string Quote(string identifier) => Seen(nameof(Quote), _pg.Quote(identifier));

    public string Param(int index) => Seen(nameof(Param), _pg.Param(index));

    public string Page(int? limit, int? offset) => Seen(nameof(Page), _pg.Page(limit, offset));

    public virtual int MaxBindParameters => Seen(nameof(MaxBindParameters), _pg.MaxBindParameters);

    public virtual bool SupportsUpsert => Seen(nameof(SupportsUpsert), _pg.SupportsUpsert);

    public virtual bool SupportsReturning => Seen(nameof(SupportsReturning), _pg.SupportsReturning);

    public string NowExpression => Seen(nameof(NowExpression), _pg.NowExpression);

    public string ConflictClause(IReadOnlyList<string> conflictColumns, OnConflict action, IReadOnlyList<UpsertSet> sets, string table, string? targetPredicate = null) =>
        Seen(nameof(ConflictClause), _pg.ConflictClause(conflictColumns, action, sets, table, targetPredicate));

    public string ReturningClause(IReadOnlyList<string> columns) => Seen(nameof(ReturningClause), _pg.ReturningClause(columns));

    public string CoalesceAssignment(string quotedColumn, string parameter) =>
        Seen(nameof(CoalesceAssignment), _pg.CoalesceAssignment(quotedColumn, parameter));

    public virtual bool SupportsArrayParameter => Seen(nameof(SupportsArrayParameter), _pg.SupportsArrayParameter);

    public virtual bool SupportsNullSafeEquality => Seen(nameof(SupportsNullSafeEquality), _pg.SupportsNullSafeEquality);

    public virtual bool SupportsRowLock => Seen(nameof(SupportsRowLock), _pg.SupportsRowLock);

    public virtual bool SupportsAggregateFilter => Seen(nameof(SupportsAggregateFilter), _pg.SupportsAggregateFilter);

    public string AnyOfExpression(string quotedColumn, string parameter) =>
        Seen(nameof(AnyOfExpression), _pg.AnyOfExpression(quotedColumn, parameter));

    public string NullSafeEqualsExpression(string quotedColumn, string parameter) =>
        Seen(nameof(NullSafeEqualsExpression), _pg.NullSafeEqualsExpression(quotedColumn, parameter));

    public string RowLockClause => Seen(nameof(RowLockClause), _pg.RowLockClause);

    public virtual bool SupportsKeyPreservingRowLock => Seen(nameof(SupportsKeyPreservingRowLock), _pg.SupportsKeyPreservingRowLock);

    public string KeyPreservingRowLockClause => Seen(nameof(KeyPreservingRowLockClause), _pg.KeyPreservingRowLockClause);

    public string CountAllExpression => Seen(nameof(CountAllExpression), _pg.CountAllExpression);

    public string CountFilterExpression(string predicate) => Seen(nameof(CountFilterExpression), _pg.CountFilterExpression(predicate));

    public string CountDistinctExpression(string quotedColumn) => Seen(nameof(CountDistinctExpression), _pg.CountDistinctExpression(quotedColumn));

    public string SumCoalescedExpression(string quotedColumn) => Seen(nameof(SumCoalescedExpression), _pg.SumCoalescedExpression(quotedColumn));

    public string MinExpression(string quotedColumn) => Seen(nameof(MinExpression), _pg.MinExpression(quotedColumn));

    public virtual bool SupportsIntervalArithmetic => Seen(nameof(SupportsIntervalArithmetic), _pg.SupportsIntervalArithmetic);

    public string NowPlusMinutesExpression(string parameter) => Seen(nameof(NowPlusMinutesExpression), _pg.NowPlusMinutesExpression(parameter));

    public string IncrementAssignment(string quotedColumn, string parameter) =>
        Seen(nameof(IncrementAssignment), _pg.IncrementAssignment(quotedColumn, parameter));

    public string PingStatement => Seen(nameof(PingStatement), _pg.PingStatement);

    public virtual bool SupportsJsonDocument => Seen(nameof(SupportsJsonDocument), _pg.SupportsJsonDocument);

    public string JsonDocumentParameter(string parameter) => Seen(nameof(JsonDocumentParameter), _pg.JsonDocumentParameter(parameter));

    public string JsonKeyTextExpression(string quotedColumn, string keyParameter) =>
        Seen(nameof(JsonKeyTextExpression), _pg.JsonKeyTextExpression(quotedColumn, keyParameter));

    public string JsonKeyComparableExpression(string quotedColumn, string keyParameter, JsonValueOrder order) =>
        Seen(nameof(JsonKeyComparableExpression), _pg.JsonKeyComparableExpression(quotedColumn, keyParameter, order));

    public string ComparableParameter(string parameter, JsonValueOrder order) =>
        Seen(nameof(ComparableParameter), _pg.ComparableParameter(parameter, order));

    public string JsonKeyRankExpression(string quotedColumn, string keyParameter, IReadOnlyList<string> arrangedParameters) =>
        Seen(nameof(JsonKeyRankExpression), _pg.JsonKeyRankExpression(quotedColumn, keyParameter, arrangedParameters));

    public string JsonKeyExistsExpression(string quotedColumn, string keyParameter) =>
        Seen(nameof(JsonKeyExistsExpression), _pg.JsonKeyExistsExpression(quotedColumn, keyParameter));

    public string JsonArrayContainsStringExpression(string quotedColumn, string keyParameter, string valueParameter) =>
        Seen(nameof(JsonArrayContainsStringExpression), _pg.JsonArrayContainsStringExpression(quotedColumn, keyParameter, valueParameter));

    public string JsonArrayContainsAnyExpression(string quotedColumn, string keyParameter, IReadOnlyList<string> valueParameters) =>
        Seen(nameof(JsonArrayContainsAnyExpression), _pg.JsonArrayContainsAnyExpression(quotedColumn, keyParameter, valueParameters));

    public string JsonKeyIndexTextExpression(string quotedColumn, string literalKey) =>
        Seen(nameof(JsonKeyIndexTextExpression), _pg.JsonKeyIndexTextExpression(quotedColumn, literalKey));

    public string JsonKeyIndexValueExpression(string quotedColumn, string literalKey) =>
        Seen(nameof(JsonKeyIndexValueExpression), _pg.JsonKeyIndexValueExpression(quotedColumn, literalKey));

    public string MovedFieldResolvedTextExpression(string quotedDocumentColumn, string documentKey) =>
        Seen(nameof(MovedFieldResolvedTextExpression), _pg.MovedFieldResolvedTextExpression(quotedDocumentColumn, documentKey));

    public string TextValueExpression(string parameter) => Seen(nameof(TextValueExpression), _pg.TextValueExpression(parameter));

    public string JsonNullValueExpression => Seen(nameof(JsonNullValueExpression), _pg.JsonNullValueExpression);

    public string JsonObjectAssignment(IReadOnlyList<(string KeyParameter, string ValueExpression)> members) =>
        Seen(nameof(JsonObjectAssignment), _pg.JsonObjectAssignment(members));

    public string JsonDocumentMergeAssignment(string quotedDocumentColumn, string objectExpression) =>
        Seen(nameof(JsonDocumentMergeAssignment), _pg.JsonDocumentMergeAssignment(quotedDocumentColumn, objectExpression));

    public string JsonDocumentRemoveThenMergeAssignment(string quotedDocumentColumn, IReadOnlyList<string> keyParameters, string objectExpression) =>
        Seen(nameof(JsonDocumentRemoveThenMergeAssignment), _pg.JsonDocumentRemoveThenMergeAssignment(quotedDocumentColumn, keyParameters, objectExpression));

    public string JsonMemberIncrementExpression(string quotedDocumentColumn, string keyParameter, string parameter) =>
        Seen(nameof(JsonMemberIncrementExpression), _pg.JsonMemberIncrementExpression(quotedDocumentColumn, keyParameter, parameter));

    public string JsonKeyOrderTerm(string quotedColumn, string keyParameter, bool descending, bool nullsFirst) =>
        Seen(nameof(JsonKeyOrderTerm), _pg.JsonKeyOrderTerm(quotedColumn, keyParameter, descending, nullsFirst));

    public string MovedFieldNumberOrEpochMillisOrderTerm(string quotedDocumentColumn, string keyParameter, string quotedTimestampColumn) =>
        Seen(nameof(MovedFieldNumberOrEpochMillisOrderTerm), _pg.MovedFieldNumberOrEpochMillisOrderTerm(quotedDocumentColumn, keyParameter, quotedTimestampColumn));

    public string OrderDirectionClause(bool descending, bool nullsFirst) =>
        Seen(nameof(OrderDirectionClause), _pg.OrderDirectionClause(descending, nullsFirst));
}

public sealed class RecordingDialect(Action<string> touch, int? maxBindParameters = null) : DialectForwarder(touch)
{
    public override int MaxBindParameters => maxBindParameters ?? base.MaxBindParameters;
}

public sealed class NoCapabilityDialect(Action<string> touch) : DialectForwarder(touch)
{
    public override bool SupportsUpsert => false;

    public override bool SupportsReturning => false;

    public override bool SupportsArrayParameter => false;

    public override bool SupportsNullSafeEquality => false;

    public override bool SupportsRowLock => false;

    public override bool SupportsAggregateFilter => false;

    public override bool SupportsKeyPreservingRowLock => false;

    public override bool SupportsIntervalArithmetic => false;

    public override bool SupportsJsonDocument => false;
}
