using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

if (args[0] == "width") { WidthLayout.Run(args[1]); return; }

var folder = args[0];
var className = args[1];
var provider = args[2];
if (File.Exists(Path.Combine(folder, className + ".cs")))
    throw new InvalidOperationException("one-shot retained authoring tool; existing constants must be edited normally");
var values = new Dictionary<string, string>(StringComparer.Ordinal);
var constants = new StringBuilder();
foreach (var file in Directory.GetFiles(folder, "*.cs").Order(StringComparer.Ordinal))
{
    if (file.EndsWith("DataLiterals.cs", StringComparison.Ordinal)) continue;
    var original = File.ReadAllText(file);
    var root = CSharpSyntaxTree.ParseText(original).GetRoot();
    var literals = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
        .Where(n => n.IsKind(SyntaxKind.StringLiteralExpression) && Multiple(n.Token.ValueText)).ToArray();
    var edits = literals.Select(literal => (Node: literal, Name: Name(literal.Token.ValueText, file))).ToArray();
    var updated = root.ReplaceNodes(literals, (literal, _) => SyntaxFactory.ParseExpression(
        className + "." + edits.Single(e => e.Node == literal).Name).WithTriviaFrom(literal));
    File.WriteAllText(file, updated.ToFullString());
}
File.WriteAllText(Path.Combine(folder, className + ".cs"), "namespace Neemle.OrmLite." + provider
    + ";\n\ninternal static class " + className + "\n{\n" + constants + "}\n");
Console.WriteLine("MOVED_LITERAL_VALUES " + values.Count);

bool Multiple(string text) => text.Any(c => char.IsWhiteSpace(c) || c is '\'' or '"' or '(' or ')');

string Name(string value, string file)
{
    if (values.TryGetValue(value, out var known)) return known;
    var name = Path.GetFileNameWithoutExtension(file).Replace('.', '_') + "Value" + values.Count;
    values.Add(value, name);
    constants.Append("    internal const string ").Append(name).Append(" =\n        ");
    var first = true; var chunk = new StringBuilder();
    foreach (var character in value)
    {
        if (SymbolDisplay.FormatLiteral(chunk.ToString() + character, true).Length > 80)
        { Append(chunk.ToString()); chunk.Clear(); }
        chunk.Append(character);
    }
    Append(chunk.ToString()); constants.Append(";\n\n");
    return name;

    void Append(string part)
    {
        if (!first) constants.Append("\n        + ");
        constants.Append(SymbolDisplay.FormatLiteral(part, true)); first = false;
    }
}
