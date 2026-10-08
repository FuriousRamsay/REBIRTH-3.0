using System;
using System.Xml.Linq;

// Current-session cache only. No inventory write, marker removal or hold release.
internal static class RebirthGearTerminalClient
{
    private sealed class Entry {internal EntityPlayerLocal Player;internal World World;internal object Session;
        internal Guid SavedWorld;internal string Marker;internal RebirthGearSettlement Terminal;}
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
    internal static bool Receive(World world,int id,RebirthGearSettlement terminal)
    {
        try
        {
            if(terminal?.PreparationRequestDigest==null||!Resolve(world,id,out var player,out var session,out var savedWorld)||
                !RebirthSurvivorRequestScope.Matches(terminal.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity==null||
                GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId||
                !RebirthGearTerminalWireCodec.TryEncode(terminal,out var bytes)||
                !RebirthGearTerminalWireCodec.TryDecode(bytes,out var copy))return false;
            // An exact duplicate retains its original admission after the marker
            // has been durably retired. It is never a new cleanup/release proof.
            var entry=retained;
            if(entry!=null&&ReferenceEquals(entry.Player,player)&&ReferenceEquals(entry.World,world)&&
                ReferenceEquals(entry.Session,session)&&entry.SavedWorld==savedWorld&&
                entry.Terminal.TransactionId==copy.TransactionId)
                return terminal.MatchesOriginalMarker(entry.Marker)&&XNode.DeepEquals(entry.Terminal.Write(),copy.Write())&&
                    Resolve(world,id,out var duplicatePlayer,out var duplicateSession,out var duplicateWorld)&&
                    ReferenceEquals(duplicatePlayer,player)&&ReferenceEquals(duplicateSession,session)&&duplicateWorld==savedWorld;
            if(!RebirthGearPreparationPlayerFileWitness.TryRead(identity,savedWorld,out var marker,out var intent,out _)||
                !terminal.MatchesOriginalMarker(marker)||intent.CreationId!=terminal.CreationId||
                !Resolve(world,id,out var current,out var currentSession,out var currentWorld)||
                !ReferenceEquals(current,player)||!ReferenceEquals(currentSession,session)||currentWorld!=savedWorld)return false;
            retained=new Entry{Player=player,World=world,Session=session,SavedWorld=savedWorld,Marker=marker,Terminal=copy};
            return true;
        }
        catch{return false;}
    }
    internal static bool TryGetCurrent(EntityPlayerLocal player,out string marker,out RebirthGearSettlement terminal)
    {
        marker=null;terminal=null;
        try {
        var entry=retained;
        if(player==null||entry==null||!ReferenceEquals(entry.Player,player)||
            !Resolve(entry.World,player.entityId,out var current,out var session,out var savedWorld)||
            !ReferenceEquals(current,player)||!ReferenceEquals(entry.Session,session)||entry.SavedWorld!=savedWorld||
            !RebirthSurvivorRequestScope.Matches(entry.Terminal.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player)))return false;
        marker=entry.Marker;terminal=entry.Terminal;return true;
        } catch {marker=null;terminal=null;return false;}
    }
    internal static void Reset(){retained=null;}
}