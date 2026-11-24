#nullable enable
using System;
using System.Collections.Generic;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

public readonly record struct MigrationContribution(
    string Version,
    Reversibility Reversibility,
    Func<ISqlProvider, IReadOnlyList<string>> Render,
    Action<MigrationBuilder>? Down)
{
    public Func<ISqlProvider, IReadOnlyList<Node>>? Nodes { get; init; }

    public static MigrationContribution Of(
        string version, Reversibility reversibility, IReadOnlyList<Node> plan, Action<MigrationBuilder>? down = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(version, Declared(reversibility), provider => DdlCompiler.Render(plan, provider), down)
        {
            Nodes = _ => plan,
        };
    }

    public static MigrationContribution For<TMigration>(string version, Reversibility reversibility, bool overridesDown)
        where TMigration : Migration, new() =>
        new(
            version,
            Declared(reversibility),
            static provider =>
            {
                var builder = new MigrationBuilder(provider);
                new TMigration().Up(builder);
                return builder.Render(provider);
            },
            overridesDown && reversibility != Reversibility.Irreversible ? static b => new TMigration().Down(b) : null)
        {
            Nodes = static provider =>
            {
                var builder = new MigrationBuilder(provider);
                new TMigration().Up(builder);
                return builder.Nodes;
            },
        };

    internal void Require()
    {
        if (string.IsNullOrWhiteSpace(Version))
            throw new InvalidOperationException(
                "a migration contribution has no Version; a default MigrationContribution is not a migration.");
        if (Render is null)
            throw new InvalidOperationException($"migration {Version} has no Render, so it has nothing to run.");
        if (!Enum.IsDefined(Reversibility))
            throw new InvalidOperationException(UndefinedMessage(Version, Reversibility));
    }

    private static Reversibility Declared(Reversibility reversibility) =>
        Enum.IsDefined(reversibility)
            ? reversibility
            : throw new ArgumentOutOfRangeException(
                nameof(reversibility), reversibility, UndefinedMessage("(unnamed)", reversibility));

    private static string UndefinedMessage(string version, Reversibility value) =>
        $"migration {version} declares Reversibility value {(int)value}, which is not a named member of "
        + "Neemle.Orm.Lite.Reversibility; declare Reversibility.Reversible or Reversibility.Irreversible. "
        + "Nothing was executed.";
}
