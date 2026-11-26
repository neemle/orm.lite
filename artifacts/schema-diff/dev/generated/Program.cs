using Generated117;

var postgres = PostgresFactory.Create();
var sqlite = SqliteFactory.Create();
var postgresCreate = PostgresCreateFactory.Create();
var sqliteCreate = SqliteCreateFactory.Create();
if (postgres.Version != sqlite.Version || postgres.ReviewDigest == sqlite.ReviewDigest)
    throw new InvalidOperationException("factory identity mismatch");
if (!postgres.DownProofRequired || postgres.CoverageVersion == postgres.Version)
    throw new InvalidOperationException("recovery obligation mismatch");
if (postgresCreate.Version != sqliteCreate.Version || postgresCreate.Version == postgres.Version)
    throw new InvalidOperationException("create factory identity mismatch");
Console.WriteLine("COMPILED_FACTORY_PASS runtime=" + Environment.Version + " version=" + postgres.Version);
