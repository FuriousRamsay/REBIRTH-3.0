using System;

// Recipient presentation intent uses native saved/synchronized player CVars.
// It never grants milestone credit, rewards or entry permissions.
internal static class RebirthPurgeTeachingIntent
{
    private static string Key(string id)
    {
        switch(id)
        {
            case "PurgeIntro":case "PurgeSupplies":case "PurgeScanner":case "PurgeSupplyUpdate":
            case "PurgeDiscovery":case "PurgeDesert":case "PurgeSnow":
            case "PurgeWasteland":case "PurgeBurntForest":
                return "%rbPurgeLiveTeaching_"+id;
            default:return null;
        }
    }
    private static bool Owns(EntityPlayerLocal player)=>player!=null&&
        RebirthPurgeReleasePolicy.Enabled&&RebirthSandboxOptionManager.Current.IsPurge&&
        ReferenceEquals(player.world,GameManager.Instance?.World)&&
        ReferenceEquals(player.world?.GetPrimaryPlayer(),player);
    internal static bool Pending(EntityPlayerLocal player,string id)
    {
        string key=Key(id);
        return key!=null&&Owns(player)&&player.Buffs.GetCustomVar(key)==1f;
    }
    internal static bool ManualPending(EntityPlayerLocal player,string id)
    {
        string key=Key(id);
        return key!=null&&Owns(player)&&player.Buffs.GetCustomVar(key)==2f;
    }
    internal static void Retain(EntityPlayerLocal player,string id)
    {
        // Automatic milestone work must not downgrade an explicit reread.
        if(id!="PurgeDiscovery"&&id!="PurgeDesert"&&id!="PurgeSnow"&&id!="PurgeWasteland"&&id!="PurgeBurntForest")return;
        if(!ManualPending(player,id))Set(player,id,1f);
    }
    internal static void RetainManual(EntityPlayerLocal player,string id)=>Set(player,id,2f);
    internal static void Retire(EntityPlayerLocal player,string id)=>Set(player,id,0f);
    private static void Set(EntityPlayerLocal player,string id,float value)
    {
        string key=Key(id);
        if(key!=null&&Owns(player)&&player.Buffs.GetCustomVar(key)!=value)
            player.Buffs.SetCustomVar(key,value,true);
    }
}
