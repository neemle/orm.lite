#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.Orm.Lite;

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class MigrationAttribute : System.Attribute { public MigrationAttribute(string version, Reversibility reversibility) { } }
