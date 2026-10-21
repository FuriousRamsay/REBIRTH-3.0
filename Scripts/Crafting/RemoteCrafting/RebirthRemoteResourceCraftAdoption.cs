using System;
using System.Linq;

// Historical status/no-use only. An ended-life entry is never resumed.
internal static class RebirthRemoteResourceCraftAdoption
{
    internal static bool QueryScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(RebirthRemoteResourceClientOriginal.Current(player,original))return true;
        return ThreadManager.IsMainThread()&&player?.world!=null&&player.IsSpawned()&&!player.IsDead()&&original?.Kind==RemoteResourceClientOperation.Craft&&
            player.Died>original.Deaths&&original.Deaths>=0&&RebirthRemoteResourceCraftOriginal.ValidateInput(original,out _,out _,out _,out _,out _)&&
            ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
            player.world.worldState.Guid==original.World&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity.CanonicalId==original.Owner;
    }
    internal static bool HostScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>
        original?.Kind==RemoteResourceClientOperation.Craft&&RebirthRemoteResourceCraftOriginal.ValidateInput(original,out _,out _,out _,out _,out _)&&
        RebirthRemoteResourceRepairAdoption.HostScope(actor,original);
    // The caller has already requalified the host's exact saved native10
    // admission. This reads that original only; Resolve/new source admission is
    // unavailable across life. Existing no-use/refund architecture owns credit.
    internal static bool Read(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourceDurableJournal.Record result)
    {
        result=null;
        try
        {
            if(!HostScope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,operation,out var root,out bool unknown)||unknown||root==null||
                root.World!=world||root.Owner!=owner.StorageKey||root.Creation!=original.Creation||(int)root.Copy().Attribute("deaths")!=original.Deaths||
                (int)root.Copy().Attribute("kind")!=(int)RemoteResourceClientOperation.Craft||!RebirthRemoteResourceCraftProtocol.HasSavedPersonalAdmission(actor,original,root))return false;
            if(root.Phase<2&&RebirthRemoteResourceDurableOutcome.TryReconcilePersonalCraftSources(actor,original,root,out var reconciled))root=reconciled;
            if(root.Phase==4){result=root;return true;}
            if(root.Phase==3)
            {
                if(RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Copy().Element("removed").Value)).Length==0)
                {result=root;return true;}
                return RebirthRemoteResourceRepairAdoption.Begin(actor,original,out result);
            }
            if(root.Phase!=2)return false;
            if(!root.HasOwnerAction)return RebirthRemoteResourceRepairAdoption.Begin(actor,original,out result);
            if(!RebirthRemoteResourceCraftSettlement.CanonicalAction(original,root,out var action)||!RebirthRemoteResourceCraftSettlement.MatchesRootAction(original,root))return false;
            var network=RebirthRemoteResourceNetworkScope.Capture(actor);int deaths=actor.Died;
            bool Current()=>network?.Current==true&&actor.Died==deaths&&HostScope(actor,original);
            if(!Current()||!RebirthCamperEnergySavedOwner.TryRead(owner,out _,out var saved)||saved.id!=actor.entityId||saved.deaths!=deaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
            string proof=RebirthRemoteResourceCraftSettlement.UsedProof(original,root.Operation,root.Binding,action);
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{
                value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","used");value.SetAttributeValue("ownerProof",proof);
            },Current,out result);
        }
        catch{result=null;return false;}
    }
}
