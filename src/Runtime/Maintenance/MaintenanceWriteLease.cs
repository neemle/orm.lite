namespace Neemle.OrmLite;

/// <summary>
/// The outcome of a bounded attempt to enter a database's writer gate. <see cref="Entered"/> is false when the gate was
/// not free within the timeout; <see cref="Release"/> gives the gate back and is null when there is nothing to give back.
/// </summary>
public readonly record struct MaintenanceWriteLease(bool Entered, IAsyncDisposable? Release);
