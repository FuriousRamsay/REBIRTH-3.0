using System;using System.Collections.Generic;using System.Xml.Linq;internal static partial class FixtureReservation{    private sealed class Entry { internal object World, Session; internal Guid SavedWorld; internal RebirthGearTransferState Offer; internal RebirthGearPreparationIntent Intent; internal bool Cold; }
    private static readonly Dictionary<EntityPlayerLocal, Entry> Entries = new Dictionary<EntityPlayerLocal, Entry>();
    internal static bool MatchesIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
        =>IsHeld(player)&&intent!=null&&ReferenceEquals(Entries[player].Session,session)&&TrySavedWorld(out var world)&&world==Entries[player].SavedWorld&&Entries[player].Intent!=null&&
            XNode.DeepEquals(Entries[player].Intent.Write(),intent.Write());
    // Refusal applies only to the retained ORIGINAL with no adopted server offer.
    internal static bool MatchesUnpreparedIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
        =>MatchesIntent(player,session,intent)&&Entries[player].Offer==null&&AuthenticatedIntent(player,session,intent);
    internal static bool ReleaseRefusedOriginal(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent,Func<bool> savedRetirement)
    {
        try
        {
            if(savedRetirement==null||!MatchesUnpreparedIntent(player,session,intent))return false;
            var original=Entries[player];
            if(!savedRetirement()||!MatchesUnpreparedIntent(player,session,intent)||
                !ReferenceEquals(Entries[player],original))return false;
            if(player.Buffs==null||player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=-1f)return false;
            foreach(string key in player.Buffs.CVars.Keys)
                if(key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))return false;
            return Entries.Remove(player);
        }
        catch{return false;}
    }
    private static bool AuthenticatedIntent(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var native=manager?.connectionToServer;
        return Current(player)&&player.world.IsRemote()&&manager!=null&&!manager.IsServer&&native!=null&&native.Length>0&&
            native[0]!=null&&ReferenceEquals(native[0],session)&&!native[0].IsDisconnected()&&
            RebirthSurvivorRequestScope.Matches(intent.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player));
    }
    private static bool TrySavedWorld(out Guid world)
    {
        world=Guid.Empty;
        try {return Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out world)&&world!=Guid.Empty;}
        catch {return false;}
    }
    private static bool Current(EntityPlayerLocal player)
        => player?.world != null && GameManager.Instance != null &&
            ReferenceEquals(player.world, GameManager.Instance.World) &&
            ReferenceEquals(player.world.GetPrimaryPlayer(), player) &&
            ReferenceEquals(player.world.GetEntity(player.entityId), player);
    internal static bool IsHeld(EntityPlayerLocal player)
        => player != null && Entries.TryGetValue(player, out var entry) &&
            ReferenceEquals(entry.World, player.world) && Current(player);
}