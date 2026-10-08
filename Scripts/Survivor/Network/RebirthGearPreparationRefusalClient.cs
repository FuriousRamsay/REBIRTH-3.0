using System;
using System.Xml.Linq;

// Session-bound receipt only; no inventory mutation, save or hold release.
internal static class RebirthGearPreparationRefusalClient
{
    private sealed class Entry
    {internal EntityPlayerLocal Player;internal World World;internal object Session;internal Guid SavedWorld;internal RebirthGearPreparationRefusal Refusal;internal bool Acknowledged;}
    private static Entry retained;
    private static bool Resolve(World world,int id,out EntityPlayerLocal player,out object session,out Guid savedWorld)
    {
        player=null;session=null;savedWorld=Guid.Empty;
        var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world==null||!world.IsRemote()||!ReferenceEquals(game?.World,world)||
            manager==null||manager.IsServer||manager.connectionToServer==null||manager.connectionToServer.Length==0||
            manager.connectionToServer[0]==null||manager.connectionToServer[0].IsDisconnected()||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out savedWorld)||savedWorld==Guid.Empty)return false;
        player=world.GetPrimaryPlayer();
        if(player==null||player.entityId!=id||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(id),player)||
            !player.IsSpawned()||player.IsDead()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        session=manager.connectionToServer[0];return true;
    }
    internal static bool Receive(World world,int id,RebirthGearPreparationRefusal refusal)
        =>ReceiveCore(world,id,refusal,false);
    internal static bool ReceiveAcknowledged(World world,int id,RebirthGearPreparationRefusal refusal)
        =>ReceiveCore(world,id,refusal,true);
    private static bool ReceiveCore(World world,int id,RebirthGearPreparationRefusal refusal,bool acknowledged)
    {
        try
        {
            if(!Resolve(world,id,out var player,out var session,out var savedWorld)||
                !RebirthGearPreparationRefusalWireCodec.TryEncode(refusal,out var bytes)||
                !RebirthGearPreparationRefusalWireCodec.TryDecode(bytes,out var copy)||copy.SavedWorld!=savedWorld||
                !RebirthSurvivorRequestScope.Matches(copy.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner==null||
                GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=owner.CanonicalId)return false;
            var old=retained;
            if(old!=null&&ReferenceEquals(old.Player,player)&&ReferenceEquals(old.World,world)&&ReferenceEquals(old.Session,session)&&
                old.SavedWorld==savedWorld&&old.Refusal.TransactionId==copy.TransactionId)
            {
                if(!XNode.DeepEquals(old.Refusal.Write(),copy.Write()))return false;
                if(!acknowledged||old.Acknowledged)
                    return Resolve(world,id,out var duplicatePlayer,out var duplicateSession,out var duplicateWorld)&&
                        ReferenceEquals(duplicatePlayer,player)&&ReferenceEquals(duplicateSession,session)&&duplicateWorld==savedWorld;
            }
            if(!RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(owner,savedWorld,out var marker,out var intent,out var saved,out var phase)||
                phase!=(acknowledged?-1f:0f)||!copy.MatchesOriginal(marker)||!intent.MatchesInventory(saved)||
                !RebirthGearOwnerReservation.MatchesUnpreparedIntent(player,session,intent)||
                !RebirthGearPreparationMarker.TryRead(marker,1f,out _,out var owned,out _)||
                player.Buffs==null||player.Buffs.GetCustomVar(marker)!=1f||player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=(acknowledged?-1f:0f)||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,owned,out var live)||
                !intent.MatchesInventory(live)||!Resolve(world,id,out var current,out var currentSession,out var currentWorld)||
                !ReferenceEquals(current,player)||!ReferenceEquals(currentSession,session)||currentWorld!=savedWorld||
                !RebirthGearOwnerReservation.MatchesUnpreparedIntent(player,session,intent))return false;
            retained=new Entry{Player=player,World=world,Session=session,SavedWorld=savedWorld,Refusal=copy,Acknowledged=acknowledged};return true;
        }
        catch{return false;}
    }
    internal static bool TryGetCurrent(EntityPlayerLocal player,object expectedSession,out RebirthGearPreparationRefusal refusal)
    {
        refusal=null;
        try
        {
            var entry=retained;
            if(player==null||entry==null||!ReferenceEquals(entry.Player,player)||!ReferenceEquals(entry.Session,expectedSession)||
                !Resolve(entry.World,player.entityId,out var current,out var session,out var world)||
                !ReferenceEquals(current,player)||!ReferenceEquals(session,entry.Session)||world!=entry.SavedWorld||
                !RebirthSurvivorRequestScope.Matches(entry.Refusal.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player)))return false;
            refusal=entry.Refusal;return true;
        }
        catch{return false;}
    }
    internal static bool TryGetAcknowledged(EntityPlayerLocal player,object session,out RebirthGearPreparationRefusal refusal)
        =>TryGetCurrent(player,session,out refusal)&&retained.Acknowledged&&ReferenceEquals(retained.Refusal,refusal);
    internal static void Reset(){retained=null;}
}