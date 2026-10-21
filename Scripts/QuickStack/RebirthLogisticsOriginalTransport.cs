using Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Xml;
using System.Xml.Linq;

// Transport admission accepts intent identifiers only. Native owner/source
// images are read by OriginalAdmission and never accepted from this request.
internal static partial class RebirthLogisticsOriginalTransport
{
    internal enum Mode:byte { Prepare=0,Query=1,Cancel=2 }
    internal static bool IsPurposePackage(NetPackage packet)=>packet is NetPackageRebirthLogisticsOriginalRequest||
        packet is NetPackageRebirthLogisticsOriginalResult||packet is NetPackageRebirthLogisticsPhysicalEvidence||
        packet is NetPackageRebirthLogisticsPhysicalAck||packet is NetPackageRebirthLogisticsTerminalPhysical||
        packet is NetPackageRebirthLogisticsHeaderRequest||packet is NetPackageRebirthLogisticsHeaderAck||
        packet is NetPackageRebirthLogisticsNativeBodyIntent||packet is NetPackageRebirthLogisticsNativeBodyAck||packet is NetPackageRebirthLogisticsNativeBodyQuery||
        packet is NetPackageRebirthLogisticsHistoricalZero||packet is NetPackageRebirthLogisticsHistoricalZeroAck||
        packet is NetPackageRebirthLogisticsBodyReferenceNotice||packet is NetPackageRebirthLogisticsBodyReferenceAck;
    internal static bool TryCurrentPeer(EntityPlayer actor,out ClientInfo peer,out INetConnection connection)
    {
        peer=null;connection=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||manager?.IsServer!=true||world?.IsRemote()!=false||actor==null||
            !ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor))return false;
        peer=manager.Clients?.ForEntityId(actor.entityId);
        if(peer==null||!peer.loginDone||peer.disconnecting||peer.netConnection==null||peer.netConnection.Length==0)return false;
        connection=peer.netConnection[0];return connection!=null&&!connection.IsDisconnected();
    }
    internal static bool AcceptPacket(NetPackage packet,World world,GameManager callbacks,bool toServer)
    {
        if(!ThreadManager.IsMainThread()||packet==null||packet.Channel!=0||world==null||
            !ReferenceEquals(world,GameManager.Instance?.World)||!ReferenceEquals(callbacks,GameManager.Instance)||world.IsRemote()==toServer||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||received.IsDisconnected())return false;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(toServer)return manager?.IsServer==true&&packet.Sender!=null&&
            ReferenceEquals(manager.Clients?.ForEntityId(packet.Sender.entityId),packet.Sender)&&packet.Sender.loginDone&&!packet.Sender.disconnecting&&
            packet.Sender.netConnection?.Length>0&&ReferenceEquals(packet.Sender.netConnection[0],received);
        return manager?.IsServer==false&&manager.IsConnected&&manager.connectionToServer?.Length>0&&
            ReferenceEquals(manager.connectionToServer[0],received);
    }
    internal static bool TryOwnerConnection(out ConnectionManager manager,out INetConnection connection)
    {
        manager=SingletonMonoBehaviour<ConnectionManager>.Instance;connection=null;
        if(!ThreadManager.IsMainThread()||manager?.IsServer!=false||!manager.IsConnected||manager.connectionToServer==null||manager.connectionToServer.Length==0)return false;
        connection=manager.connectionToServer[0];return connection!=null&&!connection.IsDisconnected();
    }
    private sealed class Job:IDisposable
    {
        internal EntityPlayer Actor;internal Guid Operation;internal ulong Epoch;internal int Request;
        internal QuickStackRadialAction Action;internal string Target,Selection,Owner,Directory;
        internal World World;internal RebirthLogisticsOriginalAdmission Admission;
        internal ConnectionManager Manager;internal ClientInfo Peer;internal INetConnection Connection;
        internal RebirthLogisticsServerOperation Running;internal bool Cancel;internal DateTime NextResult;
        public void Dispose(){Admission?.Dispose();Running?.Dispose();}
    }
    private static readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>(StringComparer.Ordinal);
    private static readonly RebirthRemoteResourceRepairSendQueue resultWire=new RebirthRemoteResourceRepairSendQueue();
    private static World resultWorld;
    private sealed class PhysicalTerminalEvidence
    {internal Guid Operation;internal string World,Binding,Result,Proof;internal int Deaths;}
    private static PhysicalTerminalEvidence physicalTerminal;
    private sealed class PhysicalSend
    {internal RebirthRemoteResourceFragments.Body Body;internal Guid Operation;internal int Stage;internal string Binding,CommandBinding;internal Func<bool> Current,Ready;internal Action<int> Send;internal DateTime Next;}
    private static readonly Dictionary<string,PhysicalSend> physicalSends=new Dictionary<string,PhysicalSend>(StringComparer.Ordinal);
    private static int physicalSendBytes;
    private static void PhysicalSendTick()
    {
        foreach(var pair in physicalSends.ToArray())
        {
            var value=pair.Value;
            if(!value.Current()){physicalSendBytes-=value.Body.Length;physicalSends.Remove(pair.Key);continue;}
            if(DateTime.UtcNow<value.Next||resultWire.IsQueued(pair.Key))continue;
            if(resultWire.Enqueue(pair.Key,value.Body,value.Current,value.Ready,value.Send))value.Next=DateTime.UtcNow.AddSeconds(2);
        }
    }
    // Activation is deliberately separate from class/package discovery. The
    // installer must supply authenticated result delivery before any producer
    // switches away from its existing native local path.
    private static Action<EntityPlayer,RebirthLogisticsTransferJournal.Record> deliver;
    internal static bool Active=>deliver!=null;
    private static void Activate()
    {
        if(!ThreadManager.IsMainThread()||deliver!=null)throw new InvalidOperationException();
        deliver=SendResult;
    }
    internal static void Receive(EntityPlayer actor,Guid operation,ulong epoch,int request,QuickStackRadialAction action,string target,string selection,Mode mode)
    {
        if(!Active||!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||operation==Guid.Empty||epoch==0||request<=0||
            (byte)action>(byte)QuickStackRadialAction.PullDroneCredits||(byte)mode>2||target==null||target.Length>512||selection==null||selection.Length>8192||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!TryCurrentPeer(actor,out var peer,out var connection))return;
        string key=identity.StorageKey+":"+operation.ToString("N");
        RebirthLogisticsHistoricalBeforeCapsule.TryAdoptPending(actor,operation);
        RebirthLogisticsPreAdmissionReceipt.Recover(actor,operation);
        if(RebirthLogisticsTransferJournal.TryRead(identity.StorageKey,operation,out var original))
        {
            if(mode==Mode.Prepare&&(original.Epoch!=epoch||original.Request!=request||original.Action!=action||original.Target!=target||original.Selection!=selection))return;
            epoch=original.Epoch;request=original.Request;action=original.Action;target=original.Target;selection=original.Selection;
            // A cold authenticated query may reobserve the SAME actual final
            // native primary. Save callbacks are liveness, not substitute proof.
            var nativeSender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);
            if(original.Phase<4&&nativeSender!=null&&RebirthStablePlayerIdentity.TryFromClientInfo(nativeSender,out var nativeOwner)&&nativeOwner.StorageKey==original.Owner&&
                RebirthLogisticsOwnerReceipt.TrySavedOperation(nativeOwner,out var nativeOperation,out _,out float nativeHeld,out _)&&nativeOperation==operation&&nativeHeld==1f)
                RebirthLogisticsNativeDeathReceipt.ObserveSaved(nativeSender);
            if(original.Phase==4){deliver(actor,original);return;}
        }
        else if(mode==Mode.Query)return; // A query never creates a replacement operation; Cancel may freeze only this unpaid original.
        if(jobs.TryGetValue(key,out var existing))
        {
            if(!ReferenceEquals(existing.Actor,actor)||existing.Epoch!=epoch||existing.Request!=request||existing.Action!=action||existing.Target!=target||existing.Selection!=selection)return;
            if(mode==Mode.Cancel)existing.Cancel=true;
            return;
        }
        // One retained live operation per owner. Different original requests
        // cannot evict an uncertain job or steal its native endpoint holds.
        if(jobs.Count>=64||jobs.Values.Any(value=>value.Owner==identity.StorageKey))return;
        var job=new Job{Actor=actor,World=actor.world,Owner=identity.StorageKey,Operation=operation,Epoch=epoch,Request=request,Action=action,
            Target=target,Selection=selection,Directory=GameIO.GetSaveGameDir(),Cancel=mode==Mode.Cancel,
            Manager=SingletonMonoBehaviour<ConnectionManager>.Instance,Peer=peer,Connection=connection};
        if(original!=null)
        {if(!RebirthLogisticsServerOperation.TryBind(actor,original,out job.Running))return;}
        else
        {job.Admission=RebirthLogisticsOriginalAdmission.Begin(actor,operation,epoch,request,action,target,selection,mode==Mode.Cancel);if(job.Admission==null)return;}
        jobs.Add(key,job);
    }
    internal static void Tick()
    {
        if(!Active||!ThreadManager.IsMainThread())return;
        foreach(var pair in jobs.ToArray())
        {
            var job=pair.Value;
            if(!ReferenceEquals(GameManager.Instance?.World,job.World)||GameIO.GetSaveGameDir()!=job.Directory||
                !ReferenceEquals(job.World.GetEntity(job.Actor.entityId),job.Actor)||job.Actor.IsDead()||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,job.Manager)||
                !TryCurrentPeer(job.Actor,out var peer,out var connection)||!ReferenceEquals(peer,job.Peer)||!ReferenceEquals(connection,job.Connection))
            {job.Dispose();jobs.Remove(pair.Key);continue;} // Durable original and unknown native holds remain retained.
            try
            {
                RebirthLogisticsTransferJournal.Record original=null;
                if(job.Running==null)
                {
                    if(job.Admission==null)
                    {
                        job.Admission=RebirthLogisticsOriginalAdmission.Begin(job.Actor,job.Operation,job.Epoch,job.Request,job.Action,job.Target,job.Selection,job.Cancel);
                        if(job.Admission==null)continue;
                    }
                    if(job.Cancel)job.Admission.RequestCancel();
                    if(!job.Admission.Step(out original)||original==null)
                    {
                        // A pure discovery watch can expire while a native
                        // endpoint is unloaded or publication is pressured.
                        // Renew only that transient watch under this SAME job
                        // and request, retaining an already received Cancel.
                        // Begin re-reads any original published in the meantime.
                        if(job.Admission.Expired){job.Admission.Dispose();job.Admission=null;}
                        continue;
                    }
                    if(!RebirthLogisticsServerOperation.TryBind(job.Actor,original,out job.Running))continue;
                    job.Admission.Dispose();job.Admission=null;
                }
                if(job.Cancel)job.Running.RequestAbort();
                bool terminal=job.Running.Step(out original);
                if(original!=null&&(terminal||DateTime.UtcNow>=job.NextResult)){deliver(job.Actor,original);job.NextResult=DateTime.UtcNow.AddSeconds(2);}
                if(terminal){job.Dispose();jobs.Remove(pair.Key);}
            }
            catch(Exception error){Log.Warning("[QuickStack] Original transport retained: "+error.Message);}
        }
    }
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();
        RebirthLogisticsOwnerReceipt.Install();RebirthLogisticsEndpointIdentity.Install();RebirthLogisticsCompositePublication.Install();
        RebirthLogisticsVehiclePublication.Install();RebirthLogisticsDronePublication.Install();RebirthRemoteResourceSourceCustody.Install();
        RebirthLogisticsDeathCustody.Install();
        RebirthLogisticsDeathSettlement.Install();
        // All dependencies must succeed before old remote producers are disabled.
        ModEvents.GameUpdate.RegisterHandler(Update);
        Activate();installed=true;
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        if(!ReferenceEquals(resultWorld,GameManager.Instance?.World))
        {
            resultWire.Clear();physicalSends.Clear();physicalSendBytes=0;resultWorld=GameManager.Instance?.World;
            if(!ReferenceEquals(clientWorld,resultWorld))ResetClient(resultWorld);RebirthLogisticsOwnerReceipt.ResetWorld(resultWorld);RebirthLogisticsHeaderAdmission.ResetWorld(resultWorld);
        }
        RebirthLogisticsHeaderAdmission.Tick(GameManager.Instance?.World?.GetPrimaryPlayer());RebirthLogisticsDeathSettlement.Tick();Tick();ClientTick();RebirthLogisticsNativeBodyIntent.Tick();RebirthLogisticsLoadedBodyNotice.Tick();PhysicalSendTick();resultWire.Pump(16);
    }
    internal const int ChunkChars=12000,ChunkBytes=9000,MaxFrameChars=4*1024*1024,MaxChunks=(MaxFrameChars+ChunkBytes-1)/ChunkBytes;
    internal static string Digest(string value)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",string.Empty).ToLowerInvariant();}
    private static void SendResult(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=GameManager.Instance?.World;
        if(actor==null||world?.IsRemote()!=false||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=original.Owner)return;
        string key="logistics-result|"+original.World+"|"+original.Owner+"|"+Digest(original.Creation)+"|"+original.Operation.ToString("N")+"|"+original.Epoch+"|"+original.Request;
        if(resultWire.IsQueued(key))return;
        string text=RebirthLogisticsTransferJournal.Encode(original);
        if(text.Length>MaxFrameChars||Encoding.UTF8.GetByteCount(text)>MaxFrameChars)throw new System.IO.InvalidDataException();
        var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(text));
        int entity=actor.entityId,deaths=actor.Died;string owner=original.Owner,creation=original.Creation,nativeWorld=original.World,directory=GameIO.GetSaveGameDir();Guid operation=original.Operation;
        if(!TryCurrentPeer(actor,out var client,out var connection))return;
        bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()==false&&ReferenceEquals(world.GetEntity(entity),actor)&&!actor.IsDead()&&actor.Died==deaths&&
            GameIO.GetSaveGameDir()==directory&&RebirthLogisticsTransferJournal.WorldKey(world)==nativeWorld&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&TryCurrentPeer(actor,out var livePeer,out var liveConnection)&&
            ReferenceEquals(livePeer,client)&&ReferenceEquals(liveConnection,connection)&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var currentOwner)&&currentOwner.StorageKey==owner;
        if(original.Phase==4&&actor.Died>original.Deaths&&Current()&&SendReady(connection)&&RebirthLogisticsNativeDeathReceipt.TryTerminalPhysicalProof(original,out var physicalProof))
            manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLogisticsTerminalPhysical>().Setup(operation,RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Digest(RebirthLogisticsTransferJournal.Encode(original)),physicalProof,original.Deaths),_attachedToEntityId:entity);
        // Rejection leaves the same durable original; query/normal result tick
        // retries. No fresh operation, terminal receipt or item effect is made.
        resultWire.Enqueue(key,body,Current,()=>SendReady(connection),index=>manager.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthLogisticsOriginalResult>().Setup(operation,body.Hash,index,body.Count,body.Part(index)),_attachedToEntityId:entity));
    }
    internal static void ReceivePhysicalTerminalEvidence(World world,Guid operation,string binding,string result,string proof,int deaths)
    {
        var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||player==null||deaths<0||deaths==int.MaxValue||player.Died<=deaths||
            !RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var native,out _)||native!=operation||new[]{binding,result,proof}.Any(x=>x?.Length!=64||!x.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')))return;
        var next=new PhysicalTerminalEvidence{Operation=operation,World=RebirthLogisticsTransferJournal.WorldKey(world),Binding=binding,Result=result,Proof=proof,Deaths=deaths};
        if(physicalTerminal!=null&&(physicalTerminal.Operation!=operation||physicalTerminal.Binding!=binding||physicalTerminal.Result!=result))return;
        physicalTerminal=next;ClientTick();
    }
    internal static bool HasPhysicalTerminalEvidence(EntityPlayerLocal player,RebirthLogisticsTransferJournal.Record original)
    {
        var evidence=physicalTerminal;
        return original?.Phase==4&&original.Outcome!=0&&evidence!=null&&evidence.Operation==original.Operation&&evidence.Deaths==original.Deaths&&player.Died>original.Deaths&&
            evidence.World==original.World&&evidence.Binding==RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)&&evidence.Result==Digest(RebirthLogisticsTransferJournal.Encode(original))&&evidence.Proof?.Length==64&&
            ReferenceEquals(GameManager.Instance?.World,player.world)&&RebirthLogisticsTransferJournal.WorldKey(player.world)==original.World&&RebirthCamperCharacterAccess.CurrentCreation(player)==original.Creation&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.StorageKey==original.Owner&&RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var native,out _)&&native==original.Operation;
    }
    // Native codec evidence only. Never a NetPackagePlayerData and never a
    // primary-file save: pre-clear snapshots cannot safely survive as native
    // player saves because ToPlayer does not replay the original death drop.
    internal static void SendPhysicalEvidence(EntityPlayerLocal player,int stage)
    {
        try
        {
            if(!Active||!ThreadManager.IsMainThread()||(stage<1||stage>4)&&stage!=6||player?.world?.IsRemote()!=true||!player.IsDead()||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out _)||!TryReadCommandScope(player,operation,out var original)||
                player.Died!=original.Deaths+1)return;
            string sendKey="logistics-physical|"+original.World+"|"+original.Owner+"|"+operation.ToString("N")+"|"+stage;
            if(physicalSends.ContainsKey(sendKey)||physicalSends.Count>=5||physicalSendBytes>32*1024*1024-(6*1024*1024+32))return;
            var captured=new PlayerDataFile();captured.FromPlayer(player);
            var images=new[]{captured.bagData,captured.inventoryData,captured.equipmentData,captured.buffData};
            int[] limits={2*1024*1024,2*1024*1024,1048576,1048576};long total=32;
            for(int i=0;i<4;i++){if(images[i]==null||images[i].Length<1||images[i].Length>limits[i])return;total+=images[i].Length;}
            if(total>6*1024*1024+32)return;
            byte[] bytes;
            using(var stream=new MemoryStream((int)total))using(var writer=new BinaryWriter(stream))
            {
                writer.Write((byte)1);writer.Write(captured.id);writer.Write(captured.deaths);writer.Write(captured.bDead);
                foreach(var image in images){writer.Write((int)image.Length);writer.Write(image.ToArray());}writer.Flush();bytes=stream.ToArray();
            }
            var body=new RebirthRemoteResourceFragments.Body(bytes);var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var native=manager?.connectionToServer?[0];var world=player.world;string creation=original.Creation,owner=original.Owner,worldKey=original.World;
            int entity=player.entityId,deaths=player.Died;string save=GameIO.GetSaveGameDir();var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
            bool Current()=>physicalSends.TryGetValue(sendKey,out var retained)&&retained.Body.Hash==body.Hash&&ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&player.entityId==entity&&player.Died==deaths&&
                GameIO.GetSaveGameDir()==save&&RebirthLogisticsTransferJournal.WorldKey(world)==worldKey&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var id)&&id.StorageKey==owner&&ReferenceEquals(manager.connectionToServer?[0],native);
            if(physicalSendBytes>32*1024*1024-body.Length)return;
            physicalSends.Add(sendKey,new PhysicalSend{Body=body,Operation=operation,Stage=stage,CommandBinding=original.Binding,Binding=TryReadCurrentOriginal(player,operation,out var canonical)?RebirthLogisticsTransferJournal.ImmutablePlanDigest(canonical):null,Current=Current,Ready=()=>SendReady(native),Send=index=>manager.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthLogisticsPhysicalEvidence>().Setup(entity,persistent.PrimaryId,operation,stage,body.Hash,body.Length,index,body.Count,body.Part(index)),false)});
            physicalSendBytes+=body.Length;PhysicalSendTick();
        }
        catch(Exception error){Log.Warning("[QuickStack] Detached death evidence retained unknown: "+error.Message);}
    }
    internal static void ReceivePhysicalEvidence(EntityPlayer actor,Guid operation,int stage,string digest,int total,int index,int count,string text)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!Active||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(actor?.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
                (stage<1||stage>4)&&stage!=6||total<1||total>6*1024*1024+32||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase>=4||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||
                original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor))return;
            string scope="logistics-physical|"+original.World+"|"+owner.StorageKey+"|"+Digest(original.Creation)+"|"+actor.entityId+"|"+stage;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,digest,total,count,index,text,DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var bytes)||bytes==null)return;
            if(!RebirthLogisticsNativeDeathReceipt.CanObserveDetachedBytes(owner.StorageKey,operation,stage,bytes.Length)||!TryDecodePhysicalEvidence(bytes,out var detached))return;
            RebirthLogisticsNativeDeathReceipt.ObserveDetachedPhysical(actor,operation,stage,detached);
        }
        catch{/* No native file/runtime player mutation on this observation route. */}
    }
    internal static void SendPhysicalAck(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,int stage)
    {
        if(stage<1||stage>6||actor?.world?.IsRemote()!=false||!ReferenceEquals(actor.world,GameManager.Instance?.World)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=original.Owner||original.World!=RebirthLogisticsTransferJournal.WorldKey(actor.world))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!TryCurrentPeer(actor,out _,out var connection)||!SendReady(connection))return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLogisticsPhysicalAck>().Setup(original.Operation,RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),stage),_attachedToEntityId:actor.entityId);
    }
    internal static void ReceivePhysicalAck(World world,Guid operation,string binding,int stage)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||stage<1||stage>6)return;
        var player=world.GetPrimaryPlayer();
        if(player!=null&&TryReadCurrentOriginal(player,operation,out var canonical))
            foreach(var value in physicalSends.Values.Where(value=>value.Operation==operation&&value.Binding==null&&value.CommandBinding==RebirthLogisticsNativeBodyIntent.Command.From(canonical).Binding))
                value.Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(canonical);
        foreach(var pair in physicalSends.Where(pair=>pair.Value.Operation==operation&&pair.Value.Binding==binding&&
            (stage==6?pair.Value.Stage==6:pair.Value.Stage<=stage&&pair.Value.Stage<=4)).ToArray())
        {if(!pair.Value.Current())continue;physicalSendBytes-=pair.Value.Body.Length;physicalSends.Remove(pair.Key);}
        // ACK releases only transport memory. It never releases original owner
        // custody, supplies payment proof, or changes a journal/native player.
    }
    internal static bool TryDecodePhysicalEvidence(byte[] bytes,out PlayerDataFile detached)
    {
        detached=null;
        try
        {
            if(bytes==null||bytes.Length<27||bytes.Length>6*1024*1024+32)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadByte()!=1)return false;int entity=reader.ReadInt32(),deaths=reader.ReadInt32();byte dead=reader.ReadByte();
                if(entity<0||deaths<1||dead!=1)return false;
                int[] limits={2*1024*1024,2*1024*1024,1048576,1048576};var offsets=new int[4];var lengths=new int[4];
                // Validate ALL declared spans before allocating any image.
                for(int i=0;i<4;i++){int length=reader.ReadInt32();if(length<1||length>limits[i]||length>stream.Length-stream.Position)return false;lengths[i]=length;offsets[i]=(int)stream.Position;stream.Position+=length;}
                if(stream.Position!=stream.Length)return false;
                var images=new MemoryStream[4];for(int i=0;i<4;i++){var image=new byte[lengths[i]];Buffer.BlockCopy(bytes,offsets[i],image,0,image.Length);images[i]=new MemoryStream(image,false);}
                detached=new PlayerDataFile{id=entity,deaths=deaths,bDead=true,bagData=images[0],inventoryData=images[1],equipmentData=images[2],buffData=images[3]};return true;
            }
        }
        catch{detached=null;return false;}
    }
    internal static bool CanSendNative(INetConnection connection)=>SendReady(connection);
    private static bool SendReady(INetConnection connection)
    {
        if(connection==null||connection.IsDisconnected())return false;
        // Exact native NetPackage base uses reliable channel0. Inspect pressure
        // before obtaining a pooled package; native connection alone mutates it.
        if(connection is NetConnectionSimple simple)
        {lock(simple.writerListLockObj)return simple.writerListPending.Count<32&&simple.reliableBufsToSend.Count==0&&simple.unreliableBufsToSend.Count==0;}
        if(connection is NetConnectionSteam steam){lock(steam.packetsToSend)return steam.packetsToSend.Count<32;}
        return false;
    }
    private static World clientWorld;private static Guid clientOperation;private static ulong clientEpoch;private static int clientRequest;
    private static QuickStackRadialAction clientAction;private static string clientTarget,clientSelection,frameDigest,immutable;
    private static string[] parts;private static DateTime nextQuery;private static Action completed;
    private static RebirthLogisticsTransferJournal.Record latest;private static bool preparedHere,cancelRequested,cancelUnknown;
    private static void ResetClient(World currentWorld)
    {
        clientWorld=currentWorld;clientOperation=Guid.Empty;clientEpoch=0;clientRequest=0;
        clientAction=QuickStackRadialAction.Deposit;clientTarget=clientSelection=null;
        frameDigest=immutable=null;parts=null;latest=null;completed=null;physicalTerminal=null;preparedHere=cancelRequested=cancelUnknown=false;nextQuery=DateTime.MinValue;
        // Intent sidecars and native owner markers are deliberately untouched.
    }
    // Readonly fallback for an original native death scope from an already
    // authenticated canonical response. It grants no payment/replay/setter.
    internal static bool TryReadCurrentOriginal(EntityPlayerLocal player,Guid operation,out RebirthLogisticsTransferJournal.Record original)
    {
        original=null;
        try
        {
            if(player?.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,clientWorld)||!ReferenceEquals(player.world,clientWorld)||
                !ReferenceEquals(clientWorld.GetPrimaryPlayer(),player)||operation==Guid.Empty||clientOperation!=operation||latest?.Operation!=operation||latest.Phase>=4||
                latest.World!=RebirthLogisticsTransferJournal.WorldKey(clientWorld)||latest.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
                RebirthLogisticsTransferJournal.ImmutablePlanDigest(latest)!=immutable||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner.StorageKey!=latest.Owner)return false;
            return RebirthLogisticsTransferJournal.TryDecode(RebirthLogisticsTransferJournal.Encode(latest),out original);
        }
        catch{return false;}
    }
    private static string IntentPath(EntityPlayerLocal player,Guid operation,out string owner)
    {
        owner=null;
        if(player?.world?.IsRemote()!=true||operation==Guid.Empty||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))throw new InvalidDataException();
        owner=identity.StorageKey;string world=RebirthLogisticsTransferJournal.WorldKey(player.world);
        if(world==null)throw new InvalidDataException();
        return Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"RebirthLogisticsOriginalIntent",world,owner,operation.ToString("N")+".xml");
    }
    private static XElement Intent(EntityPlayerLocal player,Guid operation,string owner)=>new XElement("intent",
        new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("world",RebirthLogisticsTransferJournal.WorldKey(player.world)),
        new XAttribute("owner",owner),new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(player)),new XAttribute("deaths",player.Died),
        new XAttribute("epoch",clientEpoch),new XAttribute("request",clientRequest),new XAttribute("action",(byte)clientAction),
        new XElement("target",Convert.ToBase64String(Encoding.UTF8.GetBytes(clientTarget))),new XElement("selection",Convert.ToBase64String(Encoding.UTF8.GetBytes(clientSelection))));
    private static string ReadIntentFile(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>65536||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
            return XElement.Load(reader,LoadOptions.None).ToString(SaveOptions.DisableFormatting);
    }
    private static bool SaveIntent(EntityPlayerLocal player)
    {
        try
        {
            string path=IntentPath(player,clientOperation,out var owner);var intent=Intent(player,clientOperation,owner);
            string text=new XElement("original",new XAttribute("sha256",Digest(intent.ToString(SaveOptions.DisableFormatting))),intent).ToString(SaveOptions.DisableFormatting);
            if(Encoding.UTF8.GetByteCount(text)>65536)return false;
            if(File.Exists(path))return ReadIntentFile(path)==text;
            Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            if(File.Exists(temp)){if(ReadIntentFile(temp)!=text)return false;}
            else using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {var bytes=new UTF8Encoding(false).GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            // No replace: an existing immutable header is never overwritten.
            File.Move(temp,path);return ReadIntentFile(path)==text;
        }
        catch{return false;}
    }
    private static bool LoadIntent(EntityPlayerLocal player,Guid operation)
    {
        try
        {
            string path=IntentPath(player,operation,out var owner);var envelope=XElement.Parse(ReadIntentFile(path));
            if(envelope.Name!="original"||envelope.Attributes().Count()!=1||envelope.Elements().Count()!=1)return false;
            var value=envelope.Element("intent");if(value==null||value.Attributes().Count()!=9||value.Elements().Count()!=2||value.Element("target")==null||value.Element("selection")==null||
                (string)envelope.Attribute("sha256")!=Digest(value.ToString(SaveOptions.DisableFormatting))||
                (string)value.Attribute("version")!="1"||(string)value.Attribute("operation")!=operation.ToString("N")||
                (string)value.Attribute("world")!=RebirthLogisticsTransferJournal.WorldKey(player.world)||(string)value.Attribute("owner")!=owner||
                (string)value.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||(int)value.Attribute("deaths")!=player.Died)return false;
            ulong epoch=(ulong)value.Attribute("epoch");int request=(int)value.Attribute("request");byte action=checked((byte)(int)value.Attribute("action"));
            string target=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(value.Element("target").Value)),selection=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(value.Element("selection").Value));
            if(epoch==0||request<=0||action>(byte)QuickStackRadialAction.PullDroneCredits||target.Length>512||selection.Length>8192||value.Element("target").HasElements||value.Element("selection").HasElements)return false;
            clientEpoch=epoch;clientRequest=request;clientAction=(QuickStackRadialAction)action;clientTarget=target;clientSelection=selection;
            // Canonical re-encoding rejects unknown attributes/elements/order.
            return value.ToString(SaveOptions.DisableFormatting)==Intent(player,operation,owner).ToString(SaveOptions.DisableFormatting);
        }
        catch{return false;}
    }
    internal static bool TryReadCommandScope(EntityPlayerLocal player,Guid operation,out RebirthLogisticsNativeBodyIntent.Command command)
        =>ReadCommandScope(player,operation,false,out command);
    // Reference metadata only; never used for owner debit, body spawn or payment.
    internal static bool TryReadReferenceCommandScope(EntityPlayerLocal player,Guid operation,out RebirthLogisticsNativeBodyIntent.Command command)
        =>ReadCommandScope(player,operation,true,out command);
    private static bool ReadCommandScope(EntityPlayerLocal player,Guid operation,bool laterLifeReference,out RebirthLogisticsNativeBodyIntent.Command command)
    {
        command=null;
        try
        {
            if(player?.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,player.world)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity))return false;
            if(latest?.Operation==operation&&latest.Owner==identity.StorageKey&&latest.World==RebirthLogisticsTransferJournal.WorldKey(player.world)&&latest.Creation==RebirthCamperCharacterAccess.CurrentCreation(player))
            {command=RebirthLogisticsNativeBodyIntent.Command.From(latest);return true;}
            string path=IntentPath(player,operation,out var owner);var envelope=XElement.Parse(ReadIntentFile(path));var value=envelope.Element("intent");
            if(envelope.Name!="original"||envelope.Attributes().Count()!=1||envelope.Elements().Count()!=1||value==null||value.Attributes().Count()!=9||value.Elements().Count()!=2||
                (string)envelope.Attribute("sha256")!=Digest(value.ToString(SaveOptions.DisableFormatting))||(string)value.Attribute("version")!="1"||(string)value.Attribute("operation")!=operation.ToString("N")||
                (string)value.Attribute("owner")!=owner||(string)value.Attribute("world")!=RebirthLogisticsTransferJournal.WorldKey(player.world)||(string)value.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player))return false;
            int deaths=(int)value.Attribute("deaths"),request=(int)value.Attribute("request");ulong epoch=(ulong)value.Attribute("epoch");byte action=checked((byte)(int)value.Attribute("action"));
            var target=value.Element("target");var selection=value.Element("selection");if(target==null||selection==null||target.HasElements||selection.HasElements||target.HasAttributes||selection.HasAttributes||
                epoch==0||request<=0||deaths<0||deaths==int.MaxValue||(laterLifeReference?player.Died<deaths+1:player.Died!=deaths+1)||action>(byte)QuickStackRadialAction.PullDroneCredits)return false;
            string a=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(target.Value)),b=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(selection.Value));
            if(a.Length>512||b.Length>8192||Convert.ToBase64String(Encoding.UTF8.GetBytes(a))!=target.Value||Convert.ToBase64String(Encoding.UTF8.GetBytes(b))!=selection.Value)return false;
            command=new RebirthLogisticsNativeBodyIntent.Command(operation,(string)value.Attribute("world"),owner,(string)value.Attribute("creation"),deaths,epoch,request,(QuickStackRadialAction)action,a,b);return true;
        }
        catch{return false;}
    }
    private static string CancellationText(EntityPlayerLocal player,Guid operation,out string path)
    {
        string originalPath=IntentPath(player,operation,out var owner);path=originalPath+".cancel";
        // Original header is immutable and bounded. This marker is transport
        // intent only, never host original/payment/no-effect evidence.
        string original=ReadIntentFile(originalPath);
        return new XElement("cancel",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
            new XAttribute("world",RebirthLogisticsTransferJournal.WorldKey(player.world)),new XAttribute("owner",owner),
            new XAttribute("creation",RebirthCamperCharacterAccess.CurrentCreation(player)),new XAttribute("intent",Digest(original))).ToString(SaveOptions.DisableFormatting);
    }
    private static bool SaveCancellation(EntityPlayerLocal player)
    {
        try
        {
            string text=CancellationText(player,clientOperation,out var path);string temp=path+".tmp";
            if(File.Exists(path+".bak")||File.Exists(temp)&&ReadIntentFile(temp)!=text)return false;
            if(File.Exists(path))return ReadIntentFile(path)==text;
            if(File.Exists(temp)){if(ReadIntentFile(temp)!=text)return false;}
            else using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {byte[] bytes=new UTF8Encoding(false).GetBytes(text);if(bytes.Length>65536)return false;stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(temp,path);return ReadIntentFile(path)==text;
        }
        catch{return false;}
    }
    private static bool TryLoadCancellation(EntityPlayerLocal player,Guid operation,out bool cancel)
    {
        cancel=false;
        try
        {
            string path=IntentPath(player,operation,out _)+".cancel";bool primary=File.Exists(path),pending=File.Exists(path+".tmp");
            if(File.Exists(path+".bak"))return false;
            if(!primary&&!pending)return true;
            string text=CancellationText(player,operation,out var expectedPath);if(expectedPath!=path)return false;
            if(primary&&ReadIntentFile(path)!=text||pending&&ReadIntentFile(path+".tmp")!=text)return false;
            cancel=true;return true;
        }
        catch{return false;}
    }
    internal static void Request(EntityPlayerLocal player,QuickStackRadialAction action,string target,string selection,Action onCompleted=null)
    {
        if(!Active||player?.world?.IsRemote()!=true||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return;
        RebirthLogisticsHeaderAdmission.Tick(player);
        if(RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)||RebirthLogisticsHeaderAdmission.HasPending){ClientTick();return;}
        if(!player.IsSpawned()||player.IsDead())return;
        clientWorld=player.world;clientOperation=Guid.NewGuid();clientEpoch=(ulong)DateTime.UtcNow.Ticks;clientRequest=1;
        clientAction=action;clientTarget=target??string.Empty;clientSelection=selection??string.Empty;completed=onCompleted;
        latest=null;immutable=null;parts=null;frameDigest=null;nextQuery=DateTime.MinValue;preparedHere=cancelRequested=cancelUnknown=false;
        if(!SaveIntent(player))return;
        preparedHere=true;
        // The native hold is deferred until the authenticated host header ACK
        // and the owner's AcceptedHeader/HoldAttempted sidecars are durable.
        if(!TryReadCommandScope(player,clientOperation,out var command)||!RebirthLogisticsHeaderAdmission.Start(player,command))return;
        RebirthLogisticsHeaderAdmission.Tick(player);
    }
    private static void SendClient(EntityPlayerLocal player,Mode mode)
    {
        var game=GameManager.Instance;var world=player?.world;var persistent=game?.GetPersistentLocalPlayer();
        // Bound retries and inspect actual native channel pressure BEFORE a
        // pooled package is allocated. The same original remains pending.
        nextQuery=DateTime.UtcNow.AddSeconds(3);
        if(persistent?.PrimaryId==null||!TryOwnerConnection(out var connection,out var native)||!SendReady(native)||
            world==null||!ReferenceEquals(world,clientWorld)||!ReferenceEquals(game.World,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||player.IsDead())return;
        int deaths=player.Died;Guid operation=clientOperation;string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        bool Current()=>ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(game.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&
            ReferenceEquals(world.GetEntity(player.entityId),player)&&!player.IsDead()&&player.Died==deaths&&clientOperation==operation&&
            RebirthCamperCharacterAccess.CurrentCreation(player)==creation&&TryOwnerConnection(out var live,out var liveNative)&&ReferenceEquals(live,connection)&&ReferenceEquals(liveNative,native);
        if(!Current())return;
        var packet=NetPackageManager.GetPackage<NetPackageRebirthLogisticsOriginalRequest>();
        bool attempted=false;
        try
        {
            if(!Current()||!LogisticsTransferService.ClientChannelReady(connection,packet))return;
            if(latest?.Phase!=4&&!RebirthLogisticsHeaderAdmission.CanQueryCompleted(player,clientOperation))RebirthLogisticsOwnerReceipt.TryUploadOriginal(player,clientOperation);
            if(!Current())return;
            packet.Setup(player,persistent,operation,clientEpoch,clientRequest,clientAction,clientTarget,clientSelection,mode);
            if(!Current())return;
            attempted=true;connection.SendToServer(packet,false);
            nextQuery=DateTime.UtcNow.AddSeconds(3);
        }
        catch(Exception error){nextQuery=DateTime.UtcNow.AddSeconds(3);Log.Warning("[QuickStack] Original request retained: "+error.Message);}
        finally{if(!attempted&&packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);}
    }
    internal static void CancelCurrent()
    {
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(player!=null&&RebirthLogisticsHeaderAdmission.CancelBeforeHold(player))return;
        if(player!=null&&ReferenceEquals(player.world,clientWorld)&&RebirthLogisticsOwnerReceipt.HasPendingOriginal(player))
        {cancelRequested=true;nextQuery=DateTime.MinValue;cancelUnknown=!SaveCancellation(player);if(!cancelUnknown)SendClient(player,Mode.Cancel);}
    }
    private static void ClientTick()
    {
        var world=GameManager.Instance?.World;var player=world?.GetPrimaryPlayer();
        if(world?.IsRemote()!=true||player==null||player.IsDead()||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out _))return;
        bool headerTerminal=RebirthLogisticsHeaderAdmission.CanQueryCompleted(player,operation);
        if(!RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)&&!headerTerminal)return;
        if(!ReferenceEquals(clientWorld,world)||clientOperation!=operation)
        {
            // Cold recovery queries only. Native marker never recreates intent.
            clientWorld=world;clientOperation=operation;clientEpoch=1;clientRequest=1;clientAction=QuickStackRadialAction.Deposit;
            clientTarget=clientSelection=string.Empty;parts=null;frameDigest=immutable=null;latest=null;completed=null;nextQuery=DateTime.MinValue;
            preparedHere=LoadIntent(player,operation);cancelUnknown=!TryLoadCancellation(player,operation,out cancelRequested);if(cancelUnknown)cancelRequested=true;
        }
        if(headerTerminal)
        {if(latest?.Phase==4&&RebirthLogisticsHeaderAdmission.Complete(player,latest)){ResetClient(world);return;}if(DateTime.UtcNow>=nextQuery)SendClient(player,Mode.Query);return;}
        if(latest!=null&&(!cancelRequested||latest.Rejecting||latest.Phase>=2))
        {
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,clientWorld)&&clientOperation==latest.Operation;
            if(latest.Rejecting&&latest.Phase==1&&string.IsNullOrEmpty(latest.OwnerDebitProof)&&RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out _,out int ownerPhase)&&ownerPhase!=5)RebirthLogisticsOwnerReceipt.FreezeNoEffect(player,latest,Current);
            bool terminal=RebirthLogisticsOwnerReceipt.Advance(player,latest,latest.Phase,Current);
            if(!terminal&&latest.Phase==4)terminal=RebirthLogisticsOwnerReceipt.ReleaseCompletedOriginal(player,latest,Current);
            if(terminal){RebirthLogisticsHeaderAdmission.Complete(player,latest);var callback=completed;ResetClient(world);callback?.Invoke();return;}
        }
        if(DateTime.UtcNow>=nextQuery)
        {
            if(cancelUnknown)
            {
                // Retry the same immutable metadata write after transient I/O
                // pressure. A conflicting/corrupt candidate is never replaced.
                if(SaveCancellation(player)){cancelUnknown=false;SendClient(player,Mode.Cancel);}
                else SendClient(player,Mode.Query);
            }
            else if(cancelRequested){if(SaveCancellation(player))SendClient(player,Mode.Cancel);else cancelUnknown=true;}
            else SendClient(player,latest==null&&preparedHere?Mode.Prepare:Mode.Query);
        }
    }
    internal static void ReceivePart(World world,Guid operation,string digest,int index,int count,string text)
    {
        var player=world?.GetPrimaryPlayer();
        if(!Active||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||player==null||
            (!RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)&&!RebirthLogisticsHeaderAdmission.CanQueryCompleted(player,operation))||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var native,out _)||native!=operation||
            digest?.Length!=64||!digest.All(value=>value>='0'&&value<='9'||value>='a'&&value<='f')||count<1||count>MaxChunks||index<0||index>=count||
            text==null||text.Length>ChunkChars||index<count-1&&text.Length!=ChunkChars)return;
        if(!ReferenceEquals(clientWorld,world)||clientOperation!=operation){ClientTick();if(clientOperation!=operation)return;}
        if(frameDigest!=digest||parts==null){parts=new string[count];frameDigest=digest;}
        if(parts==null||parts.Length!=count||parts[index]!=null&&parts[index]!=text)return;
        parts[index]=text;if(parts.Any(value=>value==null))return;
        var complete=parts;parts=null;
        if(!RebirthLogisticsOriginalFrame.TryAssemble(complete,digest,out var joined)||!RebirthLogisticsTransferJournal.TryDecode(joined,out var original)||
            original.Operation!=operation||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||original.Owner!=owner.StorageKey||
            original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player))return;
        string binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original);
        if(immutable!=null&&immutable!=binding||latest!=null&&(original.Phase<latest.Phase||latest.Rejecting&&!original.Rejecting||latest.Outcome!=0&&original.Outcome!=latest.Outcome))return;
        if(original.Phase==4)
        {
            foreach(var pair in physicalSends.Where(pair=>pair.Value.Operation==operation&&(pair.Value.Binding==binding||pair.Value.CommandBinding==RebirthLogisticsNativeBodyIntent.Command.From(original).Binding)).ToArray())
            {physicalSendBytes-=pair.Value.Body.Length;physicalSends.Remove(pair.Key);}
        }
        immutable=binding;latest=original;clientEpoch=original.Epoch;clientRequest=original.Request;clientAction=original.Action;clientTarget=original.Target;clientSelection=original.Selection;
        ClientTick();
    }
}

