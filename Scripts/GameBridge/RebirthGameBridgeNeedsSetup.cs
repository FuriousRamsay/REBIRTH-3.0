using Newtonsoft.Json.Linq;

// Explicit disposable fixture preparation, never gameplay qualification.
internal static class RebirthGameBridgeNeedsSetup
{
    internal static bool TryApply(BridgeRequest request,EntityPlayerLocal player,out JObject result)
    {
        result=null;
        if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||player.world==null||player.world.IsRemote()||
            !RebirthMetabolismStateRepository.IsServerAuthority)
        {request.Fail("needs=1 setup requires the local disposable CodexTest save and authoritative metabolism.",403);return false;}
        RebirthMetabolismService.EnsurePlayerReady(player);
        var state=RebirthMetabolismStateRepository.GetOrCreate(player);
        if(state==null){request.Fail("Authoritative metabolism state unavailable.",409);return false;}
        int cleared=state.IngestionEntries.Count;
        // Same native reserve/state APIs used by rbmet energytest. Explicit
        // needs=1 authorizes clearing digestion only in this disposable save.
        state.IngestionEntries.Clear();
        player.Stats.Water.Value=100f;player.Stats.Food.Value=100f;
        state.Energy=RebirthMetabolismConfig.EnergyMax;
        RebirthMetabolismService.ClampEnergyToReserveCeiling(player,state);
        state.Touch();RebirthMetabolismService.SendSnapshotToOwner(player,true);
        string error;
        bool saved=RebirthMetabolismStateRepository.TrySave("bridge-disposable-needs-setup",out error);
        result=new JObject { ["fixtureOnly"]=true,["scope"]="local-disposable-CodexTest",["hydration"]=player.Stats.Water.Value,
            ["nutrition"]=player.Stats.Food.Value,["energy"]=state.Energy,["digestionEntriesCleared"]=cleared,
            ["metabolismSaved"]=saved,["nativeStatsPersistence"]="normal-native-player-save" };
        // Expose persistence refusal without presenting setup as saved.
        if(!saved){result["persistenceError"]=error;request.Complete(new JObject{["ok"]=false,["error"]="Metabolism setup persistence refused.",["needsSetup"]=result});return false;}
        return true;
    }
}
