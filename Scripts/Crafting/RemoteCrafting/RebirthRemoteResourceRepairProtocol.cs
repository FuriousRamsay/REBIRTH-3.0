using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

// Intent-only request transport. Native owner images are observed from the
// authenticated final primary file, never installed from this wire payload.
internal static class RebirthRemoteResourceRepairProtocol
{
    private sealed class Pending
    {
        internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal ItemActionEntryRepair Entry;
        internal float Next;
        internal bool Rejecting;
        internal bool Used;
        internal string NoUseProof,SourceBinding;
        internal Guid SourceOperation;
        internal ItemStack[] Paid;
        internal XElement Refund;
        internal bool RefundApplied;
    }
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    private static readonly RebirthRemoteResourceRepairSendQueue wire=new RebirthRemoteResourceRepairSendQueue();
    private static World world;private static string directory;private static bool installed;private static float scanAt;
    internal static void Install()
    {
        if(installed)return;ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
    }
    internal static bool Begin(EntityPlayerLocal player,ItemActionEntryRepair entry,ulong epoch,ulong request,List<ItemStack> full,List<ItemStack> remote)
    {
        try
        {
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!Supported(connection?.connectionToServer?[0]))
            {GameManager.ShowTooltip(player,"Remote repair is unavailable for this network connection.");return false;}
            if(!installed||pending.Count>=32||!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||!RebirthRemoteResourceClientOriginal.TryPrepareRepair(player,entry,epoch,request,full,remote,out var original)||
                !RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))return false;
            if(!ReferenceEquals(world,player.world)||directory!=GameIO.GetSaveGameDir()){pending.Clear();wire.Clear();world=player.world;directory=GameIO.GetSaveGameDir();scanAt=0;}
            if(pending.TryGetValue(original.Operation,out var retained))return retained.Original.Digest==original.Digest;
            pending.Add(original.Operation,new Pending{Original=original,Entry=entry});
            // Native EntityBuffs.Write omits zero CVars from save files.
            // Ten is the nonzero BEFORE-admission receipt, not owner permission.
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),10f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool TryAttach(ItemActionEntryRepair entry)
    {
        try
        {
            var player=entry?.ItemController?.xui?.playerUI?.entityPlayer;
            if(!RebirthRemoteResourceOwnerAction.TrySource(entry,out int kind,out int slot,out string input))return false;
            foreach(var retained in pending.Values)
            {
                var action=retained.Original.Copy().Element("action");
                if(RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(retained.Original)||!RebirthRemoteResourceClientOriginal.Current(player,retained.Original)||(int)action.Attribute("sourceKind")!=kind||
                    (int)action.Attribute("sourceSlot")!=slot||action.Element("nativeInput").Value!=input)continue;
                retained.Entry=entry;retained.Next=0;return true;
            }
            return false;
        }
        catch{return false;}
    }
    internal static void CancelUnadmittedForUi(XUi ui)
    {
        var player=ui?.playerUI?.entityPlayer;if(player==null)return;
        foreach(var entry in pending.Values.ToArray())
            if(!HasAdmissionAck(entry.Original)&&RebirthRemoteResourcePreAdmissionRefusal.RequestClient(player,entry.Original))
            {entry.Entry=null;entry.Next=0;RebirthRemoteResourcePreAdmissionRefusal.SendRequested(player,entry.Original);}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var current=GameManager.Instance?.World;string path=GameIO.GetSaveGameDir();
        if(!ReferenceEquals(world,current)||directory!=path)
        {
            pending.Clear();wire.Clear();world=current;directory=path;scanAt=0;
            // Only existing cooking-pull originals need this legacy bootstrap;
            // fresh ingredient pulls use the native cooking path.
            if(current!=null&&!string.IsNullOrEmpty(path)&&
               Directory.Exists(Path.Combine(path,"REBIRTH","CookingPullRequests")))
                RebirthCookingLegacyPull.InstallRecovery();
        }
        wire.Pump(16);
        if(!installed||current?.IsRemote()!=true)return;var player=current.GetPrimaryPlayer();
        if(player==null||!player.IsSpawned()||player.IsDead())return;
        float now=Time.realtimeSinceStartup;
        if(now>=scanAt)
        {
            scanAt=now+5f;
            if(RebirthRemoteResourceClientOriginal.TryReadPending(out var originals))foreach(var original in originals)
            {
                if(original.Kind!=RemoteResourceClientOperation.ItemRepair||!RebirthRemoteResourceRepairAdoption.QueryScope(player,original)||pending.ContainsKey(original.Operation)||pending.Count>=32)continue;
                // Cold originals only query their retained identity. No callback
                // reference is reconstructed from a coincidental current item.
                pending.Add(original.Operation,new Pending{Original=original});
                if(player.Died==original.Deaths&&!RebirthRemoteResourceClientOriginal.TryReadRepairAction(original,out _)&&
                    player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation))==0f&&
                    RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(RebirthCamperEnergyActorImage.Capture(player)))
                {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),10f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
                bool Scope()=>RebirthRemoteResourceRepairAdoption.QueryScope(player,original);
                try
                {
                    if(RebirthRemoteResourceRepairRefund.Bootstrap(player,original,Scope))continue;
                    if(player.Died!=original.Deaths)continue; // Query only; no old-life native hold or setter.
                    if(!RebirthRemoteResourceClientOriginal.TryReadRepairAction(original,out var action))RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original);
                    else if(RebirthRemoteResourceClientOriginal.TryReadRepairSource(original,out _,out var source,out var binding,out _))
                    {
                        if(!RebirthRemoteResourceOwnerAction.TryHoldRestoredRepairUsage(player,original,Scope))
                            RebirthRemoteResourceOwnerAction.TryHold(player,original,source,binding,RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value),RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value),Scope);
                    }
                }
                catch{/* Unknown original remains retained, never applied. */}
            }
        }
        foreach(var entry in pending.Values.ToArray())
        {
            if(!RebirthRemoteResourceRepairAdoption.QueryScope(player,entry.Original)||now<entry.Next)continue;
            entry.Next=now+1f;
            try
            {
                if(RebirthRemoteResourcePreAdmissionRefusal.HasClientTerminal(entry.Original))
                {if(RebirthRemoteResourceClientOriginal.CompleteNeverAdmitted(player,entry.Original))pending.Remove(entry.Original.Operation);continue;}
                if(RebirthRemoteResourcePreAdmissionRefusal.ClientRequested(entry.Original))
                {entry.Entry=null;RebirthRemoteResourcePreAdmissionRefusal.SendRequested(player,entry.Original);}
                bool Auth()=>RebirthRemoteResourceRepairAdoption.QueryScope(player,entry.Original)&&pending.TryGetValue(entry.Original.Operation,out var same)&&ReferenceEquals(same,entry);
                if(entry.NoUseProof!=null)
                {
                    if(RebirthRemoteResourceRepairRefund.TryPrepare(player,entry.Original,entry.SourceOperation,entry.SourceBinding,entry.NoUseProof,entry.Paid,Auth,out var refund))
                    {entry.Refund=refund;entry.RefundApplied=entry.RefundApplied||RebirthRemoteResourceRepairRefund.IsApplied(player,entry.Original,refund);Send(player,entry.Original,entry.RefundApplied?7:6,refund.ToString(SaveOptions.DisableFormatting));}
                    else Send(player,entry.Original,0,"");
                    continue;
                }
                int stage=0;string action="";
                if(player.Died>entry.Original.Deaths){entry.Entry=null;Send(player,entry.Original,10,"");continue;}
                if(RebirthRemoteResourceClientOriginal.TryReadRepairAction(entry.Original,out var planned))
                {
                    action=planned.ToString(SaveOptions.DisableFormatting);stage=1;
                    if(entry.Rejecting)stage=3;
                    else if(entry.Used)stage=2;
                }
                Send(player,entry.Original,stage,action);
            }
            catch{/* Retain the exact original; no callback or compensation inferred. */}
        }
    }
    private static void Send(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,int stage,string action)
    {
        if(wire.IsQueued(RequestKey(original)))return;
        var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
        if(stage==1||stage==2||stage==4||stage==6||stage==7)GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        // Do not duplicate the potentially large immutable original in every
        // phase. Later phases reference the host's already published admission.
        var source=stage==0&&!HasAdmissionAck(original)?original.Copy():new XElement("originalRef",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("epoch",original.Epoch),new XAttribute("request",original.Request));
        var body=new XElement("repairRequest",new XAttribute("version",1),new XAttribute("stage",stage),source,new XElement("ownerActionBody",action));
        NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,persistent.PrimaryId,original,body.ToString(SaveOptions.DisableFormatting));
    }
    private static string RequestKey(RebirthRemoteResourceClientOriginal.Intent original)=>"repair-request|"+original.World+"|"+original.Operation.ToString("N");
    private static string ResultKey(int actor,RebirthRemoteResourceClientOriginal.Intent original)=>"repair-result|"+original.World+"|"+actor+"|"+original.Operation.ToString("N");
    private static bool Ready(INetConnection connection)
    {
        if(connection==null||connection.IsDisconnected())return false;
        if(connection is NetConnectionSimple simple)
        {
            lock(simple.writerListLockObj)
                // The buffer counts are readonly conservative pressure hints;
                // native writer alone mutates them. Never enumerate/mutate it.
                return simple.writerListPending.Count<32&&simple.reliableBufsToSend.Count==0&&simple.unreliableBufsToSend.Count==0;
        }
        if(connection is NetConnectionSteam steam){lock(steam.packetsToSend)return steam.packetsToSend.Count<32;}
        return false;
    }
    private static bool Supported(INetConnection connection)=>connection!=null&&!connection.IsDisconnected()&&(connection is NetConnectionSimple||connection is NetConnectionSteam);
    internal static bool TransportReady(INetConnection connection)=>Ready(connection);
    internal static bool SupportsClientTransport()=>Supported(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]);
    internal static bool SupportsServerActor(EntityPlayer actor)=>actor!=null&&Supported(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId)?.netConnection?[0]);
    internal static void QueueCooking(bool result,int entityId,Guid operation,string nativeWorld,string canonicalOwner,string creation,RebirthRemoteResourceFragments.Body body,Action<int> send)
    {
        Install();var current=GameManager.Instance?.World;var actor=current?.GetEntity(entityId) as EntityPlayer;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var client=result?manager?.Clients.ForEntityId(entityId):null;var native=result?client?.netConnection?[0]:manager?.connectionToServer?[0];
        string save=GameIO.GetSaveGameDir();int deaths=actor?.Died??-1;
        bool Current()=>current!=null&&ReferenceEquals(GameManager.Instance?.World,current)&&ReferenceEquals(current.GetEntity(entityId),actor)&&actor!=null&&!actor.IsDead()&&actor.Died==deaths&&
            current.worldState.Guid==nativeWorld&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&save==GameIO.GetSaveGameDir()&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==canonicalOwner&&
            (result?current.IsRemote()==false&&ReferenceEquals(manager?.Clients.ForEntityId(entityId),client)&&ReferenceEquals(client?.netConnection?[0],native):
                current.IsRemote()&&ReferenceEquals(current.GetPrimaryPlayer(),actor)&&manager?.IsConnected==true&&ReferenceEquals(manager.connectionToServer?[0],native));
        wire.Enqueue((result?"cooking-result|":"cooking-request|")+nativeWorld+"|"+entityId+"|"+operation.ToString("N"),body,Current,()=>Ready(native),send);
    }
    internal static void QueueRequest(int playerId,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,Action<int> send)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var actor=GameManager.Instance?.World?.GetPrimaryPlayer();var native=manager?.connectionToServer?[0];string save=GameIO.GetSaveGameDir();
        int deaths=actor?.Died??-1;
        bool Current()=>manager!=null&&!manager.IsServer&&manager.IsConnected&&RebirthRemoteResourceRepairAdoption.QueryScope(actor,original)&&actor.Died==deaths&&actor.entityId==playerId&&
            save==GameIO.GetSaveGameDir()&&ReferenceEquals(manager.connectionToServer?[0],native);
        wire.Enqueue(RequestKey(original),body,Current,()=>Ready(native),send);
    }
    internal static void QueuePlacedEndedRequest(int playerId,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,Action<int> send)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var actor=GameManager.Instance?.World?.GetPrimaryPlayer();var native=manager?.connectionToServer?[0];string save=GameIO.GetSaveGameDir();int deaths=actor?.Died??-1;
        bool Current()=>manager!=null&&!manager.IsServer&&manager.IsConnected&&RebirthRemoteResourcePlacedStationOriginal.IsEndedQueryScope(actor,original)&&
            actor.Died==deaths&&actor.entityId==playerId&&save==GameIO.GetSaveGameDir()&&ReferenceEquals(manager.connectionToServer?[0],native);
        wire.Enqueue(RequestKey(original),body,Current,()=>Ready(native),send);
    }
    internal static bool QueueObservedRequest(int playerId,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,Action<int> send,Action<bool> completed)
    {
        Install();var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var actor=GameManager.Instance?.World?.GetPrimaryPlayer();var native=manager?.connectionToServer?[0];string save=GameIO.GetSaveGameDir();int deaths=actor?.Died??-1;
        bool Current()=>manager!=null&&!manager.IsServer&&manager.IsConnected&&RebirthRemoteResourceRepairAdoption.QueryScope(actor,original)&&actor.Died==deaths&&actor.entityId==playerId&&save==GameIO.GetSaveGameDir()&&ReferenceEquals(manager.connectionToServer?[0],native);
        return wire.EnqueueObserved(RequestKey(original),body,Current,()=>Ready(native),send,completed);
    }
    internal static void QueueResult(int playerId,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,Action<int> send)
    {
        var current=GameManager.Instance?.World;var actor=current?.GetEntity(playerId) as EntityPlayer;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var client=manager?.Clients.ForEntityId(playerId);var native=client?.netConnection?[0];string save=GameIO.GetSaveGameDir();
        int deaths=actor?.Died??-1;
        bool Current()=>current?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,current)&&ReferenceEquals(current.GetEntity(playerId),actor)&&actor!=null&&!actor.IsDead()&&actor.Died==deaths&&deaths>=original.Deaths&&
            current.worldState.Guid==original.World&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&save==GameIO.GetSaveGameDir()&&
            ReferenceEquals(manager?.Clients.ForEntityId(playerId),client)&&ReferenceEquals(client?.netConnection?[0],native)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==original.Owner;
        wire.Enqueue(ResultKey(playerId,original),body,Current,()=>Ready(native),send);
    }
    internal static bool QueueObservedResult(int playerId,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,Action<int> send,Action<bool> completed)
    {
        Install();var current=GameManager.Instance?.World;var actor=current?.GetEntity(playerId) as EntityPlayer;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var client=manager?.Clients.ForEntityId(playerId);var native=client?.netConnection?[0];string save=GameIO.GetSaveGameDir();int deaths=actor?.Died??-1;
        bool Current()=>current?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,current)&&ReferenceEquals(current.GetEntity(playerId),actor)&&actor!=null&&!actor.IsDead()&&actor.Died==deaths&&deaths>=original.Deaths&&
            current.worldState.Guid==original.World&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&save==GameIO.GetSaveGameDir()&&ReferenceEquals(manager?.Clients.ForEntityId(playerId),client)&&ReferenceEquals(client?.netConnection?[0],native)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==original.Owner;
        return wire.EnqueueObserved(ResultKey(playerId,original),body,Current,()=>Ready(native),send,completed);
    }
    private static RebirthCamperTransferJournal AdmissionJournal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteRepairSourceAdmission"));
    private static RebirthCamperTransferJournal PriorJournal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteRepairPriorTerminals"));
    private static RebirthCamperTransferJournal AckJournal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteRepairAdmissionAcks"));
    private static bool HasAdmissionAck(RebirthRemoteResourceClientOriginal.Intent original)
    {
        var record=AckJournal().Read(original.Operation);if(record==null)return false;
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||!record.Before.SequenceEqual(record.After))throw new InvalidDataException();
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4096);
        if(body.Name!="repairAdmissionAck"||body.Attributes().Count()!=3||body.Nodes().Any()||(string)body.Attribute("original")!=original.Digest||
            ((string)body.Attribute("world"))?.Length!=64||!Guid.TryParseExact((string)body.Attribute("operation"),"N",out var operation)||operation==Guid.Empty)throw new InvalidDataException();
        return true;
    }
    private static bool SaveAdmissionAck(RebirthRemoteResourceClientOriginal.Intent original,string sourceWorld,Guid source)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(GameManager.Instance?.World?.GetPrimaryPlayer(),out var owner)||
            RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)!=source)return false;
        byte[] bytes=Encoding.UTF8.GetBytes(new XElement("repairAdmissionAck",new XAttribute("original",original.Digest),new XAttribute("world",sourceWorld),new XAttribute("operation",source.ToString("N"))).ToString(SaveOptions.DisableFormatting));
        var journal=AckJournal();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static void SendAdmissionAck(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,string priorProof)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string sourceWorld))return;
        var source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        var body=new XElement("repairResult",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
            new XAttribute("sourceWorld",sourceWorld),new XAttribute("sourceOperation",source.ToString("N")),new XAttribute("binding",""),new XAttribute("phase",0),new XAttribute("outcome",0),
            new XAttribute("permission",0),new XAttribute("rejecting",0),new XAttribute("ownerProof",""),new XAttribute("disposition",""),new XAttribute("priorProof",priorProof),new XAttribute("noUseProof",""),
            new XElement("paid",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0]))));
        NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,body.ToString(SaveOptions.DisableFormatting));
    }
    internal static bool TryGetAuthenticatedPriorRepairTerminal(ItemActionEntryRepair entry,string currentPaidInput,out Guid prior,out string proof)
    {
        prior=Guid.Empty;proof=null;
        try
        {
            var player=entry?.ItemController?.xui?.playerUI?.entityPlayer;
            foreach(var retained in pending.Values)
            {
                if(!ReferenceEquals(retained.Entry,entry)||!RebirthRemoteResourceClientOriginal.Current(player,retained.Original)||
                    retained.Original.Copy().Element("action").Element("nativeInput").Value!=currentPaidInput||
                    !RebirthNativeItemCodec.TryDecode(currentPaidInput,out var input)||!input.TryGetMetadata("RebirthRepairOriginalV1",out string old)||
                    !Guid.TryParseExact(old,"N",out var id)||id==Guid.Empty)continue;
                var record=PriorJournal().Read(retained.Original.Operation);
                if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=retained.Original.Operation||record.OwnerCreation!=retained.Original.Creation||!record.Before.SequenceEqual(record.After))return false;
                var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),1024*1024);
                string witness=(string)body.Attribute("proof");
                if(body.Name!="priorRepairTerminal"||body.Attributes().Count()!=3||body.Elements().Count()!=1||
                    (string)body.Attribute("original")!=retained.Original.Digest||(string)body.Attribute("prior")!=id.ToString("N")||
                    witness?.Length!=64||witness.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||body.Element("input")?.Value!=currentPaidInput)return false;
                prior=id;proof=witness;return true;
            }
            return false;
        }
        catch{prior=Guid.Empty;proof=null;return false;}
    }
    private static bool SavePrior(Pending pendingOriginal,string proof)
    {
        var original=pendingOriginal.Original;string input=original.Copy().Element("action").Element("nativeInput").Value;
        if(!RebirthNativeItemCodec.TryDecode(input,out var item))return false;
        if(!item.HasMetadata("RebirthRepairOriginalV1"))return proof=="";
        if(proof?.Length!=64||proof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||
            !item.TryGetMetadata("RebirthRepairOriginalV1",out string prior)||!Guid.TryParseExact(prior,"N",out var id)||id==Guid.Empty)return false;
        var body=new XElement("priorRepairTerminal",new XAttribute("original",original.Digest),new XAttribute("prior",id.ToString("N")),new XAttribute("proof",proof),new XElement("input",input));
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var journal=PriorJournal();var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static bool AdmitSource(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out string priorProof)
    {
        priorProof=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||original.Kind!=RemoteResourceClientOperation.ItemRepair||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            original.World!=actor.world.worldState.Guid||original.Deaths>actor.Died||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.CanonicalId!=original.Owner||
            !RemoteResourceWorldIdentity.TryGet(actor.world,out string sourceWorld))return false;
        Guid operation=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        if(RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(operation))return false;
        var journal=AdmissionJournal();var retained=journal.Read(operation);
        if(retained!=null)
        {
            if(retained.Vehicle!=original.Operation||retained.OwnerCreation!=original.Creation||!retained.Before.SequenceEqual(retained.After))return false;
            var admission=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(retained.Before),17*1024*1024);
            if(admission.Name!="repairSourceAdmission"||admission.Attributes().Count()!=2||admission.Elements().Count()!=1||
                (string)admission.Attribute("originalDigest")!=original.Digest||!XNode.DeepEquals(admission.Element("fetchClientOriginal"),original.Copy()))return false;
            priorProof=(string)admission.Attribute("priorProof");if(priorProof==null||priorProof.Length!=0&&(priorProof.Length!=64||priorProof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))))return false;
            if(retained.State==RebirthCamperTransferJournal.Phase.Prepared)retained=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(retained.State==RebirthCamperTransferJournal.Phase.Uncertain)retained=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            return retained.State==RebirthCamperTransferJournal.Phase.Applied;
        }
        if(actor.Died!=original.Deaths)return false; // Later life may only query an existing saved native10 admission.
        var root=original.Copy();var action=root.Element("action");int kind=(int)action.Attribute("sourceKind"),slot=(int)action.Attribute("sourceSlot");
        if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var observed,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
            !RebirthRemoteResourceClientOriginal.ReadActor(root.Element("actorBefore").Value).Matches(observed)||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f)return false;
        var slots=kind==1?observed.Bag:kind==2?observed.Belt:RebirthRepairOriginalCorrection.ReadSavedEquipment(saved);
        if(slots==null||slot<0||slot>=slots.Length||slots[slot]==null||slots[slot].count!=1||RebirthNativeItemCodec.Encode(slots[slot].itemValue)!=action.Element("nativeInput").Value)return false;
        var input=slots[slot].itemValue;var tools=input.ItemClass.RepairTools;var kit=tools==null||tools.Length==0?null:ItemClass.GetItemClass(tools[0].Value);
        var full=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("requirements").Value));
        var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("remoteRequirements").Value));
        if(kit==null||kit.RepairAmount.Value<=0||input.UseTimes<=0||full.Length!=1||remote.Length!=1||full[0].itemValue.type!=kit.Id||remote[0].itemValue.type!=kit.Id||
            full[0].count>Math.Ceiling((float)Mathf.CeilToInt(input.UseTimes)/kit.RepairAmount.Value)||
            !RebirthRepairOriginalCorrection.SupportsOriginalMetadata(input,identity.CanonicalId,original.Creation,original.World))return false;
        long local=observed.Bag.Concat(observed.Belt).Where(item=>item!=null&&!item.IsEmpty()&&item.itemValue.type==kit.Id).Sum(item=>(long)item.count);
        if(remote[0].count!=Math.Max(0L,(long)full[0].count-local)||!RebirthRepairOriginalCorrection.TryReadPriorRepairTerminal(actor,action.Element("nativeInput").Value,out priorProof))return false;
        byte[] bytes=Encoding.UTF8.GetBytes(new XElement("repairSourceAdmission",new XAttribute("originalDigest",original.Digest),new XAttribute("priorProof",priorProof),original.Copy()).ToString(SaveOptions.DisableFormatting));
        var record=journal.Prepare(operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static bool UnpreparedRejection(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,bool observe)
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string worldId))return false;
        Guid operation=RebirthRemoteResourceDurableJournal.Operation(worldId,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&
            actor.Died==original.Deaths&&actor.world.worldState.Guid==original.World&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation)&&
            RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var current,out _)&&current?.Phase==2&&current.Copy().Element("ownerAction")==null;
        if(!Current()||!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var retained,out _)||retained?.Phase!=2)return false;
        var root=retained.Copy();if(root.Element("ownerAction")!=null||(int)root.Attribute("outcome")!=1)return false;
        if(((int?)root.Attribute("ownerRejecting")??0)!=1)
        {
            if(observe||!RebirthRemoteResourceDurableJournal.TryAdvance(retained,value=>value.SetAttributeValue("ownerRejecting",1),Current,out _))return false;
            return true;
        }
        if(!observe)return true;
        var intent=original.Copy();var source=intent.Element("action");int kind=(int)source.Attribute("sourceKind"),slot=(int)source.Attribute("sourceSlot");
        if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var observed,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
            !RebirthRemoteResourceClientOriginal.ReadActor(intent.Element("actorBefore").Value).Matches(observed)||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=5f)return false;
        var slots=kind==1?observed.Bag:kind==2?observed.Belt:RebirthRepairOriginalCorrection.ReadSavedEquipment(saved);
        if(slots==null||slot>=slots.Length||slots[slot]?.count!=1||RebirthNativeItemCodec.Encode(slots[slot].itemValue)!=source.Element("nativeInput").Value)return false;
        string proof=RebirthRemoteResourceDurableJournal.Digest("repair-ungranted|"+original.Digest+"|"+retained.Binding+"|"+intent.Element("actorBefore").Value+"|"+kind+":"+slot+"|"+source.Element("nativeInput").Value);
        if(root.Attribute("ownerNoUseProof")!=null)return (string)root.Attribute("ownerNoUseProof")==proof;
        return RebirthRemoteResourceDurableJournal.TryAdvance(retained,value=>value.SetAttributeValue("ownerNoUseProof",proof),Current,out var published)&&
            (string)published?.Copy().Attribute("ownerNoUseProof")==proof;
    }
    internal static void Request(EntityPlayer actor,string text)
    {
        try
        {
            if(!Supported(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId)?.netConnection?[0]))return;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            if(body.Name!="repairRequest"||body.Attributes().Count()!=2||(int?)body.Attribute("version")!=1||body.Elements().Count()!=2||body.Element("ownerActionBody")==null)return;
            int stage=(int)body.Attribute("stage");if(stage<0||stage>10)return;
            RebirthRemoteResourceClientOriginal.Intent original;
            if(body.Element("fetchClientOriginal")!=null)
            {if(!RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out original))return;}
            else
            {
                var reference=body.Element("originalRef");
                if(reference==null||reference.Attributes().Count()!=4||reference.Nodes().Any()||!Guid.TryParseExact((string)reference.Attribute("operation"),"N",out var ownerOperation)||
                    !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string currentWorld))return;
                string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
                Guid operation=RebirthRemoteResourceDurableJournal.Operation(currentWorld,identity.StorageKey,creation,(ulong)reference.Attribute("epoch"),(ulong)reference.Attribute("request"),RemoteResourceClientOperation.ItemRepair);
                var record=AdmissionJournal().Read(operation);if(record==null||!record.Before.SequenceEqual(record.After))return;
                var admission=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),17*1024*1024);
                if(!RebirthRemoteResourceClientOriginal.TryDecodeOriginal(admission.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out original)||
                    original.Operation!=ownerOperation||original.Digest!=(string)reference.Attribute("digest"))return;
            }
            if(!AdmitSource(actor,original,out string priorProof))return;
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value)).ToList();
            if(actor.Died>original.Deaths&&stage!=6&&stage!=7&&stage!=10)return;
            if(stage==5)
            {
                if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var sender)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string sourceWorld))return;
                var source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,sender.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
                if(!RebirthRemoteResourceDurableJournal.TryRead(sender.StorageKey,source,out var refusal,out _)||refusal==null||
                    !RebirthRemoteResourceDurableOutcome.AcknowledgeNoPayment(actor,source,refusal.Binding))return;
            }
            bool permission=false;RebirthRemoteResourceDurableJournal.Record result;
            if(stage==10)
            {
                if(!RebirthRemoteResourceRepairAdoption.Begin(actor,original,out result)||result==null)return;
            }
            else if(stage>=8)
            {
                UnpreparedRejection(actor,original,stage==9);
                if(actor.Died>original.Deaths)
                {if(!RebirthRemoteResourceRepairAdoption.Begin(actor,original,out result)||result==null)return;}
                else if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,true,out result)||result==null)return;
            }
            else if(stage>=6)
            {
                var refund=RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("ownerActionBody")?.Value,24*1024*1024);
                RebirthRemoteResourceRepairRefund.Host(actor,original,refund,stage==7,out permission);
                if(actor.Died>original.Deaths)
                {if(!RebirthRemoteResourceRepairAdoption.Begin(actor,original,out result)||result==null)return;}
                else if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,true,out result)||result==null)return;
            }
            else if(!RebirthRemoteResourceOwnerAction.ResolveRepairOriginal(actor,original.Epoch,original.Request,stage==5?0:stage,remote,body.Element("ownerActionBody")?.Value,out result,out permission)||result==null)
            {SendAdmissionAck(actor,original,priorProof);return;}
            var root=result.Copy();
            var response=new XElement("repairResult",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),
                new XAttribute("sourceWorld",result.World),new XAttribute("sourceOperation",result.Operation.ToString("N")),new XAttribute("binding",result.Binding),
                new XAttribute("phase",result.Phase),new XAttribute("outcome",(int)root.Attribute("outcome")),new XAttribute("permission",permission?1:0),
                new XAttribute("rejecting",(int?)root.Attribute("ownerRejecting")??0),new XAttribute("ownerProof",(string)root.Attribute("ownerProof")??""),
                new XAttribute("disposition",(string)root.Attribute("disposition")??""),new XAttribute("priorProof",priorProof),new XAttribute("noUseProof",(string)root.Attribute("ownerNoUseProof")??""),new XElement("paid",root.Element("removed").Value));
            NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,response.ToString(SaveOptions.DisableFormatting));
        }
        catch{/* Unknown original remains owed; malformed requests grant nothing. */}
    }
    internal static void Result(EntityPlayerLocal player,Guid operation,string text)
    {
        try
        {
            if(!pending.TryGetValue(operation,out var entry)||!RebirthRemoteResourceRepairAdoption.QueryScope(player,entry.Original))return;
            var original=entry.Original;var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);
            if(body.Name!="repairResult"||body.Attributes().Count()!=14||body.Elements().Count()!=1||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("original")!=operation.ToString("N")||(string)body.Attribute("originalDigest")!=original.Digest||
                !Guid.TryParseExact((string)body.Attribute("sourceOperation"),"N",out var source))return;
            string sourceWorld=(string)body.Attribute("sourceWorld"),binding=(string)body.Attribute("binding"),proof=(string)body.Attribute("ownerProof");
            int phase=(int)body.Attribute("phase"),outcome=(int)body.Attribute("outcome");
            var paid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("paid").Value));
            bool Auth()=>RebirthRemoteResourceRepairAdoption.QueryScope(player,original)&&pending.TryGetValue(operation,out var same)&&ReferenceEquals(same,entry);
            if(phase==0&&outcome==0){SaveAdmissionAck(original,sourceWorld,source);return;}
            if(!SaveAdmissionAck(original,sourceWorld,source))return;
            if(phase==3&&outcome==2){Send(player,original,5,"");return;}
            if(phase==4&&outcome==2&&(string)body.Attribute("disposition")=="unused-no-payment")
            {if(RebirthRemoteResourceClientOriginal.SaveNoPaymentTerminal(player,original,sourceWorld,source,binding,proof,Auth))pending.Remove(operation);return;}
            if(phase==4&&outcome==1&&(string)body.Attribute("disposition")=="used")
            {if(RebirthRemoteResourceClientOriginal.SaveRepairTerminal(player,original,source,binding,proof,Auth))pending.Remove(operation);return;}
            if(phase==4&&outcome==1&&(string)body.Attribute("disposition")=="refunded")
            {
                if(!RebirthRemoteResourceRepairRefund.Complete(player,original,source,binding,proof,Auth))return;
                string receipt=RebirthRemoteResourceOwnerAction.Receipt(RebirthRemoteResourceRepairRefund.Child(original));
                player.Buffs.SetCustomVar(receipt,0f,true);player.Buffs.RemoveCustomVar(receipt);
                GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);pending.Remove(operation);return;
            }
            if(phase!=2||outcome!=1||!SavePrior(entry,(string)body.Attribute("priorProof")))return;
            string noUse=(string)body.Attribute("noUseProof");
            if(!string.IsNullOrEmpty(noUse))
            {
                if(player.Died>original.Deaths&&!RebirthRemoteResourceClientOriginal.SaveRepairSource(player,original,sourceWorld,source,binding,paid,Auth))return;
                if(!RebirthRemoteResourceRepairRefund.AcceptNoUse(player,original,source,binding,noUse,Auth))return;
                entry.NoUseProof=noUse;entry.SourceOperation=source;entry.SourceBinding=binding;entry.Paid=ItemStack.Clone(paid);
                if(entry.Refund!=null&&(int)body.Attribute("permission")==1&&RebirthRemoteResourceRepairRefund.Apply(player,original,entry.Refund,Auth))
                {entry.RefundApplied=true;Send(player,original,7,entry.Refund.ToString(SaveOptions.DisableFormatting));}
                return;
            }
            if(!RebirthRemoteResourceClientOriginal.TryReadRepairAction(original,out var action))
            {
                if((int)body.Attribute("rejecting")==1)
                {
                    if(RebirthRemoteResourceClientOriginal.SaveRepairSource(player,original,sourceWorld,source,binding,paid,Auth)&&
                        RebirthRemoteResourceClientOriginal.SaveUnpreparedRepairOwnerFive(player,original,Auth))Send(player,original,9,"");
                    return;
                }
                if(entry.Entry!=null&&RemoteResourceClientTransactionCoordinator.PrepareOriginalRepair(entry.Entry,original,sourceWorld,source,binding,paid,Auth,out _))return;
                if(RebirthRemoteResourceClientOriginal.SaveRepairSource(player,original,sourceWorld,source,binding,paid,Auth)&&
                    RebirthRemoteResourceClientOriginal.SaveUnpreparedRepairRejection(player,original,Auth))Send(player,original,8,"");
                return;
            }
            if(!RebirthRemoteResourceOwnerAction.TryDecodeRepairPaymentIntent(action.Element("payment").Value,out var payment)||payment.SourceOperation!=source||payment.Binding!=binding||payment.SourceWorld!=sourceWorld)return;
            if(RebirthRemoteResourceOwnerAction.TryHoldRestoredRepairUsage(player,original,Auth)&&RebirthRemoteResourceClientOriginal.TrySaveRestoredRepairOwnerUsage(player,original))
            {entry.Used=true;Send(player,original,2,action.ToString(SaveOptions.DisableFormatting));return;}
            if((int)body.Attribute("rejecting")==1)
            {
                entry.Rejecting=true;
                if(RebirthRemoteResourceOwnerAction.TryHold(player,original,source,binding,
                    RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value),RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value),Auth)&&
                    RebirthRemoteResourceClientOriginal.SaveRepairRejection(player,original,Auth)&&
                    RebirthRemoteResourceClientOriginal.TrySaveRejectedRepairOwner(player,original,entry.Entry,Auth))Send(player,original,4,action.ToString(SaveOptions.DisableFormatting));
                return;
            }
            if((int)body.Attribute("permission")==1&&entry.Entry!=null&&
                RebirthRemoteResourceOwnerAction.TryHold(player,original,payment.SourceOperation,payment.Binding,
                    RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value),RebirthRemoteResourceClientOriginal.ReadActor(action.Element("afterActor").Value),Auth)&&
                RemoteResourceClientTransactionCoordinator.ResumeOriginalRepair(entry.Entry,original,paid,Auth))
            {entry.Used=true;Send(player,original,2,action.ToString(SaveOptions.DisableFormatting));}
            else if((int)body.Attribute("permission")==1&&
                RebirthRemoteResourceClientOriginal.ReadActor(action.Element("beforeActor").Value).Matches(RebirthCamperEnergyActorImage.Capture(player))&&
                RebirthRemoteResourceClientOriginal.SaveRepairRejection(player,original,Auth))
            {entry.Rejecting=true;Send(player,original,3,action.ToString(SaveOptions.DisableFormatting));}
        }
        catch{/* No fallback to the volatile result handler or inferred refund. */}
    }
}

