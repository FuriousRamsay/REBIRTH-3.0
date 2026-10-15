using System;
using System.Collections.Generic;

// Completed consumption only. Native player CVars carry pending receipts through
// death/reconnect; clinical observations remain in their existing life-bound queue.
internal static class RebirthHealingDoseCheckpoint
{
    private const string Prefix="rbHealingDose:";
    private const int Capacity=64;
    private static EntityPlayer observedPlayer;
    private static float nextRestore;
    internal static void Reset(){observedPlayer=null;nextRestore=0;}
    internal static void Retain(RebirthSelfMedicalPracticeAwards.Claim claim)
    {
        if(claim?.Scope==null||!claim.Scope.Current()||!Guid.TryParseExact(claim.Receipt,"N",out var receipt)||receipt==Guid.Empty)return;
        string key=Key(claim.Scope,claim.Item,receipt.ToString("N"));
        var buffs=claim.Scope.Player.Buffs;
        if(buffs.GetCustomVar(key)==1f)return;
        int count=0;
        // Admission/recovery are scoped to this verified character definition.
        // Retained markers from an older scope must not consume its 64 slots.
        string currentPrefix=Prefix+claim.Scope.Creation+":"+claim.Scope.Definition+":";
        foreach(var entry in buffs.CVars)
            if(entry.Value==1f&&entry.Key.StartsWith(currentPrefix,StringComparison.OrdinalIgnoreCase))count++;
        if(count>=Capacity)return;
        RebirthSelfMedicalSimulationOwner.Set(buffs,key,1f);
    }
    internal static void Retire(RebirthSelfMedicalPracticeAwards.Claim claim)
    {
        if(claim?.Scope?.Player?.Buffs==null)return;
        var player=claim.Scope.Player;
        if(!ReferenceEquals(GameManager.Instance?.World,claim.Scope.World)||
            !ReferenceEquals(player.world,claim.Scope.World)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player))return;
        string key=Key(claim.Scope,claim.Item,claim.Receipt);
        if(player.Buffs.GetCustomVar(key)!=0f)RebirthSelfMedicalSimulationOwner.Set(player.Buffs,key,0f);
        // SET zero is sent reliably before removing the local dynamic key.
        // Native full saves omit zero values; avoid accumulating one key per dose.
        player.Buffs.RemoveCustomVar(key);
        player.Buffs.CVarsLastNetSync.Remove(key);
    }
    internal static void Restore(EntityPlayer player)
    {
        if(!ReferenceEquals(observedPlayer,player)){observedPlayer=player;nextRestore=0;}
        if(UnityEngine.Time.time<nextRestore)return;
        nextRestore=UnityEngine.Time.time+1f;
        var scope=RebirthSelfMedicalPracticeAwards.Capture(player);
        if(scope==null)return;
        RebirthSelfMedicalPracticeAwards.PrepareDoseRecovery(scope);
        // Reserve/commit changes queue state, not this native dictionary; use a
        // bounded detached list because submission may immediately retire a CVar.
        var recovered=new List<KeyValuePair<string,string>>();
        foreach(var entry in player.Buffs.CVars)
        {
            if(entry.Value!=1f||!entry.Key.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase))continue;
            string[] parts=entry.Key.Substring(Prefix.Length).Split(':');
            if(parts.Length!=4||!RebirthSurvivorRequestScope.Matches(parts[0],scope.Creation)||
                !string.Equals(parts[1],scope.Definition,StringComparison.OrdinalIgnoreCase)||
                !Guid.TryParseExact(parts[2],"N",out var receipt)||receipt==Guid.Empty||
                !RebirthDifficultyPractice.HasTreatment(parts[3]))continue;
            recovered.Add(new KeyValuePair<string,string>(parts[3],receipt.ToString("N")));
            if(recovered.Count>=Capacity)break;
        }
        foreach(var dose in recovered)
        {
            var reservation=RebirthSelfMedicalPracticeAwards.Reserve(scope,dose.Key,dose.Value);
            if(reservation!=null){RebirthSelfMedicalPracticeAwards.Submit(reservation,0f);RebirthSelfMedicalPracticeAwards.Release(reservation);}
        }
    }
    private static string Key(RebirthSelfMedicalPracticeAwards.OwnerScope scope,string item,string receipt)
        =>Prefix+scope.Creation+":"+scope.Definition+":"+receipt+":"+item;
}