// Pure bounded frame validation. No networking, inventory or file authority.
internal static class RebirthLogisticsOriginalFrame
{
    internal static bool TryAssemble(string[] parts,string expected,out string text)
    {
        text=null;const int chunk=12000,chunkBytes=9000,maximum=4*1024*1024;
        if(parts==null||parts.Length<1||parts.Length>(maximum+chunkBytes-1)/chunkBytes||expected?.Length!=64||
            !expected.All(value=>value>='0'&&value<='9'||value>='a'&&value<='f'))return false;
        long total=0;var decoded=new byte[parts.Length][];
        for(int i=0;i<parts.Length;i++)
        {
            string part=parts[i];if(part==null||part.Length<1||part.Length>chunk||i<parts.Length-1&&part.Length!=chunk)return false;
            try{decoded[i]=Convert.FromBase64String(part);}catch{return false;}
            if(Convert.ToBase64String(decoded[i])!=part||decoded[i].Length<1||decoded[i].Length>chunkBytes||i<parts.Length-1&&decoded[i].Length!=chunkBytes)return false;
            total+=decoded[i].Length;if(total>maximum)return false;
        }
        var bytes=new byte[(int)total];int offset=0;foreach(var part in decoded){Array.Copy(part,0,bytes,offset,part.Length);offset+=part.Length;}
        using(var sha=SHA256.Create())
        {if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",string.Empty).ToLowerInvariant()!=expected)return false;}
        try{text=new UTF8Encoding(false,true).GetString(bytes);return true;}catch{return false;}
    }
}

public sealed class NetPackageRebirthLogisticsOriginalRequest:NetPackage
{
    private int entity,request;private PlatformUserIdentifierAbs user;private Guid operation;private ulong epoch;
    private QuickStackRadialAction action;private string target,selection;private RebirthLogisticsOriginalTransport.Mode mode;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsOriginalRequest Setup(EntityPlayerLocal actor,PersistentPlayerData persistent,Guid original,ulong requestEpoch,int requestId,
        QuickStackRadialAction value,string selectedTarget,string selectedSources,RebirthLogisticsOriginalTransport.Mode requestMode)
    {entity=actor.entityId;user=persistent.PrimaryId;operation=original;epoch=requestEpoch;request=requestId;action=value;target=selectedTarget??string.Empty;selection=selectedSources??string.Empty;mode=requestMode;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new System.IO.InvalidDataException("Unsupported original logistics protocol.");
        entity=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);
        string token=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32);
        if(!Guid.TryParseExact(token,"N",out operation)||operation==Guid.Empty)throw new System.IO.InvalidDataException();
        epoch=reader.ReadUInt64();request=reader.ReadInt32();action=(QuickStackRadialAction)reader.ReadByte();mode=(RebirthLogisticsOriginalTransport.Mode)reader.ReadByte();
        target=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512);selection=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,8192);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((byte)1);writer.Write(entity);user.ToStream(writer);
        RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);writer.Write(epoch);writer.Write(request);writer.Write((byte)action);writer.Write((byte)mode);
        RebirthSurvivorNetworkCodec.WriteString(writer,target,512);RebirthSurvivorNetworkCodec.WriteString(writer,selection,8192);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,true)||!ValidEntityIdForSender(entity)||!ValidUserIdForSender(user))return;
        RebirthLogisticsOriginalTransport.Receive(world.GetEntity(entity) as EntityPlayer,operation,epoch,request,action,target,selection,mode);
    }
    public int GetLength()=>110+2*(target?.Length??0)+2*(selection?.Length??0);
}

