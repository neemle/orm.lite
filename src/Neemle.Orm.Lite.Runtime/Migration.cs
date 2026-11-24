#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.Orm.Lite;

    public abstract class Migration
    {
        public abstract void Up(MigrationBuilder builder);

        public virtual void Down(MigrationBuilder builder)
        {
            var recorded = builder.Scratch();
            Up(recorded);
            var derived = MigrationOps.DeriveDown(recorded.Nodes)
                ?? throw new NotSupportedException(GetType().FullName + " does not override Down.");
            foreach (var op in derived) builder.Add(op);
        }
    }
