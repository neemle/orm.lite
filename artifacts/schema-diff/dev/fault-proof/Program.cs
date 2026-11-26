using FaultProof;

foreach (var kind in Enum.GetValues<FaultKind>()) await FaultWitness.RunAsync(kind);
Console.WriteLine("INJECTED_PROVIDER_FAULT_PROOF_PASS runtime=" + Environment.Version);