public sealed class NetPackageRebirthLogisticsOriginalResult:NetPackage
{
    private Guid operation;private string digest,text;private int index,count;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsOriginalResult Setup(Guid original,string hash,int part,int total,string value)
    {operation=original;digest=hash;index=part;count=total;text=value;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new System.IO.InvalidDataException();
        if(!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation))throw new System.IO.InvalidDataException();
        digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);index=reader.ReadInt32();count=reader.ReadInt32();
        text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthLogisticsOriginalTransport.ChunkChars);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((byte)1);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);
        RebirthSurvivorNetworkCodec.WriteString(writer,digest,64);writer.Write(index);writer.Write(count);
        RebirthSurvivorNetworkCodec.WriteString(writer,text,RebirthLogisticsOriginalTransport.ChunkChars);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,false))RebirthLogisticsOriginalTransport.ReceivePart(world,operation,digest,index,count,text);}
    public int GetLength()=>220+2*(text?.Length??0);
}

// Fixed authenticated observation protocol. It has no SavePlayerData/ToPlayer path.
public sealed class NetPackageRebirthLogisticsPhysicalEvidence:NetPackage
{
    private int entity,stage,total,index,count;private PlatformUserIdentifierAbs user;private Guid operation;private string digest,text;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsPhysicalEvidence Setup(int actor,PlatformUserIdentifierAbs identity,Guid original,int observedStage,string hash,int length,int part,int parts,string value)
    {entity=actor;user=identity;operation=original;stage=observedStage;digest=hash;total=length;index=part;count=parts;text=value;return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadByte()!=1)throw new InvalidDataException();entity=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);
        if(!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation)||operation==Guid.Empty)throw new InvalidDataException();
        stage=reader.ReadByte();digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);total=reader.ReadInt32();index=reader.ReadInt32();count=reader.ReadInt32();
        text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,12000);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((byte)1);writer.Write(entity);user.ToStream(writer);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);
        writer.Write((byte)stage);RebirthSurvivorNetworkCodec.WriteString(writer,digest,64);writer.Write(total);writer.Write(index);writer.Write(count);RebirthSurvivorNetworkCodec.WriteString(writer,text,12000);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,true)||!ValidEntityIdForSender(entity)||!ValidUserIdForSender(user))return;
        RebirthLogisticsOriginalTransport.ReceivePhysicalEvidence(world.GetEntity(entity) as EntityPlayer,operation,stage,digest,total,index,count,text);
    }
    public int GetLength()=>300+2*(text?.Length??0);
}

