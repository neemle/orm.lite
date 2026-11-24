using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Ports.Tests.Fake;

namespace Neemle.Orm.Lite.Ports.Tests;

// What the tests of this project share: one fake server with the provider and data source over it, and the two
// migrations that every scenario runs.
public sealed class PortsScenario
{
    public const string FirstVersion = "0001_create_widgets";

    public const string SecondVersion = "0002_add_widget_note";

    public const string Table = "ports_widgets";

    public PortsScenario()
    {
        Database = new FakeDatabase();
        Provider = new FakeProvider(Database);
        Source = new FakeDataSource(Database);
    }

    public FakeDatabase Database { get; }

    public FakeProvider Provider { get; }

    public FakeDataSource Source { get; }

    public OrmContext NewContext() => new(Source, Provider);

    public static MigrationContribution CreateWidgets() =>
        MigrationContribution.Of(FirstVersion, Reversibility.Reversible, [new CreateTable(Table, [
            new ColumnDef("id", new TypeRef(ColumnType.Uuid), false, true),
            new ColumnDef("name", new TypeRef(ColumnType.Text), false),
            new ColumnDef("rank", new TypeRef(ColumnType.Int32), false),
        ])]);

    public static MigrationContribution AddNote() =>
        MigrationContribution.Of(SecondVersion, Reversibility.Reversible, [
            new AddColumn(Table, new ColumnDef("note", new TypeRef(ColumnType.Text))),
        ]);

    public static MigrationContribution Contribution(string version, Node operation) =>
        MigrationContribution.Of(version, Reversibility.Irreversible, [operation]);
}
