using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Purpose-specific server routing. The matching owner dispatcher must be
// complete before Install/routing is enabled; no native package is changed.
internal static partial class RebirthRemoteResourceBlockProtocol
{
    private static readonly Dictionary<Guid,RebirthOriginalNativeBlockIdentity.Admission> targetWatches=new Dictionary<Guid,RebirthOriginalNativeBlockIdentity.Admission>();
    private static readonly Dictionary<Guid,RebirthRemoteResourceClientOriginal.Intent> acceptedJobs=new Dictionary<Guid,RebirthRemoteResourceClientOriginal.Intent>();
    private static World serverWorld;private static string serverDirectory;
    private static Queue<Guid> serverCold;private static bool serverUnknown;private static int serverCursor;
    private static readonly HashSet<Guid> serverUnresolved=new HashSet<Guid>();
    private static RebirthRemoteResourceClientOriginalJournal PendingIndex()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockAcceptedPending"));
    private static XElement PendingBody(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,string command)
        =>new XElement("blockAcceptedPending",new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),new XAttribute("binding",root.Binding),new XAttribute("command",command));
    internal static bool CanRetainOwnerAction(RebirthRemoteResourceClientOriginal.Intent original,XElement action)
    {
        try
        {
            // Exact nested XML escaping, not a raw-size estimate. Issued
            // cancellation is the largest action-bearing Block request.
            var reference=new XElement("originalRef",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("epoch",original.Epoch),new XAttribute("request",original.Request));
            string rejection=RebirthRemoteResourceBlockOriginal.PreparedRejectionBody(original,action).ToString(SaveOptions.DisableFormatting);
            var envelope=new XElement("blockRequest",new XAttribute("version",1),new XAttribute("stage",10),new XAttribute("exchange",ulong.MaxValue),new XAttribute("nonce",new string('f',32)),reference,new XElement("ownerActionBody",rejection));
            return Encoding.UTF8.GetByteCount(rejection)<=24*1024*1024&&Encoding.UTF8.GetByteCount(envelope.ToString(SaveOptions.DisableFormatting))<=24*1024*1024;
        }
        catch{return false;}
    }
    internal static bool TrackPreparedAcceptance(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,string command)
    {
        try
        {
            if(root?.Phase!=2||!RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)||command?.Length!=64)return false;
            byte[] bytes=Encoding.UTF8.GetBytes(PendingBody(original,root,command).ToString(SaveOptions.DisableFormatting));
            var record=PendingIndex().Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
            return record.Before.SequenceEqual(bytes)&&record.After.SequenceEqual(bytes)&&record.State==RebirthCamperTransferJournal.Phase.Prepared;
        }
        catch{return false;}
    }
    private static void BindServer(World world)
    {
        string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(world,serverWorld)&&directory==serverDirectory)return;
        foreach(var watch in targetWatches.Values)watch.Dispose();targetWatches.Clear();acceptedJobs.Clear();serverUnresolved.Clear();serverWorld=world;serverDirectory=directory;serverCold=null;serverUnknown=false;serverCursor=0;
    }
    private static bool ReadPendingIndex(World world,Guid operation,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        var record=PendingIndex().Read(operation);
        if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Prepared||record.Before.Length>4096||!record.Before.SequenceEqual(record.After)||
            !RebirthRemoteResourceBlockAdmission.TryReadOriginal(operation,out var actual)||record.Vehicle!=operation||record.OwnerCreation!=actual.Creation||record.Revision!=0||
            !RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,actual,out var root,out var command)||
            !record.Before.SequenceEqual(Encoding.UTF8.GetBytes(PendingBody(actual,root,command.Digest).ToString(SaveOptions.DisableFormatting))))return false;
        original=actual;return true;
    }
    private static void StartCold(World world)
    {
        if(serverCold!=null)return;serverCold=new Queue<Guid>();
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockAcceptedPending");if(!Directory.Exists(directory))return;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();int count=0;var operations=new HashSet<Guid>();
            foreach(string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if(Path.GetFileName(entry)=="completed")
                {if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(entry)&FileAttributes.Directory)==0)throw new InvalidDataException();continue;}
                if(++count>16384||(File.GetAttributes(entry)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)throw new InvalidDataException();
                string name=Path.GetFileName(entry);if(!Guid.TryParseExact(name.Length>=32?name.Substring(0,32):string.Empty,"N",out var operation))throw new InvalidDataException();
                string prefix=operation.ToString("N");
                if(name!=prefix+".0.rcj"&&name!=prefix+".1.rcj"&&name!=prefix+".2.rcj")throw new InvalidDataException();
                operations.Add(operation); // Sidecar-only originals also require recovery qualification.
            }
            if(operations.Count>4096)throw new InvalidDataException();
            foreach(var operation in operations.OrderBy(value=>value))serverCold.Enqueue(operation);
        }
        catch{serverUnknown=true;} // Quarantine fresh admission, never freeze arbitrary bags or discard evidence.
    }
    private static void ServerUpdate(World world)
    {
        BindServer(world);if(world?.IsRemote()!=false)return;StartCold(world);
        // One bounded original read per frame; full owner/native plans are not
        // repeatedly traversed merely to rediscover historical terminals.
        if(serverCold.Count>0&&acceptedJobs.Count<128)
        {
            var operation=serverCold.Dequeue();
            try
            {
                var record=PendingIndex().Read(operation);
                if(record?.State==RebirthCamperTransferJournal.Phase.Applied)PendingIndex().Archive(operation);
                else if(ReadPendingIndex(world,operation,out var original)){acceptedJobs[operation]=original;serverUnresolved.Remove(operation);}
                else serverUnresolved.Add(operation);
            }
            catch{serverUnresolved.Add(operation);}
        }
        var jobs=acceptedJobs.Values.ToArray();if(jobs.Length==0)return;
        for(int i=0;i<Math.Min(4,jobs.Length);i++){serverCursor%=jobs.Length;PumpAccepted(world,jobs[serverCursor++]);}
    }
    private static bool OwnerScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,bool originalLife)
        =>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
            !actor.IsDead()&&actor.IsSpawned()&&original!=null&&actor.world.worldState.Guid==original.World&&(!originalLife||actor.Died==original.Deaths)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation);
    private static bool EnsureAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!OwnerScope(actor,original,true))return false;
        if(RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)&&retained.Digest==original.Digest&&RebirthRemoteResourceBlockAdmission.HasSavedTarget(original))return true;
        StartCold(actor.world);if(serverUnknown||serverUnresolved.Count>0||serverCold.Count>0)return false;
        if(!RebirthRemoteResourceBlockAdmission.Current(actor,original))return false;
        if(targetWatches.TryGetValue(original.Operation,out var stale)&&!stale.Current())
        {stale.Dispose();targetWatches.Remove(original.Operation);}
        if(!targetWatches.TryGetValue(original.Operation,out var watch))
        {
            if(targetWatches.Count>=32||!RebirthRemoteResourceBlockAdmission.TryBindTarget(actor,original,out watch))return false;
            targetWatches.Add(original.Operation,watch);
        }
        if(watch.Digest!=original.Digest||!watch.Saved()||!RebirthRemoteResourceBlockAdmission.ConfirmSavedTarget(original,watch))return false;
        watch.Dispose();targetWatches.Remove(original.Operation);return RebirthRemoteResourceBlockAdmission.HasSavedTarget(original);
    }
    internal static void Request(EntityPlayer actor,string text)
    {
        try
        {
            BindServer(actor?.world);if(!installed||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor))return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            if(body.Name!="blockRequest"||body.Attributes().Count()!=4||body.Elements().Count()!=2||(int?)body.Attribute("version")!=1||body.Element("ownerActionBody")==null)return;
            int stage=(int)body.Attribute("stage");ulong exchange=(ulong)body.Attribute("exchange");string nonce=(string)body.Attribute("nonce");
            if(stage<0||stage>10||exchange==0||!Guid.TryParseExact(nonce,"N",out var session)||session==Guid.Empty||session.ToString("N")!=nonce)return;
            RebirthRemoteResourceClientOriginal.Intent original;
            if(body.Element("fetchClientOriginal")!=null)
            {if(!RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal").ToString(SaveOptions.DisableFormatting),out original))return;}
            else
            {
                var reference=body.Element("originalRef");if(reference==null||reference.Attributes().Count()!=4||reference.Nodes().Any()||!Guid.TryParseExact((string)reference.Attribute("operation"),"N",out var operation)||
                    !RebirthRemoteResourceBlockAdmission.TryReadOriginal(operation,out original)||original.Digest!=(string)reference.Attribute("digest")||original.Epoch!=(ulong)reference.Attribute("epoch")||original.Request!=(ulong)reference.Attribute("request"))return;
            }
            if(!RebirthRemoteResourceBlockPlan.Validate(original)||!OwnerScope(actor,original,stage!=9&&stage!=5&&stage!=6&&stage!=7))return;
            XElement source=body.Element("fetchClientOriginal")!=null?original.Copy():new XElement("originalRef",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("epoch",original.Epoch),new XAttribute("request",original.Request));
            var canonical=new XElement("blockRequest",new XAttribute("version",1),new XAttribute("stage",stage),new XAttribute("exchange",exchange),new XAttribute("nonce",nonce),source,new XElement("ownerActionBody",body.Element("ownerActionBody").Value));
            if(canonical.ToString(SaveOptions.DisableFormatting)!=text)return;
            if(stage==9)
            {
                // Continue an already accepted command or positively settle
                // the existing ended-life source original. Neither route
                // discovers new sources or issues old-life native permission.
                RebirthRemoteResourceDurableJournal.Record retained;
                if(RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(actor.world,original,out retained,out _))
                {
                    if(acceptedJobs.Count<128||acceptedJobs.ContainsKey(original.Operation))acceptedJobs[original.Operation]=original;PumpAccepted(actor.world,original);
                    RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(actor.world,original,out retained,out _);
                    if(actor.Died>original.Deaths&&RebirthRemoteResourceBlockAdoption.Read(actor,original,out var adopted)&&adopted!=null)retained=adopted;
                }
                else if(!RebirthRemoteResourceBlockAdoption.Read(actor,original,out retained))return;
                Reply(actor,original,retained,false,nonce,exchange);return;
            }
            if((stage==5||stage==6||stage==7)&&actor.Died>original.Deaths)
            {
                if(!RebirthRemoteResourceBlockAdoption.Read(actor,original,out var owed)||owed==null)return;
                bool allowed=false;string refundBody=body.Element("ownerActionBody").Value;
                if(stage==5)RebirthRemoteResourceDurableOutcome.AcknowledgeBlockNoPayment(actor,original,owed.Operation,owed.Binding);
                else RebirthRemoteResourceBlockRefund.Host(actor,original,RebirthCookingRemoteAdmission.ReadProtocolXml(refundBody,24*1024*1024),stage==7,out allowed);
                if(RebirthRemoteResourceBlockAdoption.Read(actor,original,out owed)&&owed!=null)Reply(actor,original,owed,allowed,nonce,exchange);
                return;
            }
            if(stage==10)
            {
                // A canceled pre-admission query never creates a source root.
                // An already admitted source original may finish its original
                // custody settlement so its exact outcome can be disposed.
                if(!RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var known)||known.Digest!=original.Digest)return;
                var requirements=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
                if(RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,requirements,true,out var observed)&&observed!=null)
                    Reply(actor,original,observed,false,nonce,exchange);
                return;
            }
            if(!EnsureAdmission(actor,original)){Reply(actor,original,null,false,nonce,exchange);return;}
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
            if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,stage!=0,out var root)||root==null)
            {Reply(actor,original,null,false,nonce,exchange);return;}
            bool permission=false;string payload=body.Element("ownerActionBody").Value;
            if(stage==1&&RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action))
                RebirthRemoteResourceBlockSettlement.PrepareOwner(actor,original,action,out permission);
            else if(stage==2)
            {
                var command=RebirthCookingRemoteAdmission.ReadProtocolXml(payload,128*1024);
                if(command.Name=="blockOwnerCommand"&&command.Attributes().Count()==1&&command.Elements().Count()==1&&command.Element("changes")!=null&&
                    command.ToString(SaveOptions.DisableFormatting)==new XElement("blockOwnerCommand",new XAttribute("clear",(bool)command.Attribute("clear")),new XElement("changes",command.Element("changes").Value)).ToString(SaveOptions.DisableFormatting)&&
                    RebirthRemoteResourceBlockCommand.Admit(actor,original,command.Element("changes").Value,(bool)command.Attribute("clear")))
                {
                    if(ReadPendingIndex(actor.world,original.Operation,out _))serverUnresolved.Remove(original.Operation);
                    if(acceptedJobs.Count<128||acceptedJobs.ContainsKey(original.Operation))acceptedJobs[original.Operation]=original;PumpAccepted(actor.world,original);
                }
            }
            else if(stage==3||stage==4)
            {
                if(payload.Length==0||root.Copy().Element("ownerAction")==null)RebirthRemoteResourceBlockRejection.RejectUnissued(actor,original,stage==4,out _);
                else RebirthRemoteResourceBlockRejection.RejectIssued(actor,original,RebirthCookingRemoteAdmission.ReadProtocolXml(payload,24*1024*1024),stage==4,out _);
            }
            else if(stage==5)RebirthRemoteResourceDurableOutcome.AcknowledgeBlockNoPayment(actor,original,root.Operation,root.Binding);
            else if(stage==6||stage==7)RebirthRemoteResourceBlockRefund.Host(actor,original,RebirthCookingRemoteAdmission.ReadProtocolXml(payload,24*1024*1024),stage==7,out permission);
            else if(stage==8)RebirthRemoteResourceBlockRejection.RejectDeclined(actor,original,out _);
            if(RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(actor.world,original,out _,out _))PumpAccepted(actor.world,original);
            if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,true,out root)||root==null)return;
            Reply(actor,original,root,permission,nonce,exchange);
        }
        catch{/* Unknown never becomes refusal or owner permission. */}
    }
    private static void PumpAccepted(World world,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out _))return;
        if(root.Phase==2&&root.Copy().Attribute("ownerNoUseProof")==null)
        {
            var status=RebirthRemoteResourceBlockHost.TryInvokeAccepted(world,original);
            if(status==RebirthRemoteResourceBlockHost.Status.Published)RebirthRemoteResourceBlockSettlement.ObserveUsed(world,original);
        }
        if(RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out root,out _)&&root.Phase==4)
        {
            bool terminal=RebirthRemoteResourceBlockRefund.HasSavedTerminal(world,original)||
                RebirthRemoteResourceBlockCommand.TryRead(original,root,out var command)&&RebirthRemoteResourceBlockSettlement.HasSavedTerminal(world,original.Operation,original.Digest,
                    new Vector3i((int)original.Copy().Element("action").Attribute("x"),(int)original.Copy().Element("action").Attribute("y"),(int)original.Copy().Element("action").Attribute("z")),command.Birth);
            if(!terminal)return;
            var journal=PendingIndex();var record=journal.Read(original.Operation);
            if(record?.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record?.State==RebirthCamperTransferJournal.Phase.Uncertain)journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            RebirthRemoteResourceBlockHost.RetirePublished(world,original);acceptedJobs.Remove(original.Operation);serverUnresolved.Remove(original.Operation);
        }
    }
    private static void Reply(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,bool permission,string nonce,ulong exchange)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out var sourceWorld))return;
        var image=root?.Copy();Guid source=root?.Operation??RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        bool admitted=RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)&&retained.Digest==original.Digest&&RebirthRemoteResourceBlockAdmission.HasSavedTarget(original);
        string birth=RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var target,out _,out _)?target:string.Empty;
        string commandDigest=string.Empty,decline=string.Empty,declinedBody=string.Empty,saved=string.Empty,projection=string.Empty;
        if(root!=null&&RebirthRemoteResourceBlockCommand.TryRead(original,root,out var command))
        {
            commandDigest=command.Digest;
            if(RebirthRemoteResourceBlockCommand.TryReadDeclined(original,root,out var declined))
            {
                decline=declined;declinedBody=new XElement("blockHostDeclined",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",command.Digest),
                    new XAttribute("source",source.ToString("N")),new XAttribute("binding",root.Binding),new XAttribute("ownerProof",command.OwnerProof)).ToString(SaveOptions.DisableFormatting);
            }
            RebirthRemoteResourceBlockHost.TryReadSaved(actor.world,original,out saved);
            RebirthRemoteResourceBlockHost.CaptureProjection(actor.world,original,out projection);
        }
        var body=new XElement("blockResult",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("nonce",nonce),new XAttribute("exchange",exchange),
            new XAttribute("admitted",admitted?1:0),new XAttribute("sourceWorld",sourceWorld),new XAttribute("source",source.ToString("N")),new XAttribute("binding",root?.Binding??string.Empty),new XAttribute("birth",birth),
            new XAttribute("phase",root?.Phase??0),new XAttribute("outcome",(int?)image?.Attribute("outcome")??0),new XAttribute("permission",permission?1:0),new XAttribute("rejecting",(int?)image?.Attribute("ownerRejecting")??0),
            new XAttribute("ownerProof",(string)image?.Attribute("ownerProof")??string.Empty),new XAttribute("noUse",(string)image?.Attribute("ownerNoUseProof")??string.Empty),new XAttribute("disposition",(string)image?.Attribute("disposition")??string.Empty),
            new XAttribute("command",commandDigest),new XAttribute("decline",decline??string.Empty),new XAttribute("saved",saved??string.Empty),
            new XElement("paid",root!=null&&(root.Phase==2||root.Phase==3&&(int?)image.Attribute("ownerRejecting")==1)?image.Element("removed").Value:Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(Array.Empty<ItemStack>()))),new XElement("declinedBody",declinedBody),new XElement("projection",projection??string.Empty));
        string text=body.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(text)>12*1024*1024)return;
        NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,text);
    }
}