public sealed class NetPackageRebirthLogisticsPhysicalAck:NetPackage
{
    private Guid operation;private string binding;private int stage;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsPhysicalAck Setup(Guid original,string immutable,int observed){operation=original;binding=immutable;stage=observed;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadByte()!=1||!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation))throw new InvalidDataException();binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);stage=reader.ReadByte();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,binding,64);writer.Write((byte)stage);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,false))RebirthLogisticsOriginalTransport.ReceivePhysicalAck(world,operation,binding,stage);}
    public int GetLength()=>210;
}

// Authenticated host assertion of EXISTING named old-life final publication.
// This package never authorizes inventory deltas or a current Before image.
public sealed class NetPackageRebirthLogisticsTerminalPhysical:NetPackage
{
    private Guid operation;private string binding,result,proof;private int deaths;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsTerminalPhysical Setup(Guid original,string immutable,string terminal,string publication,int oldDeaths)
    {operation=original;binding=immutable;result=terminal;proof=publication;deaths=oldDeaths;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadByte()!=1||!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation))throw new InvalidDataException();binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);result=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);proof=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);deaths=reader.ReadInt32();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,binding,64);RebirthSurvivorNetworkCodec.WriteString(writer,result,64);RebirthSurvivorNetworkCodec.WriteString(writer,proof,64);writer.Write(deaths);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,false))RebirthLogisticsOriginalTransport.ReceivePhysicalTerminalEvidence(world,operation,binding,result,proof,deaths);}
    public int GetLength()=>520;
}
