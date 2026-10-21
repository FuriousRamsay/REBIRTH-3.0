internal static partial class RebirthHydrationOwnerHold
{
    // Freezes an already Applied native result, never authorizes a native write.
    // Only an actual cold owner observation may enter; no saved description alone.
    internal static bool TryAcquireColdApplied(RebirthHydrationColdOwnerWitness witness)
    {
        try
        {
            if(witness==null||!witness.Current())return false;
            var player=witness.Player;var original=witness.Original;
            if(entries.Count>=64||entries.ContainsKey(player)||!Scope(player,original)||!CanAcquire(player)||
                !original.TryGetPlan(out var plan)||!Capture(player,original,out var snapshot)||
                !plan.MatchesAppliedInventory(snapshot.Bag,snapshot.Belt))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var entry=new Entry{Player=player,World=player.world,Game=GameManager.Instance,Manager=manager,Peer=null,
                Save=DirectoryPath(GameIO.GetSaveGameDir()),PlayerData=DirectoryPath(GameIO.GetPlayerDataDir()),
                Original=original,Bag=player.bag,Belt=player.inventory};
            if(!Current(entry)||!CanAcquire(player)||!witness.Current())return false;
            entries.Add(player,entry);return true;
        }
        catch{return false;}
    }
}
