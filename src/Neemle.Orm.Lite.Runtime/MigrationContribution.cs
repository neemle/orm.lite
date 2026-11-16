#nullable enable
using System;
using System.Collections.Generic;

namespace Neemle.Orm.Lite;

public readonly record struct MigrationContribution(
    string Version,
    Reversibility Reversibility,
    Func<IReadOnlyList<string>> Ops,
    Action<MigrationBuilder>? Down);
