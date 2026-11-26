using Metrics;
using System.Text.Json;

var records = File.ReadAllLines(args[0]).Select(MetricScan.File).ToArray();
Console.WriteLine(JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
