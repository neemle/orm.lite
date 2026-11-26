using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal static class SecurityModel
{
    internal const string RootTable = "root\"; DROP TABLE sec108_witness;--Ω";
    internal const string LeafTable = "leaf\"; SELECT 9;--β";
    internal const string LinkTable = "link\"; DELETE FROM sec108_witness;--";
    internal const string KeyColumn = "id\";--κ";
    internal const string ParentColumn = "owner\";--";
    internal const string PayloadColumn = "payload\";--";
    internal const string LinkRoot = "from\";--";
    internal const string LinkLeaf = "to\";--";
    internal const string ChildrenName = "Children\"; DROP TABLE sec108_witness;--";
    internal const string PeersName = "Peers\"; SELECT 99;--";
    internal const string ParentName = "Parent\";--";
    internal const string KeyA = "password=fixture_A'); DROP TABLE sec108_witness;--🧪";
    internal const string KeyB = "password=fixture_a'); DROP TABLE sec108_witness;--🧪";
    internal const string LeafA = "password=leaf_A\" UNION SELECT 9;--";
    internal const string LeafB = "password=leaf_B\" UNION SELECT 9;--";
    internal const string RowA = "private-row-sec108-root-A";
    internal const string RowB = "private-row-sec108-root-B";
    internal const string LeafRow = "private-row-sec108-leaf";
    internal const string Credential = "fake_connection_credential_108";
    internal static int MappedRoots { get; set; }

    private static readonly EntityDescriptor Roots = EntityDescriptor.Of("SecurityRoot", RootTable, false)
        .Property("Id", KeyColumn, ColumnType.Text, false, true)
        .Property("Payload", PayloadColumn, ColumnType.Text, false, false);
    private static readonly EntityDescriptor Leaves = EntityDescriptor.Of("SecurityLeaf", LeafTable, false)
        .Property("Id", KeyColumn, ColumnType.Text, false, true)
        .Property("ParentId", ParentColumn, ColumnType.Text, true, false)
        .Property("Payload", PayloadColumn, ColumnType.Text, false, false);

    static SecurityModel()
    {
        OrmMeta.Register<SecurityRoot>(Roots);
        OrmMeta.Register<SecurityLeaf>(Leaves);
        GraphModel.Register(new GraphEntityBinding<SecurityRoot>(Roots, MapRoot,
            new GraphKeyBinding<SecurityRoot, string>(KeyColumn, r => r.Id),
            new GraphCollectionBinding<SecurityRoot, SecurityLeaf, string>(ChildrenName, ParentColumn,
                r => r.Id, l => new(l.ParentId is not null, l.ParentId!), (r, children) => r.Children = children),
            new GraphLinkBinding<SecurityRoot, SecurityLeaf, string, string>(PeersName,
                new(LinkTable, LinkRoot, LinkLeaf), r => r.Id, (r, peers) => r.Peers = peers)));
        GraphModel.Register(new GraphEntityBinding<SecurityLeaf>(Leaves, MapLeaf,
            new GraphKeyBinding<SecurityLeaf, string>(KeyColumn, l => l.Id),
            new GraphReferenceBinding<SecurityLeaf, SecurityRoot, string>(ParentName, ParentColumn,
                l => new(l.ParentId is not null, l.ParentId!), (l, parent) => l.Parent = parent)));
    }

    internal static void Register() { }
    internal static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    internal static GraphPath<SecurityRoot, SecurityLeaf> Children =>
        GraphPaths.Edge<SecurityRoot, SecurityLeaf>(ChildrenName);
    internal static GraphPath<SecurityRoot, SecurityLeaf> Peers =>
        GraphPaths.Edge<SecurityRoot, SecurityLeaf>(PeersName);
    internal static GraphPath<SecurityLeaf, SecurityRoot> Parent =>
        GraphPaths.Edge<SecurityLeaf, SecurityRoot>(ParentName);

    internal static Query<SecurityRoot> Query(OrmContext db) =>
        new Query<SecurityRoot>(db, MapRoot).Where(r => r.Id == KeyA || r.Id == KeyB);

    internal static SecurityRoot MapRoot(DbDataReader reader)
    {
        MappedRoots++;
        return new SecurityRoot { Id = reader.GetString(reader.GetOrdinal(KeyColumn)),
            Payload = reader.GetString(reader.GetOrdinal(PayloadColumn)) };
    }

    private static SecurityLeaf MapLeaf(DbDataReader reader) => new()
    {
        Id = reader.GetString(reader.GetOrdinal(KeyColumn)),
        ParentId = reader.IsDBNull(reader.GetOrdinal(ParentColumn)) ? null
            : reader.GetString(reader.GetOrdinal(ParentColumn)),
        Payload = reader.GetString(reader.GetOrdinal(PayloadColumn)),
    };
}
