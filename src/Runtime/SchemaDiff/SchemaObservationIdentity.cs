namespace Neemle.OrmLite;

public sealed record SchemaObservationIdentity(string DatabaseIdentityDigest, string ProviderProfile, bool Consistent);
