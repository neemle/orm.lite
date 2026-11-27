namespace Neemle.OrmLite;

/// <summary>
/// Session statements a provider runs around a maintenance statement on the same connection: <see cref="Setup"/> before
/// it and <see cref="Teardown"/> after it, whatever the outcome. Rendered from a time span only.
/// </summary>
public sealed record MaintenanceSessionGuard(string Setup, string Teardown);
