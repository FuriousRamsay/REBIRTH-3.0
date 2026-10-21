using System;

internal static partial class RebirthCookingOwnerDelta
{
    // Only an authenticated terminal response proving saved credit AND saved
    // source removal reaches this metadata path. Inventory movement since that
    // positive server observation cannot require granting the original again.
    internal static bool RetireAuthenticatedTerminal(EntityPlayerLocal player,Plan plan,Func<bool> terminalCurrent)
    {
        try
        {
            if(player==null||plan==null||terminalCurrent==null||plan.Deaths<0||permit!=null)return false;
            if(plan.Deaths<player.Died)return RetireHistoricalMetadata(player,plan,terminalCurrent);
            if(plan.Deaths!=player.Died||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||identity.CanonicalId!=plan.Owner)return false;
            string image=Encode(plan);var world=player.world;string directory=GameIO.GetSaveGameDir();
            if(Attempts.TryGetValue(plan.Operation,out var old)&&(old.Image!=image||old.Plan.Operation!=plan.Operation||old.Deaths!=plan.Deaths||old.Directory!=directory))return false;
            var scope=new Attempt{Plan=plan,Image=image,Player=player,World=world,Directory=directory,Bag=player.bag.ItemGrid,Belt=player.inventory.ItemGrid,Current=terminalCurrent,Deaths=player.Died};
            bool Still()=>permit==null&&Current(scope)&&ReferenceEquals(player.world,world)&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var now)&&now.CanonicalId==plan.Owner&&
                ReferenceEquals(GameManager.Instance?.World,world)&&GameIO.GetSaveGameDir()==directory&&permit==null;
            if(!Still()||!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.deaths!=plan.Deaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float phase,out bool present)||!present||(phase!=2f&&phase!=3f)||!Still())return false;
            float live=player.Buffs.GetCustomVar(plan.Receipt);if(live!=2f&&live!=3f)return false;
            if(phase!=3f||live!=3f)
            {
                if(!Still())return false;player.Buffs.SetCustomVar(plan.Receipt,3f,true);
                if(!Still())return false;GameManager.Instance.SaveLocalPlayerData();
            }
            if(!Still()||!Saved(identity,plan,3))return false;
            if(Attempts.TryGetValue(plan.Operation,out var retained))
            {if(retained.Image!=image||retained.Deaths!=plan.Deaths||retained.Directory!=directory)return false;Attempts.Remove(plan.Operation);}
            return Still();
        }
        catch{return false;}
    }
}
