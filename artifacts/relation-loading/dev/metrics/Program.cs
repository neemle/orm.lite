using System.Text.Json;

var records = File.ReadAllLines(args[0]).Select(MetricScan.FileOf).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { convention = "McCabe1+if/loops/catch/nondefault cases/conditional expression and &&/||/?? boundaries", records }, new JsonSerializerOptions { WriteIndented = true }));
