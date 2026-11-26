using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

internal sealed record ForeignKeyIntent(string Navigation, string Name, int Action, int? End, Location Location);
