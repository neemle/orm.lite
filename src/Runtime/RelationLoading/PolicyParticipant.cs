namespace Neemle.OrmLite;

internal sealed record PolicyParticipant(object Entity, PolicyAttachment? Attachment, long Generation);
