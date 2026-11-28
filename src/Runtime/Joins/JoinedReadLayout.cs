using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class JoinedReadLayout(object selection, IReadOnlyList<JoinLayoutCell> cells,
    IReadOnlyList<string> names)
{
    private readonly JoinLayoutCell[] _cells = cells.ToArray();
    private readonly string[] _names = names.ToArray();

    internal JoinedRow Read(DbDataReader reader)
    {
        try
        {
            Header(reader);
            return new JoinedRow(selection, Array.AsReadOnly(_cells.Select(c => Cell(reader, c)).ToArray()));
        }
        catch (JoinReadException) { throw; }
        catch (Exception error) { throw new JoinReadException(JoinError.MappingFailure, error); }
    }

    private void Header(DbDataReader reader)
    {
        if (reader.FieldCount != _names.Length) throw new JoinReadException(JoinError.MappingFailure);
        for (var i = 0; i < _names.Length; i++)
            if (reader.GetName(i) != _names[i]) throw new JoinReadException(JoinError.MappingFailure);
    }

    private static JoinResultEntry Cell(DbDataReader reader, JoinLayoutCell cell)
    {
        var present = cell.PresenceOrdinal is not { } key || !reader.IsDBNull(key);
        var isNull = reader.IsDBNull(cell.Ordinal);
        if (!present && !isNull) throw new JoinReadException(JoinError.MappingFailure);
        if (present && isNull && (!cell.Entry.Column.Binding.Column.Nullable || cell.Entry.Required))
            throw new JoinReadException(JoinError.MappingFailure);
        return cell.Entry.Column.Binding.Decode(new(reader, cell.Ordinal, cell.Entry.Slot, present, isNull));
    }
}
