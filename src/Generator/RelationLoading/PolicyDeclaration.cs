using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

internal sealed record PolicyDeclaration(string Navigation, int Mode, Location Location);
