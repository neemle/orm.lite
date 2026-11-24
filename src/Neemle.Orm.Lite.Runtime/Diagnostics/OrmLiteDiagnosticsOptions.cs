using System.Globalization;

namespace Neemle.Orm.Lite;

public sealed class OrmLiteDiagnosticsOptions
{
    internal static readonly string[] SettingNames =
    [
        nameof(StatementLevel), nameof(SlowThresholdMilliseconds), nameof(SlowLevel), nameof(TransactionLevel),
        nameof(SampleRate), nameof(LogParameters), nameof(ParameterMaxLength),
    ];

    private OrmLogLevel? _statementLevel;

    private int? _slowThresholdMilliseconds;

    private OrmLogLevel? _slowLevel;

    private OrmLogLevel? _transactionLevel;

    private double? _sampleRate;

    private bool? _logParameters;

    private int? _parameterMaxLength;

    public IOrmDiagnosticsSink? Sink { get; set; }

    public OrmLogLevel StatementLevel
    {
        get => _statementLevel ?? OrmLogLevel.None;
        set => _statementLevel = value;
    }

    public int SlowThresholdMilliseconds
    {
        get => _slowThresholdMilliseconds ?? 0;
        set => _slowThresholdMilliseconds = value;
    }

    public TimeSpan SlowThreshold
    {
        get => TimeSpan.FromMilliseconds(SlowThresholdMilliseconds);
        set => SlowThresholdMilliseconds = checked((int)value.TotalMilliseconds);
    }

    public OrmLogLevel SlowLevel
    {
        get => _slowLevel ?? OrmLogLevel.Warning;
        set => _slowLevel = value;
    }

    public OrmLogLevel TransactionLevel
    {
        get => _transactionLevel ?? OrmLogLevel.None;
        set => _transactionLevel = value;
    }

    public double SampleRate
    {
        get => _sampleRate ?? 1;
        set => _sampleRate = value;
    }

    public bool LogParameters
    {
        get => _logParameters ?? false;
        set => _logParameters = value;
    }

    public int ParameterMaxLength
    {
        get => _parameterMaxLength ?? 64;
        set => _parameterMaxLength = value;
    }

    internal bool Reports =>
        StatementLevel != OrmLogLevel.None
        || (SlowThresholdMilliseconds > 0 && SlowLevel != OrmLogLevel.None)
        || TransactionLevel != OrmLogLevel.None;

    internal void Validate()
    {
        if (Sink is null && Reports)
            throw new InvalidOperationException(
                "OrmLite:Diagnostics asks for statements to be reported but no Sink is set; assign OrmLiteOptions.Diagnostics.Sink.");
        if (SlowThresholdMilliseconds < 0)
            throw new InvalidOperationException($"OrmLite:Diagnostics:SlowThresholdMilliseconds is {SlowThresholdMilliseconds}; use 0 to switch it off.");
        if (!(SampleRate > 0 && SampleRate <= 1))
            throw new InvalidOperationException($"OrmLite:Diagnostics:SampleRate is {SampleRate.ToString(CultureInfo.InvariantCulture)}; it must be above 0 and at most 1.");
        if (ParameterMaxLength is < 1 or > 4096)
            throw new InvalidOperationException($"OrmLite:Diagnostics:ParameterMaxLength is {ParameterMaxLength}; it must be between 1 and 4096.");
    }

    internal void Apply(string name, string value, string source)
    {
        switch (SettingNames.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)))
        {
            case nameof(StatementLevel): StatementLevel = Level(value, source); break;
            case nameof(SlowThresholdMilliseconds): SlowThresholdMilliseconds = OrmLiteOptions.Integer(value, source); break;
            case nameof(SlowLevel): SlowLevel = Level(value, source); break;
            case nameof(TransactionLevel): TransactionLevel = Level(value, source); break;
            case nameof(SampleRate): SampleRate = Number(value, source); break;
            case nameof(LogParameters): LogParameters = OrmLiteOptions.Boolean(value, source); break;
            case nameof(ParameterMaxLength): ParameterMaxLength = OrmLiteOptions.Integer(value, source); break;
            default:
                throw new ArgumentException(
                    $"'{source}' is not an Orm.Lite diagnostics setting; the settings are {string.Join(", ", SettingNames)}.");
        }
    }

    internal void Overlay(OrmLiteDiagnosticsOptions other)
    {
        Sink = other.Sink ?? Sink;
        _statementLevel = other._statementLevel ?? _statementLevel;
        _slowThresholdMilliseconds = other._slowThresholdMilliseconds ?? _slowThresholdMilliseconds;
        _slowLevel = other._slowLevel ?? _slowLevel;
        _transactionLevel = other._transactionLevel ?? _transactionLevel;
        _sampleRate = other._sampleRate ?? _sampleRate;
        _logParameters = other._logParameters ?? _logParameters;
        _parameterMaxLength = other._parameterMaxLength ?? _parameterMaxLength;
    }

    private static OrmLogLevel Level(string value, string source) =>
        Enum.TryParse<OrmLogLevel>(value.Trim(), ignoreCase: true, out var level) && Enum.IsDefined(level)
            ? level
            : throw new FormatException(
                $"the Orm.Lite setting '{source}' is '{value}', which is not one of {string.Join(", ", Enum.GetNames<OrmLogLevel>())}.");

    private static double Number(string value, string source) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new FormatException($"the Orm.Lite setting '{source}' is '{value}', which is not a number.");
}
