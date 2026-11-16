using System;
using Neemle.Orm.Lite;

namespace Neemle.Orm.Lite.Tests;

public sealed record FixtureDocument(Guid Id, Jsonb? Payload, string Label);
