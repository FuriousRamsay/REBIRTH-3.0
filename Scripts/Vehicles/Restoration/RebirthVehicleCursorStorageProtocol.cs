using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

// Receipts are evidence of a particular authenticated native gesture, never item setters.
internal static class RebirthVehicleCursorStorageProtocol
{
    private sealed class ClientWait {internal RebirthVehicleCursorDestinationWitness.Witness Witness;internal EntityPlayerLocal Owner;internal Action<bool> Complete,AbortComplete;internal float Next;internal bool Query,Abort;}
    private sealed class ServerRecord {internal Guid Operation,Original;internal string Payload,Frame,Path;internal int Phase;internal bool Observed,LeaseLost,Started;internal RebirthVehicleCursorDestinationWitness.Witness Witness;internal EntityPlayer Actor;internal ClientInfo Peer;}
    private sealed class LocalAttempt {internal Guid Operation,Original;internal string Frame,Payload;internal EntityPlayerLocal Owner;internal World World;internal bool Dispatched;}
    private static ConditionalWeakTable<RebirthVehicleCursorDestinationWitness.Witness,LocalAttempt> attempts=new ConditionalWeakTable<RebirthVehicleCursorDestinationWitness.Witness,LocalAttempt>();
    private static readonly HashSet<Guid> attemptedOperations=new HashSet<Guid>();
    private static readonly Dictionary<Guid,ClientWait> clients=new Dictionary<Guid,ClientWait>();
    private static readonly Dictionary<Guid,ServerRecord> servers=new Dictionary<Guid,ServerRecord>();
    private static bool installed;private static float nextTick;
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        if(installed)return;
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.ProcessPackage)),
            prefix:new HarmonyLib.HarmonyMethod(typeof(RebirthVehicleCursorStorageProtocol),nameof(BeforeNativePacket)),
            finalizer:new HarmonyLib.HarmonyMethod(typeof(RebirthVehicleCursorStorageProtocol),nameof(AfterNativePacket)));
        foreach(string method in new[]{nameof(XUiC_ItemStack.HandleStackSwap),nameof(XUiC_ItemStack.HandleDropOne)})
            harmony.Patch(HarmonyLib.AccessTools.Method(typeof(XUiC_ItemStack),method),
                prefix:new HarmonyLib.HarmonyMethod(typeof(RebirthVehicleCursorStorageProtocol),nameof(BeforeNativeLocal)),
                finalizer:new HarmonyLib.HarmonyMethod(typeof(RebirthVehicleCursorStorageProtocol),nameof(AfterNativeLocal)));
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(LockManager),nameof(LockManager.UnlockRequestServer),new[]{typeof(int),typeof(bool)}),
            prefix:new HarmonyLib.HarmonyMethod(typeof(RebirthVehicleCursorStorageProtocol),nameof(BeforeUnlock)));
        ModEvents.GameUpdate.RegisterHandler(Tick);ModEvents.GameShutdown.RegisterHandler(Shutdown);ModEvents.WorldShuttingDown.RegisterHandler(ShutdownWorld);installed=true;
    }
    private static bool Lease(ServerRecord record)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return !record.LeaseLost&&ThreadManager.IsMainThread()&&connection?.IsServer==true&&ReferenceEquals(record.Actor.world,GameManager.Instance?.World)&&
            ReferenceEquals(record.Actor.world.GetEntity(record.Actor.entityId),record.Actor)&&CurrentCreation(record.Actor,record.Witness.Creation)&&
            (record.Peer==null?ReferenceEquals(record.Actor.world.GetPrimaryPlayer(),record.Actor):ReferenceEquals(connection.Clients?.ForEntityId(record.Actor.entityId),record.Peer))&&
            LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(record.Witness.Storage,0),out int owner)&&owner==record.Actor.entityId;
    }
    private static void BeforeUnlock(int _playerId)
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var record in servers.Values)if(record.Phase==1&&record.Actor.entityId==_playerId&&Lease(record))record.LeaseLost=true;
    }
    private static void BeforeNativePacket(NetPackageTileEntity __instance,World _world,out object __state)
    {
        __state=null;if(!ThreadManager.IsMainThread()||_world==null||_world.IsRemote()||!ReferenceEquals(_world,GameManager.Instance?.World))return;
        __state=servers.Values.FirstOrDefault(r=>r.Phase==1&&!r.Observed&&ReferenceEquals(r.Peer,__instance.Sender)&&r.Peer!=null&&
            r.Witness.Position==__instance.teWorldPos&&ReferenceEquals(_world.GetTileEntity(__instance.teWorldPos),r.Witness.Tile)&&Lease(r)&&r.Witness.MatchesBefore());
        if(__state is ServerRecord captured)captured.Started=true;
    }
    private static Exception AfterNativePacket(Exception __exception,object __state)
    {if(__exception==null&&__state is ServerRecord record&&Lease(record)&&record.Witness.MatchesAfterImage()){record.Observed=true;Advance(record);}return __exception;}
    private static void BeforeNativeLocal(XUiC_ItemStack __instance,out object __state)
    {
        __state=null;if(!ThreadManager.IsMainThread())return;
        var storage=__instance.GetParentByType<XUiC_LootWindow>()?.te;
        __state=servers.Values.FirstOrDefault(r=>r.Phase==1&&!r.Observed&&r.Peer==null&&ReferenceEquals(r.Actor,__instance.xui?.playerUI?.entityPlayer)&&
            ReferenceEquals(r.Witness.Storage,storage)&&r.Witness.Slot==__instance.SlotNumber&&Lease(r)&&r.Witness.MatchesBefore());
        if(__state is ServerRecord captured)captured.Started=true;
    }
    private static Exception AfterNativeLocal(Exception __exception,object __state)
    {if(__exception==null&&__state is ServerRecord record&&Lease(record)&&record.Witness.MatchesAfterImage()){record.Observed=true;Advance(record);}return __exception;}
    internal static bool Prepare(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner,Guid operation,Guid original,string frame,Action<bool> complete)
    {
        if(w==null||!OwnerMatches(w,owner)||operation==Guid.Empty||original==Guid.Empty||complete==null||
            !ReferenceEquals(w.CapturedWorld,owner.world)||!ReferenceEquals(w.CapturedOwner,owner)||w.UiGroup==null)return false;
        // Only a fresh captured witness/operation can establish local non-dispatch.
        // A reused witness invalidates the evidence rather than resetting it.
        if(attempts.TryGetValue(w,out var prior)){prior.Dispatched=true;return false;}
        if(w.operation!=Guid.Empty||w.Prepared||w.Saved||w.Aborted||clients.ContainsKey(operation)||servers.ContainsKey(operation)||!attemptedOperations.Add(operation))return false;
        string image;try{image=w.Encode();}catch{return false;}
        var attempt=new LocalAttempt{Operation=operation,Original=original,Frame=frame,Payload=image,Owner=owner,World=owner.world as World};
        attempts.Add(w,attempt);
        w.Original=original;w.OriginalFrame=frame;w.operation=operation;
        if(!w.MatchesBeforeLive(owner)||frame==null||frame.Length>4*1024*1024)return false;
        if(clients.Count>=16||clients.ContainsKey(operation))return false;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null)return false;
        if(!owner.world.IsRemote())
        {
            attempt.Dispatched=true;
            byte result=Server(owner.world as World,null,owner.entityId,operation,original,frame,w.Encode(),false);
            if(result==1){w.Prepared=true;complete(true);}else if(result==0)complete(false);
            return result!=3;
        }
        if(!connection.IsClient||connection.IsServer)return false;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleCursorStorageRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthVehicleCursorStorageReply));
            var wait=new ClientWait{Witness=w,Owner=owner,Complete=complete,Next=Time.realtimeSinceStartup+1f};clients.Add(operation,wait);
            attempt.Dispatched=true; // Send may throw after delivering; never call that unsent.
            Send(operation,wait);return true;
        }
        catch{clients.Remove(operation);return false;}
    }
    internal static bool Verify(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||owner==null||operation==Guid.Empty||!CurrentOwner(w,owner))return false;
        if(w.Saved)return true;
        if(!owner.world.IsRemote())
        {
            byte result=Server(owner.world as World,null,owner.entityId,operation,w.Original,w.OriginalFrame,w.Encode(),true);
            if(result==5)w.SnapshotAccepted=true;
            if(result==2){w.Saved=true;w.Dispose();return true;}return false;
        }
        if(clients.ContainsKey(operation))return false;
        if(clients.Count>=16)return false;
        var wait=new ClientWait{Witness=w,Owner=owner,Query=true,Next=Time.realtimeSinceStartup+1f};clients.Add(operation,wait);
        try{Send(operation,wait);}catch{clients.Remove(operation);}return false;
    }
    internal static bool Cancel(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner,Guid operation,Guid original,string frame,Action<bool> completed)
    {
        if(!OwnerMatches(w,owner)||operation==Guid.Empty||original==Guid.Empty||completed==null)return false;
        if(KnownUnsent(w,owner,operation,original,frame)){w.Aborted=true;completed(true);return true;}
        w.operation=operation;w.Original=original;w.OriginalFrame=frame;
        if(!owner.world.IsRemote())
        {byte result=Server(owner.world as World,null,owner.entityId,operation,original,frame,w.Encode(),false,true);if(result==4){w.Aborted=true;completed(true);}return true;}
        if(clients.TryGetValue(operation,out var existing))
        {if(existing.Witness.Encode()!=w.Encode()||!ReferenceEquals(existing.Owner,owner))return false;existing.Abort=true;existing.Query=false;existing.Complete=null;existing.AbortComplete=completed;existing.Next=0f;return true;}
        if(clients.Count>=16)return false;
        var wait=new ClientWait{Witness=w,Owner=owner,Abort=true,AbortComplete=completed,Next=Time.realtimeSinceStartup+1f};clients.Add(operation,wait);
        try{Send(operation,wait);}catch{}return true;
    }
    internal static bool ObserveAbort(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner,Guid operation)
    {
        if(!OwnerMatches(w,owner)||operation==Guid.Empty)return false;
        if(KnownUnsent(w,owner,operation,w.Original,w.OriginalFrame)){w.Aborted=true;return true;}
        if(w.Aborted)return true;
        if(!owner.world.IsRemote())
        {if(Server(owner.world as World,null,owner.entityId,operation,Guid.Empty,"",w.Encode(),false,true)==4){w.Aborted=true;return true;}return false;}
        if(clients.ContainsKey(operation)||clients.Count>=16)return false;
        var wait=new ClientWait{Witness=w,Owner=owner,Abort=true,Next=Time.realtimeSinceStartup+1f};clients.Add(operation,wait);
        try{Send(operation,wait);}catch{}return false;
    }
    private static bool KnownUnsent(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner,Guid operation,Guid original,string frame)
    {
        try{return attempts.TryGetValue(w,out var attempt)&&!attempt.Dispatched&&attempt.Operation==operation&&attempt.Original==original&&attempt.Frame==frame&&
            ReferenceEquals(attempt.Owner,owner)&&ReferenceEquals(attempt.World,owner.world)&&w.Encode()==attempt.Payload&&
            !clients.ContainsKey(operation)&&!servers.ContainsKey(operation);}
        catch{return false;}
    }
    private static bool AbortAssignment(string path,Guid original,Guid operation,RebirthVehicleCursorDestinationWitness.Witness witness)
    {
        if(!ValidAbort(Read(path),original,operation,witness.Owner,witness.World,witness.Creation,null))return false;
        string assignment=Path.Combine(Path.GetDirectoryName(path),"original_"+original.ToString("N")+".xml");
        if(!File.Exists(assignment))return false;
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096};
        XElement node;using(var reader=XmlReader.Create(assignment,settings))node=XDocument.Load(reader).Root;
        if(node==null||node.Name!="vehicleCursorOriginal"||node.HasElements||(string)node.Attribute("operation")!=operation.ToString("N")||
            (string)node.Attribute("original")!=original.ToString("N")||(string)node.Attribute("owner")!=witness.Owner||(string)node.Attribute("world")!=witness.World)return false;
        node.SetAttributeValue("aborted",true);
        if(!RebirthAtomicXmlFile.TryWrite(assignment,new XDocument(node),out _))return false;
        using(var reader=XmlReader.Create(assignment,settings))return XNode.DeepEquals(node,XDocument.Load(reader).Root);
    }
    private static bool ValidAbort(XElement receipt,Guid original,Guid operation,string owner,string world,string creation,string frame)
    {
        if(receipt==null||(int?)receipt.Attribute("phase")!=3||(string)receipt.Attribute("original")!=original.ToString("N")||
            (string)receipt.Attribute("operation")!=operation.ToString("N")||frame!=null&&(string)receipt.Element("frame")!=frame)return false;
        string payload=(string)receipt.Element("payload");
        if((string)receipt.Attribute("payloadHash")!=RebirthVehicleClientCustody.FrameHash(payload)||
            !RebirthVehicleCursorDestinationWitness.TryRead(payload,out var decoded)||!(decoded is RebirthVehicleCursorDestinationWitness.Witness image)||
            image.Owner!=owner||image.World!=world||image.Creation!=creation)return false;
        var evidence=receipt.Element("abort");
        return evidence!=null&&!evidence.HasElements&&evidence.Attributes().Count()==3&&(string)evidence.Attribute("operation")==operation.ToString("N")&&
            (bool?)evidence.Attribute("neverStarted")==true&&(string)evidence.Attribute("beforeHash")==RebirthStationPublicationRecord.Digest(image.Before);
    }
    private static bool CurrentOwner(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner)
        =>ReferenceEquals(owner.world,GameManager.Instance?.World)&&ReferenceEquals(owner.world.GetPrimaryPlayer(),owner)&&owner.world.worldState?.Guid==w.World&&
        RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.CanonicalId==w.Owner&&
        XUiC_RebirthVehicleRestoration.TryGetRequestCharacter(owner,out var creation)&&creation==w.Creation;
    internal static bool OwnerMatches(RebirthVehicleCursorDestinationWitness.Witness w,EntityPlayerLocal owner)
        =>ThreadManager.IsMainThread()&&w!=null&&owner!=null&&CurrentOwner(w,owner);
    internal static bool CurrentCreation(EntityPlayer actor,string creation)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return creation==string.Empty;
        return RebirthWorldCharacterService.TryGet(actor,out var record)&&record?.IsComplete==true&&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&RebirthSurvivorRequestScope.Matches(creation,record.Origin?.CreationId);
    }
    private static void Send(Guid operation,ClientWait wait)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null||!connection.IsClient||connection.IsServer||!CurrentOwner(wait.Witness,wait.Owner))return;
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthVehicleCursorStorageRequest>()
            .Setup(wait.Owner.entityId,operation,wait.Witness.Original,wait.Witness.OriginalFrame??"",wait.Witness.Encode(),wait.Query,wait.Abort));
    }
    internal static void Receive(World world,Guid operation,string payload,byte outcome,byte kind)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world==null||!world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||
            connection==null||!connection.IsClient||connection.IsServer||!clients.TryGetValue(operation,out var wait)||
            !CurrentOwner(wait.Witness,wait.Owner)||wait.Witness.Encode()!=payload)return;
        if(kind!=(wait.Abort?(byte)2:wait.Query?(byte)1:(byte)0))return;
        if(outcome==3)return; // Unknown never converts to a definite abort or saved credit.
        clients.Remove(operation);
        if(wait.Abort){if(outcome==4){wait.Witness.Aborted=true;wait.AbortComplete?.Invoke(true);}else wait.AbortComplete?.Invoke(false);return;}
        if(wait.Query){if(outcome==2)wait.Witness.Saved=true;else if(outcome==5)wait.Witness.SnapshotAccepted=true;return;}
        if(outcome==1){wait.Witness.Prepared=true;wait.Complete?.Invoke(true);}
        else if(outcome==0)wait.Complete?.Invoke(false);
    }
    private static string RecordPath(World world,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||!Guid.TryParse(world.worldState?.Guid,out _))return null;
        string root=Path.GetFullPath(GameIO.GetSaveGameDir());
        return Path.Combine(root,"RebirthData","VehicleCursorDestinations",operation.ToString("N")+".xml");
    }
    private static XElement Read(string path)
    {
        if(!File.Exists(path))return null;
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024};
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=XmlReader.Create(stream,settings))
        {var node=XDocument.Load(reader,LoadOptions.None).Root;if(node==null||node.Name!="vehicleCursorDestination"||(int?)node.Attribute("version")!=1||node.Attributes().Count()!=5||
            node.Elements().Any(e=>e.Name!="payload"&&e.Name!="frame"&&e.Name!="proof"&&e.Name!="abort")||node.Elements("payload").Count()!=1||node.Elements("frame").Count()!=1||node.Elements("proof").Count()>1||node.Elements("abort").Count()>1||
            !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
            !Guid.TryParseExact((string)node.Attribute("original"),"N",out var original)||original==Guid.Empty||
            ((int?)node.Attribute("phase")!=1&&(int?)node.Attribute("phase")!=2&&(int?)node.Attribute("phase")!=3)||
            node.Element("payload").HasAttributes||node.Element("payload").HasElements||node.Element("frame").HasAttributes||node.Element("frame").HasElements||
            ((int?)node.Attribute("phase")==2)!= (node.Element("proof")!=null)||((int?)node.Attribute("phase")==3)!=(node.Element("abort")!=null))throw new InvalidDataException("Destination receipt malformed.");return node;}
    }
    private static bool Save(ServerRecord record,XElement proof=null)
    {
        var node=new XElement("vehicleCursorDestination",new XAttribute("version",1),new XAttribute("operation",record.Operation.ToString("N")),
            new XAttribute("original",record.Original.ToString("N")),new XAttribute("phase",record.Phase),new XAttribute("payloadHash",RebirthVehicleClientCustody.FrameHash(record.Payload)),
            new XElement("payload",record.Payload),new XElement("frame",record.Frame));
        if(proof!=null)node.Add(new XElement(proof));
        if(!RebirthAtomicXmlFile.TryWrite(record.Path,new XDocument(node),out _))return false;
        return XNode.DeepEquals(node,Read(record.Path));
    }
    internal static byte Server(World world,ClientInfo peer,int playerId,Guid operation,Guid original,string frame,string payload,bool query,bool abort=false)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||operation==Guid.Empty||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World))return 0;
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection?.IsServer!=true||peer!=null&&(!ReferenceEquals(connection.Clients?.ForEntityId(playerId),peer)||peer.entityId!=playerId))return 0;
            var actor=world.GetEntity(playerId) as EntityPlayer;if(actor==null||!ReferenceEquals(actor.world,world)||peer==null&&!ReferenceEquals(world.GetPrimaryPlayer(),actor))return 0;
            string path=RecordPath(world,operation);if(path==null)return 0;
            XElement saved=Read(path);
            if(saved!=null)
            {
                if((string)saved.Attribute("operation")!=operation.ToString("N")||(string)saved.Element("payload")!=payload||
                    (string)saved.Attribute("payloadHash")!=RebirthVehicleClientCustody.FrameHash(payload))return 0;
                Guid retained=Guid.Parse((string)saved.Attribute("original"));string frozen=(string)saved.Element("frame");
                if(original!=Guid.Empty&&original!=retained||!string.IsNullOrEmpty(frame)&&frame!=frozen)return 0;
                original=retained;frame=frozen;
            }
            if(!RebirthVehicleCursorDestinationWitness.TryRead(payload,out var decoded)||!(decoded is RebirthVehicleCursorDestinationWitness.Witness witness)||
                world.worldState?.Guid!=witness.World||!CurrentCreation(actor,witness.Creation))return 0;
            RebirthStablePlayerIdentity identity;
            if(!(peer==null?RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity):RebirthStablePlayerIdentity.TryFromClientInfo(peer,out identity))||identity.CanonicalId!=witness.Owner)return 0;
            if(saved!=null&&(int?)saved.Attribute("phase")==2)
            {
                XElement proof=saved.Element("proof");
                // Only an original final-publication certificate, not a cold afterimage.
                if(proof==null||(string)proof.Attribute("operation")!=operation.ToString("N")||
                    (string)proof.Attribute("payloadHash")!=RebirthVehicleClientCustody.FrameHash(payload)||
                    !long.TryParse((string)proof.Attribute("generation"),out long generation)||generation<=0||
                    string.IsNullOrEmpty((string)proof.Attribute("regionDigest"))||string.IsNullOrEmpty((string)proof.Attribute("spanHash")))return 3;
                return query?(byte)2:(byte)3;
            }
            if(saved!=null&&(int?)saved.Attribute("phase")==3)
            {
                var evidence=saved.Element("abort");
                if(evidence==null||evidence.HasElements||evidence.Attributes().Count()!=3||(string)evidence.Attribute("operation")!=operation.ToString("N")||
                    (bool?)evidence.Attribute("neverStarted")!=true||(string)evidence.Attribute("beforeHash")!=RebirthStationPublicationRecord.Digest(witness.Before))return 3;
                return abort?AbortAssignment(path,original,operation,witness)?(byte)4:(byte)3:(byte)0;
            }
            if(!NetPackageRebirthVehicleAssemblyRequest.TryValidateCustodyPickup(world,peer,original,witness.World,witness.Creation,frame,out var result))return 0;
            if(servers.TryGetValue(operation,out var record))
            {
                if(record.Payload!=payload||record.Frame!=frame||record.Original!=original||!ReferenceEquals(record.Actor,actor)||!ReferenceEquals(record.Peer,peer))return 3;
                if(abort)
                {
                    if(record.Started||record.Observed||!record.Witness.MatchesBoundBefore()||!ReferenceEquals(world.GetTileEntity(witness.Position),record.Witness.Tile))return 3;
                    record.Phase=3;
                    var evidence=new XElement("abort",new XAttribute("operation",operation.ToString("N")),new XAttribute("beforeHash",RebirthStationPublicationRecord.Digest(record.Witness.Before)),new XAttribute("neverStarted",true));
                    if(!Save(record,evidence)){record.Phase=1;return 3;}
                    record.Witness.Dispose();servers.Remove(operation);
                    return AbortAssignment(path,original,operation,witness)?(byte)4:(byte)3;
                }
                if(query){Advance(record);return record.Phase==2?(byte)2:record.Witness.FrozenGeneration>0?(byte)5:(byte)3;}
                return record.Witness.MatchesBefore()?(byte)1:(byte)3;
            }
            // A prepared receipt restored from disk alone never proves an unobserved gesture.
            if(saved!=null)return 3;
            if(abort)return 3; // No live preparation evidence; never infer cold absence as an abort.
            string assignment=Path.Combine(Path.GetDirectoryName(path),"original_"+original.ToString("N")+".xml");
            if(File.Exists(assignment))
            {
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096};
                using(var reader=XmlReader.Create(assignment,settings))
                {var claim=XDocument.Load(reader).Root;if(claim==null||claim.Name!="vehicleCursorOriginal"||claim.HasElements||
                    (claim.Attributes().Count()!=4&&claim.Attributes().Count()!=5)||(string)claim.Attribute("original")!=original.ToString("N")||
                    (string)claim.Attribute("owner")!=witness.Owner||(string)claim.Attribute("world")!=witness.World)return 3;
                    // A same-operation claim without its receipt is unresolved.
                    if((string)claim.Attribute("operation")==operation.ToString("N"))return 3;
                    if((string)claim.Attribute("operation")!=operation.ToString("N"))
                    {if((bool?)claim.Attribute("aborted")!=true||!Guid.TryParseExact((string)claim.Attribute("operation"),"N",out var old))return 3;
                        var oldReceipt=Read(RecordPath(world,old));if(!ValidAbort(oldReceipt,original,old,witness.Owner,witness.World,witness.Creation,frame))return 3;}}
            }
            if(query)return 0;
            record=new ServerRecord{Operation=operation,Original=original,Payload=payload,Frame=frame,Path=path,Phase=1,Witness=witness,Actor=actor,Peer=peer};
            // The authenticated Prepare is refused before Ready/native invocation.
            // Persist that event; a later Abort or cold absence cannot prove it.
            if(servers.Count>=16||!witness.BindServer(world,actor)||!witness.MatchesBefore()||
                (witness.Position.ToVector3()-actor.position).sqrMagnitude>64f||witness.Grid.IsLocked(witness.Slot)||
                !RemoteResourceAccess.AuthorizeComposite(witness.Tile,actor,false,out _)||
                !LockManager.Instance.singleLocks.TryGetByValue(new LockManager.LockEntry(witness.Storage,0),out int nativeOwner)||nativeOwner!=actor.entityId||
                string.IsNullOrEmpty(result.PickupNativeItem)||!RebirthNativeItemCodec.TryDecode(result.PickupNativeItem,out var pickup)||pickup.IsEmpty()||
                !witness.ValidPickupDelta(pickup))return RefusePreparation(record,assignment);
            witness.operation=operation;
            if(!Save(record))return 3;
            var claimNode=new XElement("vehicleCursorOriginal",new XAttribute("original",original.ToString("N")),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("owner",witness.Owner),new XAttribute("world",witness.World));
            if(!RebirthAtomicXmlFile.TryWrite(assignment,new XDocument(claimNode),out _))return 3;
            using(var reader=XmlReader.Create(assignment,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))
                if(!XNode.DeepEquals(claimNode,XDocument.Load(reader).Root))return 3;
            witness.ServerCurrentExtra=()=>record.Observed&&Lease(record);
            witness.ServerFrozenCurrent=()=>ThreadManager.IsMainThread()&&ReferenceEquals(world,GameManager.Instance?.World)&&world.worldState?.Guid==witness.World&&
                ReferenceEquals(actor.world,world)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&CurrentCreation(actor,witness.Creation)&&
                (peer==null?ReferenceEquals(world.GetPrimaryPlayer(),actor):ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId),peer));
            servers.Add(operation,record);return 1;
        }
        catch{return 3;}
    }
    private static byte RefusePreparation(ServerRecord record,string assignment)
    {
        // Server checked the original journal and any prior aborted assignment.
        record.Phase=3;
        var evidence=new XElement("abort",new XAttribute("operation",record.Operation.ToString("N")),
            new XAttribute("beforeHash",RebirthStationPublicationRecord.Digest(record.Witness.Before)),new XAttribute("neverStarted",true));
        try
        {
            if(!Save(record,evidence))return 3;
            var claim=new XElement("vehicleCursorOriginal",new XAttribute("original",record.Original.ToString("N")),
                new XAttribute("operation",record.Operation.ToString("N")),new XAttribute("owner",record.Witness.Owner),
                new XAttribute("world",record.Witness.World),new XAttribute("aborted",true));
            if(!RebirthAtomicXmlFile.TryWrite(assignment,new XDocument(claim),out _))return 3;
            return AbortAssignment(record.Path,record.Original,record.Operation,record.Witness)?(byte)0:(byte)3;
        }
        finally{record.Witness.Dispose();}
    }
    private static void Advance(ServerRecord record)
    {
        if(record.Phase!=1||!record.Observed||record.Witness==null||record.Witness.FrozenGeneration==0&&!Lease(record)||!record.Witness.CurrentAfter())return;
        if(!record.Witness.RequestAndVerifySaved(null,record.Operation))
        {if(Lease(record))record.Witness.TryFreezeSnapshot();return;}
        var w=record.Witness;
        if(w.Serialized==null||w.Generation<=0||w.RegionPath==null||w.PayloadDigest==null)return;
        var proof=new XElement("proof",new XAttribute("operation",record.Operation.ToString("N")),
            new XAttribute("payloadHash",RebirthVehicleClientCustody.FrameHash(record.Payload)),new XAttribute("regionDigest",w.PayloadDigest),
            new XAttribute("spanHash",RebirthStationPublicationRecord.Digest(w.Serialized)),new XAttribute("spanStart",w.Span),
            new XAttribute("spanLength",w.Serialized.Length),new XAttribute("generation",w.Generation),new XAttribute("region",Path.GetFileName(w.RegionPath)));
        record.Phase=2;if(!Save(record,proof)){record.Phase=1;return;}w.Dispose();servers.Remove(record.Operation);
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread()||Time.realtimeSinceStartup<nextTick)return;nextTick=Time.realtimeSinceStartup+0.25f;
        foreach(var entry in servers.ToArray())
        {
            var r=entry.Value;
            if(!ReferenceEquals(r.Actor?.world,GameManager.Instance?.World)||r.Peer!=null&&!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(r.Actor.entityId),r.Peer))
            {r.Witness?.Dispose();servers.Remove(entry.Key);continue;}
            Advance(r);
        }
        foreach(var entry in clients.ToArray())
        {
            var wait=entry.Value;
            if(!CurrentOwner(wait.Witness,wait.Owner)){clients.Remove(entry.Key);continue;}
            if(!wait.Query&&!wait.Abort&&(wait.Witness.UiGroup?.isShowing!=true||RebirthVehicleCustodyUiGeneration.Read(wait.Witness.UiGroup)!=wait.Witness.UiGeneration))
            {var callback=wait.Complete;wait.Complete=null;wait.Abort=true;wait.Next=0f;callback?.Invoke(false);} // Custody routes false through durable Cancel; no item movement.
            if(Time.realtimeSinceStartup>=wait.Next){wait.Next=Time.realtimeSinceStartup+1f;try{Send(entry.Key,wait);}catch{}}
        }
    }
    private static void Shutdown(ref ModEvents.SGameShutdownData data){foreach(var r in servers.Values)r.Witness?.Dispose();servers.Clear();clients.Clear();ResetAttempts();nextTick=0f;}
    private static void ShutdownWorld(ref ModEvents.SWorldShuttingDownData data){foreach(var r in servers.Values)r.Witness?.Dispose();servers.Clear();clients.Clear();ResetAttempts();nextTick=0f;}
    private static void ResetAttempts(){attempts=new ConditionalWeakTable<RebirthVehicleCursorDestinationWitness.Witness,LocalAttempt>();attemptedOperations.Clear();}
}

