namespace Neemle.OrmLite;

public enum SchemaDiffChangeKind
{
    CreateTable, AddNullableColumn, DropTable, DropColumn, PossibleRename, TypeChange, NullabilityChange,
    DefaultChange, KeyOrConstraintChange, IndexOrTriggerChange, UnknownMetadata, PreserveExternal,
}
