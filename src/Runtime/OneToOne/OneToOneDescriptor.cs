namespace Neemle.OrmLite;

public sealed record OneToOneDescriptor(OneToOneOwner Owner, OneToOneTarget Target, OneToOneConstraints Constraints);
