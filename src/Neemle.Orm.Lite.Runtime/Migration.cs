#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neemle.Orm.Lite;

    public abstract class Migration
    {
        public abstract void Up(MigrationBuilder builder);

        public virtual void Down(MigrationBuilder builder) =>
            throw new NotSupportedException(GetType().FullName + " does not override Down.");
    }
