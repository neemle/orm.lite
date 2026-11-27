using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveRegistrationRow<TMarker>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    internal static EntityDescriptor Descriptor() =>
        EntityDescriptor.Of(typeof(SaveRegistrationRow<TMarker>).FullName!, "gs112_registration", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("Name", "name", ColumnType.Text, false, false);

    internal static object?[] Values(SaveRegistrationRow<TMarker> row) => [row.Id, row.Name];
    internal static Guid Key(SaveRegistrationRow<TMarker> row) => row.Id;
    internal static SaveRegistrationRow<TMarker> Map(DbDataReader reader) => new()
    { Id = reader.GetGuid(0), Name = reader.GetString(1) };

    internal static GraphSaveEntityBinding<SaveRegistrationRow<TMarker>, Guid> Binding(
        EntityDescriptor descriptor) => new(descriptor, Values, Key, Map, []);
}
