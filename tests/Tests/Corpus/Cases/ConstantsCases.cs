using System.Reflection;

namespace Neemle.OrmLite.Tests.Corpus.Cases;

internal static class ConstantsCases
{
    public static void Register(CorpusRun run)
    {
        run.Rec.Text("constants", "runtime-string-constants", () =>
        {
            var types = CorpusSurface.SurveyedTypes
                .Concat([typeof(Jsonb), typeof(SoftDeleteAttribute)])
                .Where(t => !t.ContainsGenericParameters)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);
            var lines = new List<string>
            {
                "AdvisorySessionLocks.TryLockStatement = " + CorpusFormat.Quote(DdlText.Statement(AdvisorySessionLocks.TryLock(0)).Text),
                "AdvisorySessionLocks.UnlockStatement = " + CorpusFormat.Quote(DdlText.Statement(AdvisorySessionLocks.Unlock(0)).Text),
            };
            foreach (var type in types)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(f => f.FieldType == typeof(string) && (f.IsLiteral || f.IsInitOnly))
                    .OrderBy(f => f.Name, StringComparer.Ordinal);
                foreach (var field in fields)
                {
                    var value = (string?)(field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null));
                    lines.Add(type.Name + "." + field.Name + " = " + (value is null ? "null" : CorpusFormat.Quote(value)));
                }
            }
            return CorpusFormat.Lines(lines);
        });
    }
}
