using System;
using System.Data.Common;
using System.Threading.Tasks;
using Neemle.OrmLite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public enum FixturePriority { low, high }
