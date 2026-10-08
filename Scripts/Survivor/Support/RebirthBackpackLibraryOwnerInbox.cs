using System;

// Authenticated dispatcher supplies current creation and exact connection session.
// Receiving an offer never applies inventory or releases the custody reservation.
public sealed class RebirthBackpackLibraryOwnerInbox
{
    private readonly EntityPlayerLocal owner;
    private readonly object world,session;
    private readonly string creation;
    private readonly RebirthBackpackLibraryOfferInbox inbox;
    public RebirthBackpackLibraryOwnerInbox(EntityPlayerLocal player,Guid creationId,object authenticatedSession) : this(player,creationId.ToString("N"),authenticatedSession) {}
    public RebirthBackpackLibraryOwnerInbox(EntityPlayerLocal player,string creationId,object authenticatedSession)
    {
        if(player==null||player.world==null||!RebirthSurvivorRequestScope.TryNormalize(creationId,out var normalized)||authenticatedSession==null)
            throw new ArgumentException("Owner inbox requires a current owner, creation and session.");
        owner=player;world=player.world;creation=normalized;session=authenticatedSession;
        inbox=new RebirthBackpackLibraryOfferInbox(session);
    }
    private bool Current(EntityPlayerLocal player,string currentCreation,object currentSession)
        =>ReferenceEquals(player,owner)&&ReferenceEquals(session,currentSession)&&RebirthSurvivorRequestScope.Matches(creation,currentCreation)&&
        GameManager.Instance!=null&&ReferenceEquals(world,GameManager.Instance.World)&&
        ReferenceEquals(world,owner.world)&&ReferenceEquals(owner.world.GetEntity(owner.entityId),owner)&&
        owner.IsSpawned()&&!owner.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(owner)&&
        RebirthSurvivorMode.IsEnabledForCurrentWorld();
    public bool Receive(EntityPlayerLocal player,Guid currentCreation,object currentSession,RebirthBackpackLibraryOfferChunk chunk)
        =>Receive(player,currentCreation.ToString("N"),currentSession,chunk);
    public bool Receive(EntityPlayerLocal player,string currentCreation,object currentSession,RebirthBackpackLibraryOfferChunk chunk)
        =>Current(player,currentCreation,currentSession)&&chunk!=null&&chunk.CreationId==creation&&chunk.Deliver(inbox,session);
    public bool TryGetOffer(EntityPlayerLocal player,Guid currentCreation,object currentSession,Guid transaction,
        out RebirthBackpackLibraryReceipt receipt)=>TryGetOffer(player,currentCreation.ToString("N"),currentSession,transaction,out receipt);
    public bool TryGetOffer(EntityPlayerLocal player,string currentCreation,object currentSession,Guid transaction,
        out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;return Current(player,currentCreation,currentSession)&&inbox.TryFinish(session,creation,transaction,out receipt);
    }
    // Only authenticated settlement or actual session teardown may call this transport cleanup.
    public bool Clear(object currentSession,Guid transaction)
        =>ReferenceEquals(session,currentSession)&&inbox.Clear(session,creation,transaction);
}