[Preserve]
public sealed class NetPackageRebirthRemoteRepairOriginal:NetPackage
{
    private const uint Version=0x31515252;private int playerId,index,count,total;private PlatformUserIdentifierAbs user;
    private Guid operation;private string nativeWorld,creation,hash,fragment;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal static void SendPlacedEnded(int player,PlatformUserIdentifierAbs owner,RebirthRemoteResourceClientOriginal.Intent original)
    {
        // This purpose-specific producer authors only the fixed stage4
        // metadata grammar; it cannot broaden normal financial QueueRequest.
        string text=new XElement("placedStationRequest",new XAttribute("version",1),new XAttribute("stage",4),original.Copy()).ToString(SaveOptions.DisableFormatting);
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(text));
        RebirthRemoteResourceRepairProtocol.QueuePlacedEndedRequest(player,original,body,part=>SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthRemoteRepairOriginal>().Setup(player,owner,original,body,part)));
    }
    internal static void Send(int player,PlatformUserIdentifierAbs owner,RebirthRemoteResourceClientOriginal.Intent original,string text)
    {
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(text));
        RebirthRemoteResourceRepairProtocol.QueueRequest(player,original,body,part=>SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthRemoteRepairOriginal>().Setup(player,owner,original,body,part)));
    }
    internal static bool SendObserved(int player,PlatformUserIdentifierAbs owner,RebirthRemoteResourceClientOriginal.Intent original,string text,Action<bool> completed)
    {
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(text));
        return RebirthRemoteResourceRepairProtocol.QueueObservedRequest(player,original,body,part=>SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthRemoteRepairOriginal>().Setup(player,owner,original,body,part)),completed);
    }
    private NetPackageRebirthRemoteRepairOriginal Setup(int player,PlatformUserIdentifierAbs owner,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,int part)
    {RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);playerId=player;user=owner;operation=original.Operation;nativeWorld=original.World;creation=original.Creation;hash=body.Hash;total=body.Length;count=body.Count;index=part;fragment=body.Part(part);return this;}
    public override void read(PooledBinaryReader reader)
    {
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        if(reader.ReadUInt32()!=Version)throw new InvalidDataException();playerId=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);
        byte[] bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);
        nativeWorld=RebirthCookingGridCustody.ReadWireString(reader,64);creation=RebirthCookingGridCustody.ReadWireString(reader,512);hash=RebirthCookingGridCustody.ReadWireString(reader,64);
        total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();fragment=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write(Version);writer.Write(playerId);user.ToStream(writer);writer.Write(operation.ToByteArray());
        RebirthCookingGridCustody.WriteWireString(writer,nativeWorld,64);RebirthCookingGridCustody.WriteWireString(writer,creation,512);RebirthCookingGridCustody.WriteWireString(writer,hash,64);
        writer.Write(total);writer.Write(count);writer.Write(index);RebirthCookingGridCustody.WriteWireString(writer,fragment,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!RebirthRemoteResourceNetworkScope.Receive(this,world,callbacks,true,out var connection)||world.worldState.Guid!=nativeWorld||operation==Guid.Empty||
            Sender==null||!ValidEntityIdForSender(playerId)||!ValidUserIdForSender(user)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(playerId),Sender))return;
        var actor=world.GetEntity(playerId) as EntityPlayer;
        if(actor==null||actor.IsDead()||creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return;
        if(total>24*1024*1024||!RebirthRemoteResourceFragments.TryAdd("repair-request|"+nativeWorld+"|"+owner.StorageKey+"|"+creation+"|"+RebirthRemoteResourceNetworkScope.SessionKey(connection),operation,hash,total,count,index,fragment,Time.realtimeSinceStartup,out var complete)||complete==null)return;
        try
        {
            string text=new UTF8Encoding(false,true).GetString(complete);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            var original=body.Element("fetchClientOriginal")??body.Element("originalRef");if((string)original?.Attribute("operation")!=operation.ToString("N"))return;
            if(body.Name=="preAdmissionRefusalRequest")RebirthRemoteResourcePreAdmissionRefusal.Request(actor,text);
            else if(body.Name=="craftRequest")RebirthRemoteResourceCraftProtocol.Request(actor,text);
            else if(body.Name=="fetchedStationRequest")RebirthRemoteResourceFetchedStationOriginal.Request(actor,text);
            else if(body.Name=="placedStationRequest")RebirthRemoteResourcePlacedStationOriginal.Request(actor,text);
            else if(body.Name=="placedStationCancelRequest")RebirthRemoteResourcePlacedStationOriginal.NativeCancellationRequest(this,world,actor,text);
            else if(body.Name=="blockRequest")RebirthRemoteResourceBlockProtocol.Request(actor,text);
            else if(body.Name=="repairRequest")RebirthRemoteResourceRepairProtocol.Request(actor,text);
        }
        catch{}
    }
    public int GetLength()=>0;
}