[Preserve]
public sealed class NetPackageRebirthVehicleCursorStorageRequest:NetPackage
{
    private int owner;private Guid operation,original;private string frame,payload;private bool query,abort;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthVehicleCursorStorageRequest Setup(int actor,Guid op,Guid source,string frozen,string image,bool verify,bool cancel=false)
    {owner=actor;operation=op;original=source;frame=frozen;payload=image;query=verify;abort=cancel;return this;}
    internal static string ReadText(BinaryReader r,int maximum){int length=r.ReadInt32();if(length<0||length>maximum)throw new InvalidDataException();byte[] bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
    internal static void WriteText(BinaryWriter w,string value,int maximum){byte[] bytes=Encoding.UTF8.GetBytes(value??"");if(bytes.Length>maximum)throw new InvalidDataException();w.Write(bytes.Length);w.Write(bytes);}
    public override void read(PooledBinaryReader reader)
    {BinaryReader r=(BinaryReader)reader;if(r.ReadByte()!=1)throw new InvalidDataException();owner=r.ReadInt32();operation=new Guid(r.ReadBytes(16));original=new Guid(r.ReadBytes(16));byte kind=r.ReadByte();if(kind>2)throw new InvalidDataException();query=kind==1;abort=kind==2;frame=ReadText(r,4*1024*1024);payload=ReadText(r,3*1024*1024);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);BinaryWriter w=(BinaryWriter)writer;w.Write((byte)1);w.Write(owner);w.Write(operation.ToByteArray());w.Write(original.ToByteArray());w.Write(abort?(byte)2:query?(byte)1:(byte)0);WriteText(w,frame,4*1024*1024);WriteText(w,payload,3*1024*1024);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||connection?.IsServer!=true||
            Sender==null||!ReferenceEquals(connection.Clients?.ForEntityId(owner),Sender)||!ValidEntityIdForSender(owner))return;
        byte outcome=RebirthVehicleCursorStorageProtocol.Server(world,Sender,owner,operation,original,frame,payload,query,abort);
        Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthVehicleCursorStorageReply>().Setup(operation,payload,outcome,abort?(byte)2:query?(byte)1:(byte)0));
    }
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthVehicleCursorStorageReply:NetPackage
{
    private Guid operation;private string payload;private byte outcome,kind;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthVehicleCursorStorageReply Setup(Guid op,string image,byte result,byte mode){operation=op;payload=image;outcome=result;kind=mode;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader r=(BinaryReader)reader;if(r.ReadByte()!=1)throw new InvalidDataException();operation=new Guid(r.ReadBytes(16));outcome=r.ReadByte();kind=r.ReadByte();if(outcome>5||kind>2)throw new InvalidDataException();payload=NetPackageRebirthVehicleCursorStorageRequest.ReadText(r,3*1024*1024);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter w=(BinaryWriter)writer;w.Write((byte)1);w.Write(operation.ToByteArray());w.Write(outcome);w.Write(kind);NetPackageRebirthVehicleCursorStorageRequest.WriteText(w,payload,3*1024*1024);}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthVehicleCursorStorageProtocol.Receive(world,operation,payload,outcome,kind);
    public int GetLength()=>0;
}
