using System;
using System.Xml.Linq;

// Block's native owner action is distinct from personal queue acceptance.
internal static class RebirthRemoteResourceBlockSettlement
{
    private static string UsedProof(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,
        RebirthRemoteResourceBlockCommand.Accepted command,string savedProof)=>UsedProof(original,root.Operation,root.Binding,command.Digest,command.OwnerProof,savedProof);
    internal static string UsedProof(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string commandDigest,string ownerProof,string savedProof)
        =>RebirthRemoteResourceDurableJournal.Digest("native-block-used|"+original.Digest+"|"+source.ToString("N")+"|"+binding+"|"+commandDigest+"|"+ownerProof+"|"+savedProof);
    // World-only completion of an already accepted original. Its owner2
    // witness was persisted with the command before any native host attempt.
    // No current-life owner setters, callback or source work are permitted here.
    internal static bool ObserveUsed(World world,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||
                !MatchesRootAction(original,root)||!RebirthRemoteResourceBlockHost.TryReadSaved(world,original,out var savedProof))return false;
            string proof=UsedProof(original,root,command,savedProof);var body=root.Copy();
            if(root.Phase==4)return (string)body.Attribute("disposition")=="used"&&(string)body.Attribute("ownerProof")==proof;
            if(root.Phase!=2||body.Attribute("ownerNoUseProof")!=null||body.Element("ownerRefund")!=null)return false;
            bool Current()=>RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var current,out var accepted)&&
                current.Operation==root.Operation&&current.Binding==root.Binding&&accepted.Digest==command.Digest&&
                RebirthRemoteResourceBlockHost.TryReadSaved(world,original,out var observed)&&observed==savedProof;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","used");value.SetAttributeValue("ownerProof",proof);},Current,out _);
        }
        catch{return false;}
    }
    internal static bool HasSavedTerminal(World world,Guid operation,string digest,Vector3i position,string beforeBirth)
    {
        try
        {
            if(!RebirthRemoteResourceBlockAdmission.TryReadOriginal(operation,out var original)||original.Digest!=digest)return false;
            var originalAction=original.Copy().Element("action");var originalPosition=new Vector3i((int)originalAction.Attribute("x"),(int)originalAction.Attribute("y"),(int)originalAction.Attribute("z"));
            if(position==originalPosition&&Guid.TryParseExact(beforeBirth,"N",out var beforeId)&&beforeId!=Guid.Empty&&
                RebirthRemoteResourcePreAdmissionRefusal.HasSavedServerTerminal(world,original))return true;
            if(position==originalPosition&&RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var originalBirth,out _,out _)&&originalBirth==beforeBirth&&
                RebirthRemoteResourceBlockRefund.HasSavedTerminal(world,original))return true;
            if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||root.Phase!=4)return false;
            var action=original.Copy().Element("action");var parent=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));
            if(position==parent){if(command.Birth!=beforeBirth)return false;}
            else if(!RebirthRemoteResourceBlockFootprintAdmission.TryRead(original,command,out var cells)||!System.Linq.Enumerable.Any(cells,cell=>cell.Cell.Position==position&&cell.Birth==beforeBirth))return false;
            if(!RebirthRemoteResourceBlockHost.TryReadSaved(world,original,out var savedProof))return false;
            return (string)root.Copy().Attribute("disposition")=="used"&&(string)root.Copy().Attribute("ownerProof")==UsedProof(original,root,command,savedProof);
        }
        catch{return false;}
    }
    internal static bool Read(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthStablePlayerIdentity owner,out RebirthRemoteResourceDurableJournal.Record root)
    {
        owner=null;root=null;
        if(!RebirthRemoteResourceBlockAdmission.Current(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out owner)||
            !RemoteResourceWorldIdentity.TryGet(actor.world,out var world))return false;
        Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        return RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,operation,out root,out bool unknown)&&!unknown&&root!=null&&root.World==world&&root.Owner==owner.StorageKey&&
            root.Creation==original.Creation&&(int)root.Copy().Attribute("deaths")==original.Deaths&&(int)root.Copy().Attribute("kind")== (int)original.Kind;
    }
    internal static bool CanonicalAction(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out XElement action)
    {
        action=null;
        try
        {
            if(root==null||root.Phase<2||(int?)root.Copy().Attribute("outcome")!=1||!RebirthRemoteResourceBlockAdmission.HasSavedTarget(original)||
                !RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var birth,out _,out _))return false;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Copy().Element("removed").Value));
            return RebirthRemoteResourceBlockOriginal.Canonical(original,root.World,root.Operation,root.Binding,birth,paid,out action);
        }
        catch{return false;}
    }
    private static string BeforeProof(RebirthRemoteResourceClientOriginal.Intent original,XElement action)=>RebirthRemoteResourceDurableJournal.Digest(
        "native-block-owner1|"+original.Digest+"|"+(string)action.Attribute("targetBirth")+"|"+action.Element("beforeActor").Value);
    internal static bool MatchesRootAction(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        if(!CanonicalAction(original,root,out var action))return false;
        var wrapper=root.Copy().Element("ownerAction");
        return (string)wrapper?.Attribute("domain")=="native-block"&&(string)wrapper.Attribute("child")==original.Operation.ToString("N")&&
            (string)wrapper.Attribute("nativeBeforeProof")==BeforeProof(original,action)&&wrapper.Element("original")?.Value==action.ToString(SaveOptions.DisableFormatting);
    }
    internal static bool PrepareOwner(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,XElement incoming,out bool permission)
    {
        permission=false;
        try
        {
            if(!RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)||!Read(actor,original,out var owner,out var root)||root.Phase!=2||!CanonicalAction(original,root,out var action)||!XNode.DeepEquals(action,incoming)||
                ((int?)root.Copy().Attribute("ownerRejecting")??0)!=0||!RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var birth,out var density,out var painted))return false;
            var intent=original.Copy().Element("action");var position=new Vector3i((int)intent.Attribute("x"),(int)intent.Attribute("y"),(int)intent.Attribute("z"));var current=actor.world.GetBlock(position);
            if(Convert.ToUInt64(current.rawData)!=(ulong)intent.Attribute("blockRaw")||current.damage!=(int)intent.Attribute("blockDamage")||actor.world.GetDensity(position)!=density||
                (actor.world.ChunkCache.GetTextureFull(position)!=0L)!=painted||!RebirthOriginalNativeBlockIdentity.TryReadClaim(actor.world,position,1,original.Operation,original.Digest,out var beforeBirth,out var nowBirth)||beforeBirth!=birth||nowBirth!=birth||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value).Matches(actual)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=1f)return false;
            if(root.Copy().Element("ownerAction")!=null){permission=MatchesRootAction(original,root);return permission;}
            var wrapper=new XElement("ownerAction",new XAttribute("domain","native-block"),new XAttribute("child",original.Operation.ToString("N")),
                new XAttribute("nativeBeforeProof",BeforeProof(original,action)),new XElement("original",action.ToString(SaveOptions.DisableFormatting)));
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.Add(wrapper),()=>RebirthRemoteResourceBlockAdmission.Current(actor,original),out var retained))return false;
            permission=MatchesRootAction(original,retained);return permission;
        }
        catch{permission=false;return false;}
    }
    internal static bool TryObserveOwnerPaid(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!Read(actor,original,out var owner,out var root)||root.Phase!=2||!MatchesRootAction(original,root)||!CanonicalAction(original,root,out var action)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value).Matches(actual)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
            proof=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+root.Operation.ToString("N")+"|"+root.Binding+"|"+action.Element("afterActor").Value);return true;
        }
        catch{proof=null;return false;}
    }
}
