using System;
using System.Diagnostics;

// Authenticated native-session transport. Never reapplies inventory; a verified
// cached terminal can finish its saved retirement once the original is assembled.
public static class RebirthGearOfferClient
{
    private static EntityPlayerLocal owner;
    private static World ownerWorld;
    private static object session;
    private static string creation;
    private static RebirthGearOfferInbox inbox;
    private static RebirthGearTransferState complete;
    private sealed class SettledOriginal { internal EntityPlayerLocal Player; internal World World; internal object Session; internal string Creation; internal RebirthGearTransferState Offer; }
    private static SettledOriginal settled;
    private static bool Resolve(World world,int playerId,out EntityPlayerLocal player,out object connection,out string current)
    {
        player=null;connection=null;current=null;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||manager==null||manager.IsServer)return false;
        player=world.GetPrimaryPlayer();var native=manager.connectionToServer;
        if(player==null||player.entityId!=playerId||!ReferenceEquals(player.world,world)||
            !ReferenceEquals(world.GetEntity(playerId),player)||!player.IsSpawned()||player.IsDead()||native==null||native.Length==0||
            native[0]==null||native[0].IsDisconnected()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            !RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out current))return false;
        connection=native[0];return true;
    }
    public static bool Receive(World world,int playerId,RebirthGearOfferFragment fragment)
    {
        if(fragment==null||!Resolve(world,playerId,out var player,out var connection,out var current)||fragment.CreationId!=current)return false;
        if(!ReferenceEquals(owner,player)||!ReferenceEquals(ownerWorld,world)||!ReferenceEquals(session,connection)||creation!=current)
        {
            // An outcome can precede fragments. Preserve only its authenticated
            // CURRENT native scope; reset unrelated session authority normally.
            if(!RebirthGearTerminalClient.TryGetCurrent(player,out _,out _))RebirthGearTerminalClient.Reset();
            ResetTransport();owner=player;ownerWorld=world;session=connection;creation=current;
            inbox=new RebirthGearOfferInbox(player,world,connection,current);
        }
        if(!inbox.TryReceive(player,world,connection,current,Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency,fragment,out var offer))return false;
        complete=offer;TryFinishCurrentTerminal(world,playerId);return true;
    }
    public static bool TryGetCurrentOffer(World world,int playerId,out RebirthGearTransferState offer)
    {
        offer=null;
        if(complete==null||!Resolve(world,playerId,out var player,out var connection,out var current)||
            !ReferenceEquals(owner,player)||!ReferenceEquals(ownerWorld,world)||!ReferenceEquals(session,connection)||creation!=current)return false;
        offer=complete;return true;
    }
    // Explicit dispatcher step; receiving fragments never applies items. A refused
    // or uncertain result must not clear the saved offer or release native custody.
    public static RebirthGearOwnerApplySequence.Result TryApplyCurrentOffer(World world,int playerId)
    {
        if(!TryGetCurrentOffer(world,playerId,out var offer)||
            !Resolve(world,playerId,out var player,out var connection,out var current))
            return complete==null?RebirthGearOwnerApplySequence.Result.Conflict:RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
        Func<bool> isCurrent=()=>Resolve(world,playerId,out var live,out var liveConnection,out var liveCreation)&&
            ReferenceEquals(live,player)&&ReferenceEquals(liveConnection,connection)&&liveCreation==current&&
            ReferenceEquals(owner,player)&&ReferenceEquals(ownerWorld,world)&&ReferenceEquals(session,connection)&&
            creation==current&&ReferenceEquals(complete,offer);
        try
        {
            if(!isCurrent()||!RebirthGearOwnerReservation.TryAcquire(player,connection,offer))
                return RebirthGearOwnerApplySequence.Result.Pending;
            return RebirthGearOwnerInventoryAdapter.Apply(player,offer,connection,isCurrent);
        }
        catch{return RebirthGearOwnerApplySequence.Result.Pending;}
    }
    // Scheduling uploads is not an acknowledgment or permission to release custody.
    public static bool RequestAppliedCheckpoint(World world,int playerId)
    {
        if(!TryGetCurrentOffer(world,playerId,out var offer)||
            !Resolve(world,playerId,out var player,out var connection,out var current))return false;
        var game=GameManager.Instance;
        Func<bool> isCurrent=()=>ReferenceEquals(GameManager.Instance,game)&&
            TryGetCurrentOffer(world,playerId,out var retained)&&ReferenceEquals(retained,offer)&&
            Resolve(world,playerId,out var live,out var liveConnection,out var liveCreation)&&
            ReferenceEquals(live,player)&&ReferenceEquals(liveConnection,connection)&&liveCreation==current&&
            RebirthGearOwnerReservation.Matches(player,connection,offer);
        try
        {
            if(!isCurrent()||player.Buffs==null||player.Buffs.GetCustomVar("rbGear_"+offer.TransactionId)!=1f||
                !offer.TryGetPlan(out var plan)||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,4,out var snapshot)||
                !plan.MatchesAppliedInventory(snapshot.Bag,snapshot.Belt)||
                !RebirthGearOwnerSaveCheckpoint.TryPersist(player,offer,RebirthGearOwnerReceipt.Applied,isCurrent)||!isCurrent())return false;
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            if(!isCurrent())return false;
            game.TriggerSendOfLocalPlayerDataFile(0f);
            return isCurrent();
        }
        catch{return false;}
    }
    public static bool RequestAppliedConfirmation(World world,int playerId)
    {
        if(!TryGetCurrentOffer(world,playerId,out var offer)||
            !Resolve(world,playerId,out var player,out var connection,out var current))return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearAppliedRequest));
            if(!RequestAppliedCheckpoint(world,playerId)||
                !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||
                !TryGetCurrentOffer(world,playerId,out var retained)||!ReferenceEquals(retained,offer)||
                !Resolve(world,playerId,out var live,out var liveConnection,out var liveCreation)||
                !ReferenceEquals(live,player)||!ReferenceEquals(liveConnection,connection)||liveCreation!=current||
                !RebirthGearOwnerReservation.Matches(player,connection,offer))return false;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthGearAppliedRequest>().Setup(playerId,current,Guid.ParseExact(offer.TransactionId,"N")));
            // Queued request only. Native upload may arrive later; no hold release.
            return true;
        }
        catch{return false;}
    }
    // Explicit producer for untouched rejection; never infer rejection after Applying.
    public static bool TryRejectCurrentOffer(World world,int playerId)
    {
        if(!TryGetCurrentOffer(world,playerId,out var offer)||
            !Resolve(world,playerId,out var player,out var connection,out var current))return false;
        Func<bool> isCurrent=()=>TryGetCurrentOffer(world,playerId,out var retained)&&ReferenceEquals(retained,offer)&&
            Resolve(world,playerId,out var live,out var liveConnection,out var liveCreation)&&ReferenceEquals(live,player)&&
            ReferenceEquals(liveConnection,connection)&&liveCreation==current&&RebirthGearOwnerReservation.Matches(player,connection,offer);
        try
        {
            if(!RebirthGearOwnerReservation.TryAcquire(player,connection,offer)||!isCurrent())return false;
            return RebirthGearOwnerSaveCheckpoint.TryPersist(player,offer,RebirthGearOwnerReceipt.Rejected,isCurrent)&&isCurrent();
        }
        catch{return false;}
    }
    public static bool RequestRejectedConfirmation(World world,int playerId)
    {
        if(!TryGetCurrentOffer(world,playerId,out var offer)||
            !Resolve(world,playerId,out var player,out var connection,out var current))return false;
        var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        Func<bool> isCurrent=()=>ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&
            TryGetCurrentOffer(world,playerId,out var retained)&&ReferenceEquals(retained,offer)&&
            Resolve(world,playerId,out var live,out var liveConnection,out var liveCreation)&&ReferenceEquals(live,player)&&
            ReferenceEquals(liveConnection,connection)&&liveCreation==current&&RebirthGearOwnerReservation.Matches(player,connection,offer);
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearRejectedRequest));
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            if(!isCurrent()||player.Buffs==null||player.Buffs.GetCustomVar("rbGear_"+offer.TransactionId)!=-1f||
                !RebirthGearOwnerSaveCheckpoint.TryPersist(player,offer,RebirthGearOwnerReceipt.Rejected,isCurrent)||!isCurrent())return false;
            game.TriggerSendOfLocalPlayerDataFile(0f);
            if(!isCurrent())return false;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthGearRejectedRequest>().Setup(playerId,current,Guid.ParseExact(offer.TransactionId,"N")));
            return isCurrent(); // Queued only; hold survives until exact saved terminal reply.
        }
        catch{return false;}
    }
    // Binds the exact retained original for terminal cleanup without applying it.
    internal static bool TryBindCurrentOffer(World world,int playerId)
    {
        try
        {
            return TryGetCurrentOffer(world,playerId,out var offer)&&
                Resolve(world,playerId,out var player,out var connection,out _)&&
                RebirthGearOwnerReservation.TryAcquire(player,connection,offer);
        }
        catch{return false;}
    }
    internal static bool TryFinishCurrentTerminal(World world,int playerId)
    {
        try
        {
            if(!TryGetCurrentOffer(world,playerId,out var offer)||offer.PreparationRequestDigest==null||
                !Resolve(world,playerId,out var player,out _,out _)||
                !RebirthGearTerminalClient.TryGetCurrent(player,out _,out var terminal)||
                terminal.TransactionId!=offer.TransactionId||terminal.CreationId!=offer.CreationId||
                terminal.GearRevision!=offer.ExpectedRevision+1||terminal.PreparationRequestDigest!=offer.PreparationRequestDigest||
                !TryBindCurrentOffer(world,playerId))return false;
            return ReceiveSettlement(world,playerId,terminal.CreationId,Guid.ParseExact(terminal.TransactionId,"N"),terminal.GearRevision,terminal.Applied);
        }
        catch{return false;}
    }
    public static bool ReceiveSettlement(World world,int playerId,string character,Guid transaction,long revision,bool applied)
    {
        if(transaction==Guid.Empty||!TryGetCurrentOffer(world,playerId,out var offer)||
            !Resolve(world,playerId,out var player,out var connection,out var current)||
            !RebirthSurvivorRequestScope.Matches(character,current)||offer.TransactionId!=transaction.ToString("N")||
            revision!=offer.ExpectedRevision+1||player.Buffs==null||
            player.Buffs.GetCustomVar("rbGear_"+offer.TransactionId)!=(applied?1f:-1f))return false;
        if(offer.PreparationRequestDigest!=null&&!RebirthGearTerminalOwnerCheckpoint.TryPersist(player,connection,offer))return false;
        var retainedInbox=inbox;
        Func<bool> currentSettlement=()=>ReferenceEquals(inbox,retainedInbox)&&
            TryGetCurrentOffer(world,playerId,out var retained)&&ReferenceEquals(retained,offer)&&
            Resolve(world,playerId,out var live,out var liveConnection,out var liveCreation)&&
            ReferenceEquals(live,player)&&ReferenceEquals(liveConnection,connection)&&liveCreation==current&&
            player.Buffs.GetCustomVar("rbGear_"+offer.TransactionId)==(applied?1f:-1f)&&
            retainedInbox!=null&&retainedInbox.MatchesCompleted(player,world,connection,current,transaction)&&
            (offer.PreparationRequestDigest==null||RebirthGearTerminalOwnerCheckpoint.HasSavedRetirement(player,offer,applied));
        // All operations below are synchronous on the native packet dispatch thread.
        // The receipt packet is sent only after server final-file verification.
        if(!currentSettlement()||!RebirthGearOwnerReservation.ReleaseSettledWithTransport(player,connection,offer,currentSettlement,
            ()=>retainedInbox.TryReleaseSettled(player,world,connection,current,transaction,currentSettlement)))return false;
        if(applied&&offer.PreparationRequestDigest!=null)settled=new SettledOriginal{Player=player,World=world,Session=connection,Creation=current,Offer=offer};
        complete=null;return true;
    }
    // Presentation evidence only, retained AFTER exact saved retirement/release.
    // It grants no item effects and never substitutes for a current native slot.
    internal static bool TryGetSettledOriginal(World world,int playerId,string transaction,out RebirthGearTransferState offer)
    {
        offer=null;var entry=settled;
        if(entry==null||entry.Offer==null||entry.Offer.TransactionId!=transaction||
            !Resolve(world,playerId,out var player,out var peer,out var current)||
            !ReferenceEquals(entry.Player,player)||!ReferenceEquals(entry.World,world)||
            !ReferenceEquals(entry.Session,peer)||entry.Creation!=current||
            !RebirthGearTerminalClient.TryGetCurrent(player,out _,out var terminal)||!terminal.Applied||
            terminal.TransactionId!=transaction||terminal.CreationId!=entry.Offer.CreationId||
            terminal.GearRevision!=entry.Offer.ExpectedRevision+1||
            terminal.PreparationRequestDigest!=entry.Offer.PreparationRequestDigest)return false;
        offer=entry.Offer;return true;
    }
    private static void ResetTransport(){owner=null;ownerWorld=null;session=null;creation=null;inbox=null;complete=null;}
    public static void Reset(){settled=null;RebirthGearTerminalClient.Reset();ResetTransport();}
}