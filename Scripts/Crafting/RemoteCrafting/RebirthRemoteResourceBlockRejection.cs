using System;
using System.Xml.Linq;

// Named paid-but-never-permitted Block disposition. This does not classify an
// attempted/accepted native command or a missing marker as unused.
internal static partial class RebirthRemoteResourceBlockRejection
{
    private static bool Read(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthStablePlayerIdentity owner,out RebirthRemoteResourceDurableJournal.Record root)
    {
        owner=null;root=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
            !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||original==null||actor.world.worldState.Guid!=original.World||actor.Died!=original.Deaths||
            (original.Kind!=RemoteResourceClientOperation.BlockRepair&&original.Kind!=RemoteResourceClientOperation.BlockUpgrade)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out owner)||owner.CanonicalId!=original.Owner||!RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation)||
            !RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)||retained.Digest!=original.Digest||
            !RebirthRemoteResourceBlockAdmission.HasSavedTarget(original)||!RemoteResourceWorldIdentity.TryGet(actor.world,out var world))return false;
        Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        return RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out root,out bool unknown)&&!unknown&&root!=null&&root.World==world&&root.Owner==owner.StorageKey&&
            root.Creation==original.Creation&&(int)root.Copy().Attribute("deaths")==original.Deaths&&(int)root.Copy().Attribute("kind")== (int)original.Kind;
    }
    internal static string UnissuedProof(RebirthRemoteResourceClientOriginal.Intent original,string binding)=>RebirthRemoteResourceDurableJournal.Digest(
        "native-block-unissued|"+original.Digest+"|"+binding+"|"+original.Copy().Element("actorBefore").Value);
    internal static bool ValidateUnissuedRootProof(XElement root)
    {
        try
        {
            if(!Guid.TryParseExact((string)root.Attribute("operation"),"N",out var source)||!RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(source,out var original))return false;
            string owner=RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner),world=(string)root.Attribute("world");
            return (string)root.Attribute("owner")==owner&&(string)root.Attribute("creation")==original.Creation&&(int)root.Attribute("deaths")==original.Deaths&&
                (int)root.Attribute("kind")== (int)original.Kind&&RebirthRemoteResourceDurableJournal.Operation(world,owner,original.Creation,original.Epoch,original.Request,original.Kind)==source&&
                root.Element("ownerAction")==null&&(string)root.Attribute("ownerNoUseProof")==UnissuedProof(original,new RebirthRemoteResourceDurableJournal.Record(source,0,root).Binding);
        }
        catch{return false;}
    }
    private static bool NeverIssued(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
        =>root.Phase==2&&(int?)root.Copy().Attribute("outcome")==1&&root.Copy().Element("ownerAction")==null&&
            RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)&&RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original);
    private static string DeclinedProof(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,string declined)
        =>RebirthRemoteResourceDurableJournal.Digest("native-block-declined-unused|"+original.Digest+"|"+root.Binding+"|"+declined);
    internal static bool ValidateDeclinedRootProof(XElement image)
    {
        try
        {
            if(!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var source)||!RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(source,out var original))return false;
            var root=new RebirthRemoteResourceDurableJournal.Record(source,0,image);
            return root.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)&&root.Creation==original.Creation&&
                root.Operation==RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,original.Creation,original.Epoch,original.Request,original.Kind)&&
                (int?)image.Attribute("deaths")==original.Deaths&&(int?)image.Attribute("kind")== (int)original.Kind&&
                RebirthRemoteResourceBlockCommand.TryReadDeclined(original,root,out var declined)&&
                (string)image.Attribute("ownerNoUseProof")==DeclinedProof(original,root,declined);
        }
        catch{return false;}
    }
    internal static bool RejectDeclined(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!Read(actor,original,out var owner,out var root)||root.Phase!=2||
                !RebirthRemoteResourceBlockCommand.TryReadDeclined(original,root,out var declined)||
                !RebirthRemoteResourceBlockCommand.TryRead(original,root,out var command)||
                !RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value).Matches(actual)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
            string expected=DeclinedProof(original,root,declined);
            if(root.Copy().Attribute("ownerNoUseProof")!=null){proof=(string)root.Copy().Attribute("ownerNoUseProof");return proof==expected;}
            bool Current()=>Read(actor,original,out _,out var current)&&current.Operation==root.Operation&&current.Binding==root.Binding&&current.Phase==2&&
                RebirthRemoteResourceBlockCommand.TryReadDeclined(original,current,out var currentDeclined)&&currentDeclined==declined;
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("ownerRejecting",1);value.SetAttributeValue("ownerNoUseProof",expected);},Current,out _))return false;
            proof=expected;return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool RejectUnissued(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,bool observe,out string proof)
    {
        proof=null;
        try
        {
            if(!Read(actor,original,out var owner,out var root)||!NeverIssued(original,root))return false;
            var body=root.Copy();
            bool Current()=>Read(actor,original,out _,out var current)&&current.Operation==root.Operation&&current.Binding==root.Binding&&NeverIssued(original,current);
            if(((int?)body.Attribute("ownerRejecting")??0)!=1)
                return !observe&&RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.SetAttributeValue("ownerRejecting",1),Current,out _);
            if(!observe)return true;
            if(!RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(actual)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=5f)return false;
            string expected=UnissuedProof(original,root.Binding);
            if(body.Attribute("ownerNoUseProof")!=null){proof=(string)body.Attribute("ownerNoUseProof");return proof==expected;}
            if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.SetAttributeValue("ownerNoUseProof",expected),Current,out _))return false;
            proof=expected;return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool IsUnissuedOwed(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            // Canonical retained admission includes the original native10
            // beforeeffect proof. No current target/range is renewed here.
            return RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)&&retained.Digest==original.Digest&&
                root.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)&&root.Creation==original.Creation&&
                root.Operation==RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,original.Creation,original.Epoch,original.Request,original.Kind)&&
                (int?)root.Copy().Attribute("deaths")==original.Deaths&&(int?)root.Copy().Attribute("kind")== (int)original.Kind&&
                root.Phase==2&&(int?)root.Copy().Attribute("outcome")==1&&(int?)root.Copy().Attribute("ownerRejecting")==1&&root.Copy().Element("ownerAction")==null&&
                RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)&&RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)&&
                (string)root.Copy().Attribute("ownerNoUseProof")==UnissuedProof(original,root.Binding);
        }
        catch{return false;}
    }
}
