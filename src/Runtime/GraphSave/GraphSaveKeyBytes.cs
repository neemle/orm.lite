using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Neemle.OrmLite;

internal static class GraphSaveKeyBytes
{
    internal static byte[] Encode(object value) => value switch
    {
        Guid guid => guid.ToByteArray(bigEndian: true),
        string text => Text(text),
        int integer => Integer(integer),
        long integer => Integer(integer),
        bool boolean => [boolean ? (byte)1 : (byte)0],
        _ => Other(value),
    };

    private static byte[] Other(object value) => value switch
    {
        DateTime timestamp => Integer(timestamp.Ticks),
        DateOnly date => Integer(date.DayNumber),
        decimal number => Encoding.ASCII.GetBytes(number.ToString("G29", CultureInfo.InvariantCulture)),
        double number => Integer(BitConverter.DoubleToInt64Bits(double.IsNaN(number) ? double.NaN : number == 0 ? 0 : number)),
        Enum enumeration => Encoding.UTF8.GetBytes(enumeration.ToString()),
        _ => throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping),
    };

    private static byte[] Text(string text)
    {
        var bytes = new byte[checked(text.Length * 2)];
        for (var i = 0; i < text.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(i * 2), text[i]);
        return bytes;
    }

    private static byte[] Integer(long value)
    {
        var bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
