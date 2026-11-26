namespace Neemle.OrmLite;

internal sealed record PolicyDemandWork(object Entity, IGraphNavigationBinding Edge,
    PolicyAttachment Attachment, long Generation);
