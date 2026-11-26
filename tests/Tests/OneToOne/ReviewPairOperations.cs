namespace Neemle.OrmLite.Tests.OneToOne;

internal static class ReviewPairOperations
{
    internal const string Text = """
        public static class ReviewOps
        {
            private static Principal _known = null!;
            public static async Task Seed(OrmContext db, DbConnection connection)
            {
                await PrincipalOrm.InsertAsync(db, connection, new Principal { Id = __KA__, Payload = __RA__ });
                await PrincipalOrm.InsertAsync(db, connection, new Principal { Id = __KB__, Payload = __RB__ });
                await DetailOrm.InsertAsync(db, connection, new Detail { Id = new Guid(1,0,0,new byte[8]),
                    PrincipalId = __KA__, Payload = __OR__ });
                await DetailOrm.InsertAsync(db, connection, new Detail { Id = new Guid(2,0,0,new byte[8]),
                    PrincipalId = __KB__, Payload = __OR__ });
            }
            public static async Task<bool> Read(OrmContext db, DbConnection connection)
            {
                var first = await Pair(db, connection, __KA__, new Guid(1,0,0,new byte[8]), __RA__);
                var second = await Pair(db, connection, __KB__, new Guid(2,0,0,new byte[8]), __RB__);
                return first && second;
            }
            private static async Task<bool> Pair(OrmContext db, DbConnection connection, string key,
                Guid ownerId, string payload)
            {
                var principal = (await PrincipalOrm.GetAsync(db, connection, key))!;
                var owner = (await DetailOrm.GetAsync(db, connection, ownerId))!;
                await DetailOrm.LoadParentAsync(db, connection, owner);
                await PrincipalOrm.LoadDetailAsync(db, connection, principal);
                _known = principal;
                return principal.Id == key && principal.Payload == payload && owner.PrincipalId == key
                    && owner.Parent?.Id == key && owner.Parent.Payload == payload
                    && principal.Detail?.Id == ownerId && principal.Detail.Payload == __OR__;
            }
            public static async Task<Exception> Refuse(OrmContext db, DbConnection connection)
            {
                var old = _known.Detail;
                try { await PrincipalOrm.LoadDetailAsync(db, connection, _known); }
                catch (Exception error)
                {
                    if (!ReferenceEquals(old, _known.Detail)) throw new Exception("Navigation was overwritten");
                    return error;
                }
                throw new Exception("Expected a refusal");
            }
        }
        """;
}
