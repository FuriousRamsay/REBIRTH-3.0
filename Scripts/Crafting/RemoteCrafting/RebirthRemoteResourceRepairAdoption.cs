using System;
using System.Linq;
using System.Xml.Linq;

// Query scope is not owner-write permission. Only a positively paid original
// that NEVER published an owner action can become this new-life owed delivery.
internal static class RebirthRemoteResourceRepairAdoption
{
    internal static bool QueryScope(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(RebirthRemoteResourceClientOriginal.Current(player,original))return true;
        if(original?.Kind==RemoteResourceClientOperation.BlockRepair||original?.Kind==RemoteResourceClientOperation.BlockUpgrade)return RebirthRemoteResourceBlockAdoption.QueryScope(player,original);
        if(original?.Kind==RemoteResourceClientOperation.Craft)return RebirthRemoteResourceCraftAdoption.QueryScope(player,original);
        try{return ThreadManager.IsMainThread()&&player?.world?.IsRemote()==true&&player.IsSpawned()&&!player.IsDead()&&original!=null&&
            original.Kind==RemoteResourceClientOperation.ItemRepair&&player.Died>original.Deaths&&
            ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
            player.world.worldState.Guid==original.World&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity.CanonicalId==original.Owner&&
            RebirthRemoteResourceClientOriginal.HasNeverPreparedRepairAction(original);}
        catch{return false;}
    }
    internal static string Proof(RebirthRemoteResourceClientOriginal.Intent original,string binding)=>
        RebirthRemoteResourceDurableJournal.Digest("repair-unissued-adoption|"+original.Digest+"|"+binding);
    internal static bool HostScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&
        !actor.IsDead()&&actor.Died>original.Deaths&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
        actor.world.worldState.Guid==original.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.CanonicalId==original.Owner&&
        RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation);
    // Caller already validated the immutable original pre-source admission
    // (including its saved native10 source image) from the host's own journal.
    internal static bool Begin(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourceDurableJournal.Record result)
    {
        result=null;
        try
        {
            if(!HostScope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var root,out _)||root==null||root.Copy().Element("ownerAction")!=null||
                (int)root.Copy().Attribute("deaths")!=original.Deaths||root.Phase!=2&&root.Phase!=3&&root.Phase!=4||!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root))return false;
            if(original.Kind==RemoteResourceClientOperation.Craft&&!RebirthRemoteResourceCraftProtocol.HasSavedPersonalAdmission(actor,original,root))return false;
            string proof=Proof(original,root.Binding);var body=root.Copy();
            bool partial=original.Kind==RemoteResourceClientOperation.Craft&&(root.Phase==3||root.Phase==4)&&(int)body.Attribute("outcome")==2&&
                RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("removed").Value)).Length>0&&
                body.Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0||
                    (int)source.Attribute("published")==1&&((int?)source.Attribute("restored")??0)==0);
            if(!partial&&((int)body.Attribute("outcome")!=1||body.Element("removed")==null||body.Element("sources").Elements("source").Any(source=>(int)source.Attribute("published")!=1)))return false;
            if(body.Attribute("ownerNoUseProof")!=null)
            {if((string)body.Attribute("ownerNoUseProof")!=proof||((int?)body.Attribute("ownerRejecting")??0)!=1)return false;result=root;return true;}
            if(root.Phase!=2&&!(partial&&root.Phase==3))return false;
            bool Current()=>HostScope(actor,original)&&RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var live,out _)&&live!=null&&live.Revision==root.Revision&&live.Binding==root.Binding&&live.Copy().Element("ownerAction")==null;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("ownerRejecting",1);value.SetAttributeValue("ownerNoUseProof",proof);},Current,out result);
        }
        catch{result=null;return false;}
    }
}
