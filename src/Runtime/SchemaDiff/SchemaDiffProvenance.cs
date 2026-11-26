namespace Neemle.OrmLite;

public sealed record SchemaDiffProvenance(
    string ModelVersion, string TargetDigest, string ObservationDigest, string ProviderProfile);
