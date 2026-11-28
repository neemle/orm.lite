using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;

namespace Neemle.OrmLite.Generator.Orphans;

internal sealed record OrphanCollectionModel(Entity Parent, Entity Child, string Navigation, string OwnerProperty);
