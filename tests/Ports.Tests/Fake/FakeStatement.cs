namespace Neemle.OrmLite.Ports.Tests.Fake;

// One command that reached the fake server: its text and the parameters it carried, by name.
public sealed record FakeStatement(string Text, IReadOnlyDictionary<string, object> Values);