[Preserve]
public sealed class NetPackageRebirthRemoteRepairResult:NetPackage
{
    private const uint Version=0x31535252;private Guid operation;private string nativeWorld,owner,creation,hash,fragment;private int total,count,index;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal static void Send(int player,RebirthRemoteResourceClientOriginal.Intent original,string text)
    {
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(text));
        RebirthRemoteResourceRepairProtocol.QueueResult(player,original,body,part=>SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthRemoteRepairResult>().Setup(original,body,part),_attachedToEntityId:player));
    }
    internal static bool SendObserved(int player,RebirthRemoteResourceClientOriginal.Intent original,string text,Action<bool> completed)
    {
        var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(text));
        return RebirthRemoteResourceRepairProtocol.QueueObservedResult(player,original,body,part=>SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthRemoteRepairResult>().Setup(original,body,part),_attachedToEntityId:player),completed);
    }
    private NetPackageRebirthRemoteRepairResult Setup(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceFragments.Body body,int part)
    {RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);operation=original.Operation;nativeWorld=original.World;owner=original.Owner;creation=original.Creation;hash=body.Hash;total=body.Length;count=body.Count;index=part;fragment=body.Part(part);return this;}
    public override void read(PooledBinaryReader reader)
    {
        RebirthProductiveEarnedUiDeferredTransport.ForgetReceivedConnection(this);
        if(reader.ReadUInt32()!=Version)throw new InvalidDataException();byte[] bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);
        nativeWorld=RebirthCookingGridCustody.ReadWireString(reader,64);owner=RebirthCookingGridCustody.ReadWireString(reader,512);creation=RebirthCookingGridCustody.ReadWireString(reader,512);hash=RebirthCookingGridCustody.ReadWireString(reader,64);
        total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();fragment=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write(Version);writer.Write(operation.ToByteArray());RebirthCookingGridCustody.WriteWireString(writer,nativeWorld,64);RebirthCookingGridCustody.WriteWireString(writer,owner,512);RebirthCookingGridCustody.WriteWireString(writer,creation,512);RebirthCookingGridCustody.WriteWireString(writer,hash,64);
        writer.Write(total);writer.Write(count);writer.Write(index);RebirthCookingGridCustody.WriteWireString(writer,fragment,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!RebirthRemoteResourceNetworkScope.Receive(this,world,callbacks,false,out var connection)||world.worldState.Guid!=nativeWorld||operation==Guid.Empty)return;
        var actor=world.GetPrimaryPlayer();if(actor==null||actor.IsDead()||creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.CanonicalId!=owner)return;
        if(total>12*1024*1024||!RebirthRemoteResourceFragments.TryAdd("repair-result|"+nativeWorld+"|"+identity.StorageKey+"|"+creation+"|"+RebirthRemoteResourceNetworkScope.SessionKey(connection),operation,hash,total,count,index,fragment,Time.realtimeSinceStartup,out var complete)||complete==null)return;
        try
        {
            string text=new UTF8Encoding(false,true).GetString(complete);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,12*1024*1024);
            if(body.Name=="preAdmissionRefusalResult")RebirthRemoteResourcePreAdmissionRefusal.Result(actor,operation,text);
            else if(body.Name=="craftResult")RebirthRemoteResourceCraftProtocol.Result(actor,operation,text);
            else if(body.Name=="fetchedStationResult")RebirthRemoteResourceFetchedStationOriginal.Result(actor,operation,text);
            else if(body.Name=="placedStationResult"||body.Name=="placedStationProgress"||body.Name=="placedStationNoPayment"||body.Name=="placedStationEndedNoPayment")RebirthRemoteResourcePlacedStationOriginal.Result(actor,operation,text);
            else if(body.Name=="placedStationCancelResult")RebirthRemoteResourcePlacedStationOriginal.NativeCancellationResult(this,world,actor,operation,text,body);
            else if(body.Name=="fetchedStationEndedZero")RebirthRemoteResourceFetchedStationOriginal.EndedZeroResult(actor,operation,text);
            else if(body.Name=="fetchedStationNativeFrame")RebirthRemoteResourceFetchedStationOriginal.NativeFrameResult(actor,operation,text);
            else if(body.Name=="blockResult")RebirthRemoteResourceBlockProtocol.Result(actor,operation,text);
            else if(body.Name=="repairResult")RebirthRemoteResourceRepairProtocol.Result(actor,operation,text);
        }
        catch{}
    }
    public int GetLength()=>0;
}
