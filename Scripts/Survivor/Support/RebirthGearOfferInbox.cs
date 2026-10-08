using System;

// One offer per authenticated owner session. This transport has no native custody
// effects; expiry/reset must never cancel a persisted transfer or release a hold.
public sealed class RebirthGearOfferInbox
{
    public const double AssemblyLifetimeSeconds=30;
    private readonly object owner,world,session;
    private readonly string creation;
    private RebirthGearOfferAssembly assembly;
    private Guid transaction;
    private long revision;
    private int length;
    private string digest;
    private double started;
    private bool delivered;
    public RebirthGearOfferInbox(object owner,object world,object session,string creation)
    {
        if(owner==null||world==null||session==null||!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized))
            throw new ArgumentException("Invalid gear inbox scope.");
        this.owner=owner;this.world=world;this.session=session;this.creation=normalized;
    }
    public bool TryReceive(object currentOwner,object currentWorld,object currentSession,string currentCreation,
        double now,RebirthGearOfferFragment fragment,out RebirthGearTransferState offer)
    {
        offer=null;
        if(!Current(currentOwner,currentWorld,currentSession,currentCreation)||double.IsNaN(now)||double.IsInfinity(now)||now<0||fragment==null||
            fragment.CreationId!=creation)return false;
        if(assembly!=null&&!delivered&&(now<started||now-started>=AssemblyLifetimeSeconds))ClearAssembly();
        if(assembly==null)
        {
            transaction=fragment.TransactionId;revision=fragment.ExpectedRevision;length=fragment.TotalBytes;digest=fragment.Digest;started=now;
            assembly=new RebirthGearOfferAssembly(creation,transaction,revision,length,digest);
        }
        // A different transfer cannot evict a complete or partially received offer.
        if(fragment.TransactionId!=transaction||fragment.ExpectedRevision!=revision||fragment.TotalBytes!=length||fragment.Digest!=digest)return false;
        if(!assembly.TryAdd(fragment)||delivered||!assembly.TryFinish(out var completed))return false;
        delivered=true;offer=completed;return true;
    }
    private bool Current(object currentOwner,object currentWorld,object currentSession,string currentCreation)
        =>ReferenceEquals(owner,currentOwner)&&ReferenceEquals(world,currentWorld)&&ReferenceEquals(session,currentSession)&&
            RebirthSurvivorRequestScope.Matches(creation,currentCreation);
    public bool MatchesCompleted(object currentOwner,object currentWorld,object currentSession,string currentCreation,Guid transfer)
        =>delivered&&transfer==transaction&&Current(currentOwner,currentWorld,currentSession,currentCreation);
    // Caller has verified the authoritative saved settlement. This does not release
    // native owner reservation; that service independently verifies its own witness.
    public bool TryReleaseSettled(object currentOwner,object currentWorld,object currentSession,string currentCreation,
        Guid settledTransaction,Func<bool> verifySavedSettlement)
    {
        if(!delivered||settledTransaction!=transaction||verifySavedSettlement==null||!Current(currentOwner,currentWorld,currentSession,currentCreation))return false;
        try{if(!verifySavedSettlement()||!Current(currentOwner,currentWorld,currentSession,currentCreation)||settledTransaction!=transaction)return false;ClearAssembly();return true;}
        catch{return false;}
    }
    private void ClearAssembly(){assembly=null;transaction=Guid.Empty;revision=0;length=0;digest=null;started=0;delivered=false;}
}