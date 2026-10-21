using System;
using System.Linq;
using System.Xml.Linq;

// Host observation only. Host-native source originals, final native owner
// files and the accepted native queue are the effect witnesses, not this wire.
internal static class RebirthRemoteResourceCraftSettlement
{
    private static bool Scope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&!actor.IsDead()&&
        original?.Kind==RemoteResourceClientOperation.Craft&&actor.Died==original.Deaths&&actor.world.worldState.Guid==original.World&&
        ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation);
    private static bool Read(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthStablePlayerIdentity identity,out RebirthRemoteResourceDurableJournal.Record root)
    {
        identity=null;root=null;
        if(!Scope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out identity)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
        Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        return RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out root,out _)&&root!=null&&root.Phase>=2&&root.World==world&&root.Owner==identity.StorageKey&&root.Creation==original.Creation&&
            (int)root.Copy().Attribute("deaths")==original.Deaths&&(int)root.Copy().Attribute("kind")== (int)RemoteResourceClientOperation.Craft;
    }
    internal static bool CanonicalAction(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out XElement action)
    {
        action=null;
        try
        {
            if(root==null||(int)root.Copy().Attribute("outcome")!=1)return false;
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Copy().Element("removed").Value));
            return RebirthRemoteResourceCraftOriginal.Canonical(original,root.World,root.Operation,root.Binding,paid,out action);
        }
        catch{return false;}
    }
    internal static bool MatchesRootAction(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        if(!CanonicalAction(original,root,out var action))return false;
        var wrapper=root.Copy().Element("ownerAction");
        return wrapper!=null&&(string)wrapper.Attribute("domain")=="native-craft"&&(string)wrapper.Attribute("child")==original.Operation.ToString("N")&&
            (string)wrapper.Attribute("nativeBeforeProof")==RebirthRemoteResourceDurableJournal.Digest(original.Digest+"|"+original.Copy().Element("queueBefore").Value+"|"+action.Element("beforeActor").Value)&&
            wrapper.Element("original")?.Value==action.ToString(SaveOptions.DisableFormatting);
    }
    internal static bool Prepare(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,XElement incoming,out bool permission)
    {
        permission=false;
        try
        {
            if(!Read(actor,original,out var identity,out var root)||root.Phase!=2||!CanonicalAction(original,root,out var action)||!XNode.DeepEquals(action,incoming))return false;
            var body=root.Copy();if(((int?)body.Attribute("ownerRejecting")??0)!=0)return false;
            var existing=body.Element("ownerAction");
            if(!RebirthRemoteResourceCraftingRead.TryRead(identity,out var actual,out var saved,out var queue)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                queue!=original.Copy().Element("queueBefore").Value||!RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value).Matches(actual)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=1f)return false;
            if(existing!=null){permission=MatchesRootAction(original,root);return permission;}
            var wrapper=new XElement("ownerAction",new XAttribute("domain","native-craft"),new XAttribute("child",original.Operation.ToString("N")),
                new XAttribute("nativeBeforeProof",RebirthRemoteResourceDurableJournal.Digest(original.Digest+"|"+queue+"|"+RebirthRemoteResourceClientOriginal.ActorImage(actual))),new XElement("original",action.ToString(SaveOptions.DisableFormatting)));
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.Add(wrapper),()=>Scope(actor,original),out var savedRoot))return false;
            permission=MatchesRootAction(original,savedRoot);return permission;
        }
        catch{permission=false;return false;}
    }
    internal static string UsedProof(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,XElement action)=>RebirthRemoteResourceDurableJournal.Digest(
        source.ToString("N")+"|"+binding+"|"+original.Digest+"|"+action.Element("markedRecipe").Value+"|"+(string)action.Attribute("count")+"|"+(string)action.Attribute("quality")+"|"+action.Element("afterActor").Value);
    internal static bool ObserveUsed(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!Read(actor,original,out var identity,out var root)||!MatchesRootAction(original,root)||!CanonicalAction(original,root,out var action))return false;
            var body=root.Copy();string expected=UsedProof(original,root.Operation,root.Binding,action);
            if(root.Phase==4)return (string)body.Attribute("disposition")=="used"&&(string)body.Attribute("ownerProof")==expected;
            if(root.Phase!=2||body.Attribute("ownerNoUseProof")!=null||body.Element("ownerRefund")!=null||
                !RebirthRemoteResourceOwnerAction.TrySavedCraftUsage(actor,identity,original,root.Operation,root.Binding,action.Element("markedRecipe").Value,(int)action.Attribute("count"),(int)action.Attribute("quality"),
                    RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value),out string proof)||proof!=expected)return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","used");value.SetAttributeValue("ownerProof",proof);},()=>Scope(actor,original),out _);
        }
        catch{return false;}
    }
    internal static string UnusedProof(RebirthRemoteResourceClientOriginal.Intent original,string binding)=>RebirthRemoteResourceDurableJournal.Digest("craft-unused|"+original.Digest+"|"+binding+"|"+
        original.Copy().Element("actorBefore").Value+"|"+original.Copy().Element("queueBefore").Value);
    internal static bool Reject(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,bool observe)
    {
        try
        {
            if(!Read(actor,original,out var identity,out var root)||root.Phase!=2||(int)root.Copy().Attribute("outcome")!=1)return false;
            var body=root.Copy();
            if(body.Element("ownerAction")!=null&&!MatchesRootAction(original,root)||body.Element("ownerAction")==null&&!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root))return false;
            if(((int?)body.Attribute("ownerRejecting")??0)!=1)
                return !observe&&RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.SetAttributeValue("ownerRejecting",1),()=>Scope(actor,original),out _);
            if(!observe)return true;
            string proof=UnusedProof(original,root.Binding);
            if(body.Attribute("ownerNoUseProof")!=null)return (string)body.Attribute("ownerNoUseProof")==proof;
            // Exact full queue BEFORE, not absence of an ingredient marker:
            // an exception could leave a newly accepted unmarked queue row.
            if(!RebirthRemoteResourceCraftingRead.TryRead(identity,out var actorImage,out var saved,out string queue)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                queue!=original.Copy().Element("queueBefore").Value||!RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(actorImage)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=5f)return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.SetAttributeValue("ownerNoUseProof",proof),()=>Scope(actor,original),out _);
        }
        catch{return false;}
    }
}
