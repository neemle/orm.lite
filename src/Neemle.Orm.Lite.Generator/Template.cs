using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Neemle.Orm.Lite.Templating
{
    internal static class Template
    {
        public static string Render(string template, object model)
        {
            var nodes = new Parser { S = template }.Block(null);
            var sb = new StringBuilder();
            RenderNodes(nodes, new List<(string, object?)> { ("", model) }, sb);
            return sb.ToString();
        }

        public static string RenderResource(Assembly asm, string fileName, object model) =>
            Render(LoadResource(asm, fileName), model);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _cache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        private static string LoadResource(Assembly asm, string fileName)
        {
            if (_cache.TryGetValue(fileName, out var cached)) return cached;
            var names = asm.GetManifestResourceNames();
            var res = names.FirstOrDefault(r => r.EndsWith(fileName, StringComparison.Ordinal));
            if (res == null)
                throw new InvalidOperationException($"Template '{fileName}' not embedded in {asm.GetName().Name}. Available: [{string.Join(", ", names)}]");
            using var stream = asm.GetManifestResourceStream(res)!;
            using var reader = new System.IO.StreamReader(stream);
            return _cache[fileName] = reader.ReadToEnd();
        }

        private abstract class Node { }
        private sealed class Text : Node { public string Value = ""; }
        private sealed class Interp : Node { public string Path = ""; }
        private sealed class For : Node { public string Var = "", Path = ""; public List<Node> Body = new List<Node>(); }
        private sealed class If : Node { public string Path = ""; public List<Node> Then = new List<Node>(), Else = new List<Node>(); }

        private sealed class Parser
        {
            public string S = "";
            public int Pos;
            public string LastStop = "";

            public List<Node> Block(string[]? stops)
            {
                var nodes = new List<Node>();
                while (Pos < S.Length)
                {
                    var open = S.IndexOf("{{", Pos, StringComparison.Ordinal);
                    if (open < 0) { Emit(nodes, S.Substring(Pos)); Pos = S.Length; break; }
                    if (open > Pos) Emit(nodes, S.Substring(Pos, open - Pos));
                    var close = S.IndexOf("}}", open + 2, StringComparison.Ordinal);
                    if (close < 0) { Emit(nodes, S.Substring(open)); Pos = S.Length; break; }
                    var token = S.Substring(open + 2, close - open - 2).Trim();
                    var after = close + 2;

                    if (!token.StartsWith("#", StringComparison.Ordinal))
                    {
                        nodes.Add(new Interp { Path = token });
                        Pos = after;
                        continue;
                    }

                    var dir = token.Substring(1).Trim();
                    var head = dir.Split(' ')[0];
                    var (lineAfter, ownLine) = LineSpan(S, open, after);
                    if (ownLine) TrimTrailingInlineWhitespace(nodes);
                    Pos = ownLine ? lineAfter : after;

                    if (stops != null && Array.IndexOf(stops, head) >= 0) { LastStop = head; return nodes; }

                    if (head == "for")
                    {
                        var parts = dir.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        nodes.Add(new For { Var = parts[1], Path = parts[3], Body = Block(new[] { "endfor" }) });
                    }
                    else if (head == "if")
                    {
                        var node = new If { Path = dir.Substring(2).Trim() };
                        node.Then = Block(new[] { "else", "endif" });
                        if (LastStop == "else") node.Else = Block(new[] { "endif" });
                        nodes.Add(node);
                    }
                }
                return nodes;
            }
        }

        private static void Emit(List<Node> nodes, string text) { if (text.Length > 0) nodes.Add(new Text { Value = text }); }

        private static (int lineAfter, bool ownLine) LineSpan(string s, int open, int after)
        {
            var ls = open; while (ls > 0 && s[ls - 1] != '\n') ls--;
            var le = after; while (le < s.Length && s[le] != '\n') le++;
            var ownLine = s.Substring(ls, open - ls).Trim().Length == 0 && s.Substring(after, le - after).Trim().Length == 0;
            return (le < s.Length ? le + 1 : le, ownLine);
        }

        private static void TrimTrailingInlineWhitespace(List<Node> nodes)
        {
            if (nodes.Count == 0 || !(nodes[nodes.Count - 1] is Text t)) return;
            var nl = t.Value.LastIndexOf('\n');
            var tail = nl < 0 ? t.Value : t.Value.Substring(nl + 1);
            if (tail.Trim().Length == 0) t.Value = nl < 0 ? "" : t.Value.Substring(0, nl + 1);
            if (t.Value.Length == 0) nodes.RemoveAt(nodes.Count - 1);
        }

        private static void RenderNodes(List<Node> nodes, List<(string name, object? val)> scope, StringBuilder sb)
        {
            foreach (var n in nodes)
                switch (n)
                {
                    case Text t: sb.Append(t.Value); break;
                    case Interp i: sb.Append(Resolve(i.Path, scope)?.ToString() ?? ""); break;
                    case If f: RenderNodes(Truthy(Resolve(f.Path, scope)) ? f.Then : f.Else, scope, sb); break;
                    case For fr:
                        if (Resolve(fr.Path, scope) is IEnumerable seq)
                            foreach (var item in seq)
                            {
                                scope.Add((fr.Var, item));
                                RenderNodes(fr.Body, scope, sb);
                                scope.RemoveAt(scope.Count - 1);
                            }
                        break;
                }
        }

        private static bool Truthy(object? v) => v switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            IEnumerable e => e.Cast<object>().Any(),
            _ => true,
        };

        private static object? Resolve(string path, List<(string name, object? val)> scope)
        {
            var parts = path.Split('.');
            object? cur = null;
            var found = false;
            for (var i = scope.Count - 1; i >= 0 && !found; i--)
                if (scope[i].name == parts[0]) { cur = scope[i].val; found = true; }
            if (!found)
                for (var i = scope.Count - 1; i >= 0 && !found; i--)
                    if (scope[i].name == "") { cur = Member(scope[i].val, parts[0]); found = true; }
            for (var j = 1; j < parts.Length && cur != null; j++) cur = Member(cur, parts[j]);
            return cur;
        }

        private static object? Member(object? obj, string name)
        {
            if (obj == null) return null;
            if (obj is IDictionary<string, object?> d) return d.TryGetValue(name, out var v) ? v : null;
            return obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
        }
    }
}
