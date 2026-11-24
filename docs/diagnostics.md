# SQL diagnostics

[Documentation](README.md)

`OrmLiteOptions.Diagnostics` routes the SQL the provider serialised, its elapsed time, rows affected, connection and
transaction ids, and the transaction begin, commit and rollback events to a sink you assign: every statement, only
those over a threshold, or a sample. Parameter values are off by default and redacted when on: text that looks
like a secret, or that names one (a `password`, `token`, `api_key`, `api key`, `pin` or `Authorization` label in a JSON
document, a header line, a query string or an `a=b` pair, also when written with JSON escapes), is replaced whole,
whether it arrives as a string or as another value converted to text. A short unlabelled secret cannot be seen. Off by
default, and with no sink nothing is timed or allocated.

    options.Diagnostics.Sink = mySink;
    options.Diagnostics.SlowThreshold = TimeSpan.FromMilliseconds(200);

See [diagnostics in the demo](../demo/README.md) for a complete sink implementation.
