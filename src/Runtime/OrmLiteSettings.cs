#nullable enable
using System;
using System.Threading;

namespace Neemle.OrmLite;

public static class OrmLiteSettings
{
    public const string DefaultActorSetting = "ormlite.actor";

    private const int MaxPartLength = 31;

    private static readonly object s_gate = new();

    private static string? s_registered;

    private static State? s_frozen;

    public static string ActorSetting => Frozen().Name;

    public static void UseActorSetting(string name)
    {
        Validate(name);
        lock (s_gate)
        {
            if (s_frozen is { } frozen)
            {
                if (frozen.Name != name) throw Conflict(frozen.Name, name);
                return;
            }

            if (s_registered is { } registered && registered != name) throw Conflict(registered, name);
            s_registered = name;
        }
    }

    internal static void Validate(string? name)
    {
        if (!IsActorSetting(name))
            throw new ArgumentException(
                "An Orm.Lite actor setting is a lowercase prefix and a lowercase name joined by one dot, for example app.actor; got '"
                + name + "'.",
                nameof(name));
    }

    internal static void ResetForTests()
    {
        lock (s_gate)
        {
            s_registered = null;
            s_frozen = null;
        }
    }

    private static State Frozen()
    {
        if (Volatile.Read(ref s_frozen) is { } cached) return cached;
        lock (s_gate)
        {
            s_frozen ??= new State(s_registered ?? DefaultActorSetting);
            return s_frozen;
        }
    }

    private static InvalidOperationException Conflict(string existing, string requested) =>
        new("The Orm.Lite actor setting is already '" + existing + "' and cannot become '" + requested + "'.");

    private static bool IsActorSetting(string? name)
    {
        if (name is null) return false;
        var dot = name.IndexOf('.');
        return dot > 0 && IsPart(name, 0, dot) && IsPart(name, dot + 1, name.Length - dot - 1);
    }

    private static bool IsPart(string value, int start, int length)
    {
        if (length < 1 || length > MaxPartLength) return false;
        if (value[start] is < 'a' or > 'z') return false;
        for (var i = start + 1; i < start + length; i++)
        {
            var ch = value[i];
            if (ch is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')) return false;
        }
        return true;
    }

    private sealed class State(string name)
    {
        public string Name { get; } = name;
    }
}
