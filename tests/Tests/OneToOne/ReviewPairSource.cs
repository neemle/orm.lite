using Microsoft.CodeAnalysis.CSharp;

namespace Neemle.OrmLite.Tests.OneToOne;

internal static class ReviewPairSource
{
    internal const string ParentTable = "p_\"; SELECT 7;--Ω";
    internal const string OwnerTable = "o_\"; DROP TABLE r111_witness;--β";
    internal const string KeyColumn = "key\";--κ";
    internal const string ForeignColumn = "owner\";--δ";
    internal const string RowColumn = "payload\";--σ";
    internal const string ForeignName = "fk_\";--Ω";
    internal const string UniqueName = "uq_\";--β";
    internal const string KeyA = "password=private-r111-A'); DROP TABLE r111_witness;--🧪";
    internal const string KeyB = "password=private-r111-a'); DROP TABLE r111_witness;--🧪";
    internal const string RowA = "private-row-r111-A";
    internal const string RowB = "private-row-r111-B";
    internal const string OwnerRow = "private-owner-row-r111";

    internal static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    internal static string Text => Models.Replace("__PT__", Literal(ParentTable))
        .Replace("__OT__", Literal(OwnerTable)).Replace("__KC__", Literal(KeyColumn))
        .Replace("__FC__", Literal(ForeignColumn)).Replace("__RC__", Literal(RowColumn))
        .Replace("__FK__", Literal(ForeignName)).Replace("__UQ__", Literal(UniqueName))
        + ReviewPairOperations.Text.Replace("__KA__", Literal(KeyA)).Replace("__KB__", Literal(KeyB))
            .Replace("__RA__", Literal(RowA)).Replace("__RB__", Literal(RowB))
            .Replace("__OR__", Literal(OwnerRow));

    private const string Models = """
        using System.Data.Common;
        using System.Threading.Tasks;
        using Neemle.OrmLite.Nodes;
        using Neemle.OrmLite.Generated;
        [Table(__PT__)] public class Principal
        {
            [Key, Column(__KC__)] public string Id { get; set; } = "";
            [Column(__RC__)] public string Payload { get; set; } = "";
            [InverseOneToOne(nameof(Detail.Parent))] public Detail? Detail { get; set; }
        }
        [Table(__OT__)] public class Detail
        {
            [Key] public Guid Id { get; set; }
            [Column(__FC__)] public string? PrincipalId { get; set; }
            [Column(__RC__)] public string Payload { get; set; } = "";
            [OneToOne(nameof(PrincipalId), nameof(Principal.Detail))]
            [ForeignKey(__FK__, ReferentialAction.Restrict), OneToOneUnique(__UQ__)]
            public Principal? Parent { get; set; }
        }
        """;
}
