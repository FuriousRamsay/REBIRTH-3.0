using System;

// One inbox per authenticated connection/session. Transport cleanup is not custody cancellation.
public sealed class RebirthBackpackLibraryOfferInbox
{
    private readonly object session;
    private string creation;
    private Guid transaction;
    private int length;
    private RebirthBackpackLibraryOfferAssembly assembly;
    public RebirthBackpackLibraryOfferInbox(object authenticatedSession)
    {session=authenticatedSession??throw new ArgumentNullException(nameof(authenticatedSession));}
    public bool TryBegin(object currentSession,Guid creationId,Guid transactionId,int totalBytes)
        =>TryBegin(currentSession,creationId.ToString("N"),transactionId,totalBytes);
    public bool TryBegin(object currentSession,string creationId,Guid transactionId,int totalBytes)
    {
        if(!ReferenceEquals(session,currentSession)||!RebirthSurvivorRequestScope.TryNormalize(creationId,out var normalized)||transactionId==Guid.Empty||
            totalBytes<=0||totalBytes>RebirthBackpackLibraryWireCodec.MaxBytes)return false;
        if(assembly!=null)return RebirthSurvivorRequestScope.Matches(creation,creationId)&&transaction==transactionId&&length==totalBytes;
        assembly=new RebirthBackpackLibraryOfferAssembly(creationId,transactionId,totalBytes);
        creation=normalized;transaction=transactionId;length=totalBytes;return true;
    }
    public bool TryAdd(object currentSession,Guid creationId,Guid transactionId,int index,byte[] bytes)
        =>TryAdd(currentSession,creationId.ToString("N"),transactionId,index,bytes);
    public bool TryAdd(object currentSession,string creationId,Guid transactionId,int index,byte[] bytes)
        =>Matches(currentSession,creationId,transactionId)&&assembly.TryAdd(index,bytes);
    public bool TryFinish(object currentSession,Guid creationId,Guid transactionId,out RebirthBackpackLibraryReceipt receipt)
        =>TryFinish(currentSession,creationId.ToString("N"),transactionId,out receipt);
    public bool TryFinish(object currentSession,string creationId,Guid transactionId,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;return Matches(currentSession,creationId,transactionId)&&assembly.TryFinish(out receipt);
    }
    private bool Matches(object currentSession,string creationId,Guid transactionId)
        =>ReferenceEquals(session,currentSession)&&assembly!=null&&RebirthSurvivorRequestScope.Matches(creation,creationId)&&transaction==transactionId;
    // Caller must verify settlement or session teardown. This only releases received bytes.
    public bool Clear(object currentSession,Guid creationId,Guid transactionId)
        =>Clear(currentSession,creationId.ToString("N"),transactionId);
    public bool Clear(object currentSession,string creationId,Guid transactionId)
    {
        if(!Matches(currentSession,creationId,transactionId))return false;
        assembly=null;creation=null;transaction=Guid.Empty;length=0;return true;
    }
}