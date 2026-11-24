using System;
using Neemle.OrmLite;

namespace Neemle.OrmLite.Tests;

public sealed record FixtureDocument(Guid Id, Jsonb? Payload, string Label);
