# SQL diagnostics

[Documentation](README.md)

## Report slow statements

Assign a diagnostics sink before opening the connection. This example sets a 200 ms threshold.

```csharp
options.Diagnostics.Sink = mySink;
options.Diagnostics.SlowThreshold = TimeSpan.FromMilliseconds(200);
```

See [diagnostics in the demo](../demo/README.md) and its [sink source](../demo/SlowQueryReport.cs) for a complete example.

## What the sink receives

`OrmLiteOptions.Diagnostics` routes the SQL the provider serialised, its elapsed time, rows affected, connection and
transaction ids, and the transaction begin, commit and rollback events to a sink you assign: every statement, only
those over a threshold, or a sample.

## Parameter values and redaction

- **Default:** Parameter values are excluded. With no sink, diagnostics does not time statements or allocate events.
- **With values enabled:** Secret-looking text is replaced in full, including non-string values converted to text.
  The redactor recognises labels such as `password`, `token`, `api_key`, `api key`, `pin`, and `Authorization`
  in JSON, headers, query strings, and `a=b` pairs. It also recognises escaped JSON labels.
- **Limit:** Short, unlabelled secrets may escape detection.
