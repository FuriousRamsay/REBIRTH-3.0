using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Owns only the contents of an actual Applied camper removal. Neither a
// client-provided inventory plan nor an overflow request is source authority.
internal static class RebirthCamperRemovalParcelHost
{
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOriginals"));
    private static bool Source(EntityPlayer actor,RebirthStablePlayerIdentity identity,string creation,Guid vehicle,Guid removal,string source,
        out RebirthCamperTransferJournal.Record original)
    {
        original=null;
        try
        {
            string owner=actor is EntityPlayerLocal?creation:"remote:"+identity.CanonicalId+":"+creation;
            var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"));
            foreach(var candidate in journal.FindApplied(vehicle,owner))
            {
                if(removal!=Guid.Empty&&candidate.Operation!=removal)continue;
                if(candidate.State!=RebirthCamperTransferJournal.Phase.Applied||candidate.OwnerCreation!=owner||
                    RebirthStationPublicationRecord.Digest(candidate.Before)+RebirthStationPublicationRecord.Digest(candidate.After)!=source)continue;
                if(original!=null)return false;original=candidate;
            }
            return original!=null;
        }
        catch{original=null;return false;}
    }
    internal static XElement Request(EntityPlayer actor,XElement request)
    {
        IDisposable workspace=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
                !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||!actor.IsSpawned()||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||request?.Name!="removalParcelRequest"||
                request.Attributes().Count()!=8||request.Nodes().Any()||(int?)request.Attribute("version")!=1||
                !Guid.TryParseExact((string)request.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)request.Attribute("vehicle"),"N",out var vehicle)||vehicle==Guid.Empty||
                !Guid.TryParseExact((string)request.Attribute("installation"),"N",out var installation)||installation==Guid.Empty||
                !Guid.TryParseExact((string)request.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty)return null;
            string source=(string)request.Attribute("source"),creation=(string)request.Attribute("creation");int stage=(int)request.Attribute("stage");
            if(source?.Length!=128||!source.All(Uri.IsHexDigit)||string.IsNullOrEmpty(creation)||creation.Length>512||
                !RebirthOriginalActionScope.Matches(actor,identity.StorageKey,creation)||stage<0||stage>2||
                operation!=RebirthCamperRemovalParcelPlan.Key(actor.world.worldState.Guid,source,identity.StorageKey,creation))return null;
            var canonical=new XElement("removalParcelRequest",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("source",source),new XAttribute("vehicle",vehicle.ToString("N")),new XAttribute("installation",installation.ToString("N")),
                new XAttribute("creation",creation),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("stage",stage));
            if(!XNode.DeepEquals(request,canonical))return null;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out workspace))return null;
            var journal=Journal();var record=journal.Read(operation);RebirthCamperRemovalParcelPlan plan;
            if(record==null)
            {
                if(stage!=0||!Source(actor,identity,creation,vehicle,Guid.Empty,source,out var original)||
                    !RebirthCamperEnergySavedOwner.TryRead(identity,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=actor.Died||
                    !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,"rbRemovalParcel_"+operation.ToString("N"),out float phase,out bool present)||!present||phase!=1f||
                    !RebirthCamperRemovalParcelPlan.TryCreate(actor,identity,creation,original,before,out plan)||plan.Operation!=operation||plan.Installation!=installation)return null;
                record=plan.Retain();
            }
            else
            {
                if(record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After)||record.Vehicle!=vehicle||record.OwnerCreation!=creation||
                    !RebirthCamperRemovalParcelPlan.TryRead(RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024),out plan)||
                    plan.Operation!=operation||plan.Vehicle!=vehicle||plan.Installation!=installation||plan.Source!=source||plan.Owner!=identity.StorageKey||plan.Creation!=creation||
                    plan.World!=actor.world.worldState.Guid||!Source(actor,identity,creation,vehicle,plan.Removal,source,out _))return null;
            }
            int deaths=actor.Died,entity=actor.entityId;var world=actor.world;string save=GameIO.GetSaveGameDir();
            bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&
                actor.entityId==entity&&ReferenceEquals(world.GetEntity(entity),actor)&&!actor.IsDead()&&actor.IsSpawned()&&actor.Died==deaths&&
                GameIO.GetSaveGameDir()==save&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,creation);
            string proof=string.Empty;int status=0;
            if(stage>=1||record.State!=RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=deaths||
                    !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float phase,out bool present)||!present)return null;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                {
                    if(stage!=1||deaths!=plan.Deaths||phase!=500f||!plan.Credited.Matches(actual)||!Current()||
                        !RebirthCamperRemovalParcelOverflow.RetainUnattempted(plan)||!Current())return null;
                    record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
                }
                if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)
                {
                    // Native retirement may have persisted before its metadata
                    // marker. Only an already positive saved overflow terminal
                    // can qualify that exact stripped owner frame; it must not
                    // enter a new overflow spawn from a matching shape.
                    bool retiredBeforeMarker=phase==500f&&plan.Retired.Matches(actual)&&RebirthCamperRemovalParcelOverflow.Terminal(plan,out _);
                    if(deaths!=plan.Deaths||!(phase==500f&&plan.Credited.Matches(actual)||phase==1000f&&plan.Retired.Matches(actual)||retiredBeforeMarker)||!Current())return null;
                    if(!RebirthCamperRemovalParcelOverflow.Advance(actor,plan,Current,out proof))return Result(plan,nonce,0,string.Empty);
                    status=1;
                    if(stage==2)
                    {
                        if(deaths!=plan.Deaths||phase!=1000f||!plan.Retired.Matches(actual)||!Current())return null;
                        record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Applied);status=2;
                    }
                }
                if(record.State==RebirthCamperTransferJournal.Phase.Applied)
                {
                    if(!RebirthCamperRemovalParcelOverflow.Terminal(plan,out proof))return null;status=2;
                }
            }
            return Current()?Result(plan,nonce,status,proof):null;
        }
        catch{return null;}
        finally{workspace?.Dispose();}
    }
    private static XElement Result(RebirthCamperRemovalParcelPlan plan,Guid nonce,int status,string proof)=>new XElement("removalParcelResult",
        new XAttribute("version",1),new XAttribute("operation",plan.Operation.ToString("N")),new XAttribute("nonce",nonce.ToString("N")),
        new XAttribute("binding",plan.Binding),new XAttribute("source",plan.Source),new XAttribute("status",status),new XAttribute("proof",proof),plan.Write());
}
