using System;
// At most one pending summary. It becomes visible only after the existing map
// receiver publishes the same native-authenticated request/world/session.
internal sealed class RebirthPurgeObjectiveClient
{
    private readonly Guid request;
    private RebirthPurgeObjectiveFrame pending;
    internal RebirthPurgeObjectiveFrame Published { get; private set; }
    internal RebirthPurgeObjectiveClient(Guid nonce) { if(nonce==Guid.Empty)throw new ArgumentException("Missing request.");request=nonce; }
    internal bool Accept(RebirthPurgeObjectiveFrame frame,Guid world,Guid session)
    {
        if(frame==null || frame.Request!=request)return false;
        if(world!=Guid.Empty && (frame.World!=world || frame.Session!=session))return false;
        long sequence=pending==null?(Published==null?0:Published.Sequence):pending.Sequence;
        if(frame.Sequence<=sequence)return false;
        if(Published!=null && (frame.Revision<Published.Revision || frame.Generation<Published.Generation))return false;
        pending=frame;Publish(world,session);return true;
    }
    internal void Publish(Guid world,Guid session)
    {
        if(world==Guid.Empty || session==Guid.Empty || pending==null)return;
        if(pending.World!=world || pending.Session!=session) { pending=null;return; }
        Published=pending;pending=null;
    }
}