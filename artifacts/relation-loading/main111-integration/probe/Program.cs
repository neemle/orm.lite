using ComposeProbe;
Console.WriteLine("composition runtime="+Environment.Version);
await using var fixture=await Fixture.OpenAsync();
await BufferedCases.Run(fixture);
await BufferedCases.Scoped(fixture);
await MaskCases.Run(fixture);
await MaskCases.Forged(fixture);
await StreamCases.Run(fixture);
Console.WriteLine("109+111 new composition assertions="+Check.Count+" PASS");
