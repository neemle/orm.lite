using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

internal sealed record PolicyIssue(string Navigation, string Reason, Location Location);
