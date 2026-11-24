using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Sqlite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class InitOnlyEntitySqliteTests : IAsyncLifetime
{
    private const string Source = """
        using System.Threading.Tasks;
        using System.Collections.Generic;
        using Neemle.Orm.Lite.Generated;

        public enum Mood { Calm, Loud }

        [Table("members")]
        public class Member
        {
            [Key] public required Guid Id { get; init; }
            public required string Name { get; init; }
            public string? Nickname { get; init; }
            public Mood Mood { get; init; }
            public int Visits { get; set; }
        }

        [Table("notes")]
        [SoftDelete(Tenant = SoftDeleteAttribute.Platform)]
        public class Note
        {
            [Key] public Guid Id { get; set; }
            public string Body { get; set; } = "";
            public DateTime? DeletedAt { get; init; }
        }

        [Table("owners")]
        public class Owner
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }

        [Table("pets")]
        public class Pet
        {
            [Key] public Guid Id { get; set; }
            public Guid OwnerId { get; init; }
            public Owner? Owner { get; set; }
        }

        [Table("stamps")]
        public record Stamp
        {
            [Key] public Guid Id { get; init; }
            public string Label { get; init; } = "";
            public int? Weight { get; init; }
        }

        public static class Ops
        {
            public static Member NewMember(Guid id, string name, string? nick, bool loud, int visits) =>
                new Member
                {
                    Id = id, Name = name, Nickname = nick, Mood = loud ? Mood.Loud : Mood.Calm, Visits = visits,
                };

            public static Task AddMember(OrmContext db, Member m) => MemberOrm.InsertAsync(db, m);

            public static async Task AddMemberOn(OrmContext db, Member m)
            {
                await using var connection = await db.DataSource.OpenConnectionAsync();
                await MemberOrm.InsertAsync(db, connection, m);
            }

            public static Task<Member?> GetMember(OrmContext db, Guid id) => MemberOrm.GetAsync(db, id);

            public static Task<List<Member>> AllMembers(OrmContext db) => MemberOrm.Query(db).ToListAsync();

            public static Task<List<string>> MemberNames(OrmContext db) =>
                MemberOrm.Query(db).ProjectColumn(m => m.Name).ProjectionAsync(r => r.GetString(0));

            public static Task ChangeMember(OrmContext db, Member m, int visits)
            {
                m.Visits = visits;
                return MemberOrm.UpdateAsync(db, m);
            }

            public static Task<int> DeleteMember(OrmContext db, Guid id) => MemberOrm.DeleteAsync(db, id);

            public static Task AddNote(OrmContext db, Guid id, string body) =>
                NoteOrm.InsertAsync(db, new Note { Id = id, Body = body });

            public static Task<int> DeleteNote(OrmContext db, Guid id) => NoteOrm.DeleteAsync(db, id);

            public static Task<List<Note>> AllNotes(OrmContext db) => NoteOrm.Query(db).ToListAsync();

            public static Task AddOwner(OrmContext db, Guid id, string name) =>
                OwnerOrm.InsertAsync(db, new Owner { Id = id, Name = name });

            public static Task AddPet(OrmContext db, Guid id, Guid owner) =>
                PetOrm.InsertAsync(db, new Pet { Id = id, OwnerId = owner });

            public static async Task<Pet?> PetWithOwner(OrmContext db, Guid id)
            {
                var pet = await PetOrm.GetAsync(db, id);
                await using var connection = await db.DataSource.OpenConnectionAsync();
                await PetOrm.LoadOwnerAsync(db, connection, pet!);
                return pet;
            }

            public static Task AddStamp(OrmContext db, Guid id, string label, int? weight) =>
                StampOrm.InsertAsync(db, new Stamp { Id = id, Label = label, Weight = weight });

            public static Task<Stamp?> GetStamp(OrmContext db, Guid id) => StampOrm.GetAsync(db, id);

            public static Task<List<Stamp>> AllStamps(OrmContext db) => StampOrm.Query(db).ToListAsync();
        }
        """;

    private static readonly Guid IdA = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private static readonly Guid IdB = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static readonly GeneratedModel Model = Build();

    private string _directory = "";

    private OrmContext _db = null!;

    private static GeneratedModel Build()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output);
    }

    public async Task InitializeAsync()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ormlite-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SqliteProviderFactory.Register();
        _db = await OrmLite.OpenAsync(
            new OrmLiteOptions { Provider = "sqlite", Path = Path.Combine(_directory, "init.db") });
        await MigrationRunner.RunAsync(_db.DataSource, _db.Provider, Model.Contributions);
        Model.RunModuleInitializer();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static async Task<object?> Ops(string method, params object?[] args)
    {
        var chosen = Model.Class("MemberOrm").Assembly.GetType("Fixture.Ops", throwOnError: true)!.GetMethod(method)!;
        var result = chosen.Invoke(null, args);
        if (result is not Task task) return result;
        await task;
        return task.GetType().IsGenericType ? task.GetType().GetProperty("Result")!.GetValue(task) : null;
    }

    private static object? Get(object target, string property) =>
        target.GetType().GetProperty(property)!.GetValue(target);

    private static List<object> Items(object? list) => ((System.Collections.IEnumerable)list!).Cast<object>().ToList();

    private async Task<long> CountAsync(string table)
    {
        await using var command = _db.DataSource.CreateCommand("SELECT count(*) FROM " + table);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact(
        DisplayName = "AC3 a required init entity round-trips through insert in both forms, get, query, "
        + "projection, update and delete")]
    public async Task A_required_init_entity_round_trips()
    {
        var ann = (await Ops("NewMember", IdA, "ann", null, true, 1))!;
        var bob = (await Ops("NewMember", IdB, "bob", "bobby", false, 2))!;

        await Ops("AddMember", _db, ann);
        await Ops("AddMemberOn", _db, bob);
        var found = (await Ops("GetMember", _db, IdA))!;

        Assert.NotSame(ann, found);
        Assert.Equal(IdA, Get(found, "Id"));
        Assert.Equal("ann", Get(found, "Name"));
        Assert.Null(Get(found, "Nickname"));
        Assert.Equal("Loud", Get(found, "Mood")!.ToString());
        Assert.Equal(1, Get(found, "Visits"));
        Assert.Equal("bobby", Get((await Ops("GetMember", _db, IdB))!, "Nickname"));
        Assert.Equal(2, Items(await Ops("AllMembers", _db)).Count);
        Assert.Equal(["ann", "bob"], ((List<string>)(await Ops("MemberNames", _db))!).Order().ToArray());
        await Ops("ChangeMember", _db, found, 7);
        Assert.Equal(7, Get((await Ops("GetMember", _db, IdA))!, "Visits"));
        Assert.Equal(1, await Ops("DeleteMember", _db, IdA));
        Assert.Null(await Ops("GetMember", _db, IdA));
    }

    [Fact(
        DisplayName = "AC3 a soft-delete entity with an init DeletedAt soft-deletes and is left out of the "
        + "default query")]
    public async Task A_soft_delete_entity_with_an_init_column_soft_deletes()
    {
        await Ops("AddNote", _db, IdA, "first");
        await Ops("AddNote", _db, IdB, "second");

        Assert.Equal(1, await Ops("DeleteNote", _db, IdA));

        var live = Items(await Ops("AllNotes", _db));
        Assert.Equal("second", Get(Assert.Single(live), "Body"));
        Assert.Equal(2, await CountAsync("notes"));
    }

    [Fact(DisplayName = "AC3 a to-one relation whose key is init-only loads through its loader")]
    public async Task A_relation_with_an_init_key_loads()
    {
        await Ops("AddOwner", _db, IdA, "ann");
        await Ops("AddPet", _db, IdB, IdA);

        var pet = (await Ops("PetWithOwner", _db, IdB))!;

        Assert.Equal("ann", Get(Get(pet, "Owner")!, "Name"));
    }

    [Fact(DisplayName = "AC9 a record entity round-trips, including a NULL column")]
    public async Task A_record_entity_round_trips()
    {
        await Ops("AddStamp", _db, IdA, "first", null);
        await Ops("AddStamp", _db, IdB, "second", 5);

        var first = (await Ops("GetStamp", _db, IdA))!;

        Assert.Equal("first", Get(first, "Label"));
        Assert.Null(Get(first, "Weight"));
        Assert.Equal(5, Get((await Ops("GetStamp", _db, IdB))!, "Weight"));
        Assert.Equal(2, Items(await Ops("AllStamps", _db)).Count);
    }
}
