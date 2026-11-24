namespace Neemle.OrmLite;

public sealed partial class OrmLiteOptions
{
    public string? Provider { get; set; }

    public string? ConnectionString { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? Database { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Path { get; set; }

    public bool? Pooling { get; set; }

    public int? BusyTimeoutSeconds { get; set; }

    public OrmLiteDiagnosticsOptions Diagnostics { get; } = new();
}
