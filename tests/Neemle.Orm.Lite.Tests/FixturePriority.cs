using System;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.Orm.Lite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public enum FixturePriority { low, high }
