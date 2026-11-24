#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.OrmLite;

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class MigrationAttribute : System.Attribute { public MigrationAttribute(string version, Reversibility reversibility) { } }
