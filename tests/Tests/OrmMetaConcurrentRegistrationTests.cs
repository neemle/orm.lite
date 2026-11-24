using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neemle.OrmLite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class OrmMetaConcurrentRegistrationTests
{
    private sealed class Marker<T> { }

    private static readonly string[] Columns = { "id", "name" };

    private static string Col(string member) => member switch
    {
        "Id" => "id",
        "Name" => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown marker member."),
    };

    private static (Action Register, Type Key, string Table) Entry<T>()
    {
        var table = "concurrent_" + typeof(T).Name.ToLowerInvariant();
        return (() => OrmMeta.Register<Marker<T>>(table, Col, Columns), typeof(Marker<T>), table);
    }

    private static readonly (Action Register, Type Key, string Table)[] Entries =
    {
        Entry<byte>(), Entry<sbyte>(), Entry<short>(), Entry<ushort>(),
        Entry<int>(), Entry<uint>(), Entry<long>(), Entry<ulong>(),
        Entry<float>(), Entry<double>(), Entry<decimal>(), Entry<char>(),
        Entry<bool>(), Entry<string>(), Entry<Guid>(), Entry<DateTime>(),
    };

    [Fact]
    public void Concurrent_Register_and_Get_never_corrupt_the_registry()
    {
        const int rounds = 50;
        var failures = new ConcurrentQueue<Exception>();

        using var start = new Barrier(Entries.Length);
        var threads = Enumerable.Range(0, Entries.Length).Select(offset => new Thread(() =>
        {
            try
            {
                start.SignalAndWait();

                for (var round = 0; round < rounds; round++)
                {
                    for (var j = 0; j < Entries.Length; j++)
                    {
                        var entry = Entries[(offset + j) % Entries.Length];
                        entry.Register();

                        var meta = OrmMeta.Get(entry.Key);
                        if (meta.Table != entry.Table)
                        {
                            throw new InvalidOperationException(
                                $"Registry returned table '{meta.Table}' for {entry.Key.Name}, expected '{entry.Table}'.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Enqueue(ex);
            }
        })).ToArray();

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.True(
            failures.IsEmpty,
            "Concurrent OrmMeta access failed: " + string.Join(" | ", failures.Select(e => e.Message)));

        foreach (var entry in Entries)
        {
            var meta = OrmMeta.Get(entry.Key);
            Assert.Equal(entry.Table, meta.Table);
            Assert.Equal(Columns, meta.Columns);
            Assert.Equal("name", meta.Col("Name"));
        }
    }

    [Fact]
    public void Get_throws_for_a_type_that_was_never_registered()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OrmMeta.Get(typeof(Marker<TimeSpan>)));

        Assert.Contains("No ORM metadata registered", ex.Message);
        Assert.Contains(typeof(Marker<TimeSpan>).FullName!, ex.Message);
    }
}
