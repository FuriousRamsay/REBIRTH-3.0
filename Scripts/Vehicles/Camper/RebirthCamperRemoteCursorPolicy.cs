// Small deterministic adapter shared by the native client and offline checks.
// Receipt 2 means a callback may have interrupted application; it is not proof
// of a debit. Only the two exact recognized cursor states can finish it.
public static class RebirthCamperRemoteCursorPolicy
{
    public enum Action { PublishCursor, PublishReceipt, Resume, Cancel, Retain }
    public static Action Decide(bool serverAcknowledged,float receipt,bool exactBefore,bool exactAfter)
    {
        if(serverAcknowledged || receipt==1f || receipt==3f)return Action.Resume;
        if(receipt==2f && exactAfter)return Action.PublishReceipt;
        if((receipt==0f || receipt==2f) && exactBefore)return Action.PublishCursor;
        if(receipt==0f)return Action.Cancel;
        return Action.Retain;
    }
}
