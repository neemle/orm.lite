ORML-109 bounded owner packet — unresolved contract bridges, not a delivery success

Pinned policy ADR f161e964 v2 / immutable8d738c20, Speck56fe3d2e v1 / immutablef8504998; producer108 frozen61dffff/source25306975. Both independent Runtime and Generator compile in Docker. Behavioral/provider/AOT/quality/security acceptance is NOT RUN at this checkpoint.

1. What observable origin satisfies bare Bind matching-context/provider?

Frozen GatedDataSource.LeaseOf associates a connection with lane/diagnostics, but not its originating source. DbDataSource/DbConnection provide no neutral origin identity. CreateConnection is explicitly refused by GatedDataSource. Current109 records exact context witnesses for LoadingScope.OpenAsync and UnitOfWork.BeginAsync; unknown bare origins safely refuse InvalidResource. No driver-name/type, credential/connection-string comparison, extra open or public DataSource replacement is used.

Minimal proposed owner route (UNIMPLEMENTED, GatedDataSource still117-owned): internal static bool GatedDataSource.OriginatesFrom(DbConnection connection, GatedDataSource source), backed by the existing weak lease table, retaining originating gate on each Lease and requiring the lease is not released. Existing public API/open/command/ownership behavior is unchanged. It proves the same datasource/provider, not which of two OrmContext wrappers over the SAME gate called Open. Owner must state whether that same-gate case satisfies the contract and what happens for arbitrary plain custom DbDataSource origins; no unobservable unforgeable-wrapper guarantee is inferred.

Positive witness: conn = await db.DataSource.OpenConnectionAsync(); LoadingScope.Bind(db, conn, null, options) succeeds for that exact live gate and performs zero Bind SQL/open. Negative witnesses: another gate/provider, released/closed/broken connection or mismatching transaction.Connection refuses before SQL, never opens replacement. Known Open/UoW origin paths remain supported.

2. How does generated GetAsync(db, conn, key, options, ct) name the scope transaction without weakening explicit On binding?

Authored LoadingScope exposes no public connection/transaction getter. Generated code is in the consumer assembly, so cannot access internal Binding. Query(db).On(conn) records a null transaction and must remain different from a UnitOfWork-bound scope. Omitting On would lose validation of the supplied conn. Current generated connection-bearing overload retains strict .On(conn), therefore its UnitOfWork-scope case is explicitly INCOMPLETE.

Minimal proposed owner route (UNIMPLEMENTED): additive class-constrained RelationQueryExtensions.FirstOrDefaultAsync<T>(this Query<T> query, DbConnection connection, RelationReadOptions options, CancellationToken ct=default) and ToListAsync counterpart if needed by generated reads. Four parameters. Runtime can validate query/context and exact supplied connection, fill the scope's transaction only for this explicit bridge, then execute one existing policy operation. Existing public methods/generic constraints and ordinary Query.On(conn, null) matching remain unchanged. Alternative: an equally bounded typed generated-read bridge selected by the owner; no six/seven-parameter Get API, raw statement/mask API, public resource getter or extra query is proposed.

Positive witness: generated GetAsync(db, unit.Connection, key, new RelationReadOptions(Scope: scopeBoundToUnit), ct) uses that exact connection+transaction and one root/eager budget. Negative: another conn/context, explicit Query.On(conn,null) against a tx scope, and disposed/committed unit refuse before SQL. Bare Bind origin and this generated bridge are separate questions.

No ADR/Speck/spec, tests/projects/controls, GatedDataSource or coordination-ledger changes were made. No new items, implementation admission, release or whole-feature PASS is claimed. Remaining full109 behavior and independent verification continue after owning disposition.
