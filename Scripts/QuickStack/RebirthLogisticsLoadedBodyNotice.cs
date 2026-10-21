using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// Fixed server->owner reference metadata. Neither ECD is ever ToPlayer,
// EntityAdd, inventory input, spawn permission or owner/source payment proof.
internal static class RebirthLogisticsLoadedBodyNotice
{
    private static readonly bool Enabled=true;
    internal const int MaximumFrame=RebirthLogisticsNativeBodyPreflight.MaximumFrame;
    private const long MaximumRetained=16L*1024*1024;
    internal sealed class Notice:IDisposable
    {
        internal Guid Operation,Incarnation,Nonce;internal string World,Owner,Creation,Command,Binding,Proof,Digest,PolicyWitness;
        internal int Deaths,Life,Entity,Class,X,Z,Old,Next;internal byte[] Before,After;
        internal IDisposable FrameAllocation,Workspace;internal RebirthLogisticsSnapshotReservation Account;
        internal DateTime Expires,NextSend,NextApply;internal bool PairValidated,Acked,Suppressed;internal int References=1;internal long ClientCharge;
        internal ConnectionManager ReceivedManager;internal INetConnection ReceivedConnection;internal World ReceivedWorld;
        internal Notice Retain(){if(Interlocked.Increment(ref References)<=1)throw new ObjectDisposedException(nameof(Notice));return this;}
        public void Dispose(){if(Interlocked.Decrement(ref References)!=0)return;Before=null;After=null;ReceivedManager=null;ReceivedConnection=null;ReceivedWorld=null;FrameAllocation?.Dispose();Workspace?.Dispose();Account?.Dispose();if(ClientCharge!=0){Interlocked.Add(ref clientRetained,-ClientCharge);ClientCharge=0;}}
    }
    private static long clientRetained;
    private static readonly Dictionary<string,Notice> server=new Dictionary<string,Notice>(StringComparer.Ordinal),client=new Dictionary<string,Notice>(StringComparer.Ordinal);
    private static World world;private static int bodyCursor;private static DateTime nextReload;private static IEnumerator<RebirthLogisticsTransferJournal.Record> coldOriginals;
    private static void EnsureWorld(World actual)
    {
        if(ReferenceEquals(world,actual))return;
        foreach(var n in server.Values)n.Dispose();foreach(var n in client.Values)n.Dispose();server.Clear();client.Clear();coldOriginals?.Dispose();coldOriginals=null;bodyCursor=0;world=actual;
    }
    private static string Key(Notice n)=>n.Owner+":"+n.Operation.ToString("N")+":"+n.Incarnation.ToString("N")+":"+n.Life;
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static bool IsEnabled=>Enabled;
    internal static bool WireFits(Notice n)=>n!=null&&n.Before!=null&&n.After!=null&&n.Before.Length>0&&n.Before.Length<=MaximumFrame&&n.After.Length>0&&n.After.Length<=MaximumFrame&&8192L+n.Before.Length+n.After.Length<2L*1024*1024;
    internal static bool CompareLifetimeOnly(byte[] left,byte[] right)
    {
        EntityCreationData a=null,b=null;
        try
        {
            a=Decode(left);b=Decode(right);
            if(left.Length!=right.Length||float.IsNaN(a.lifetime)||float.IsInfinity(a.lifetime)||a.lifetime<0||float.IsNaN(b.lifetime)||float.IsInfinity(b.lifetime)||b.lifetime<0)return false;
            for(int i=0;i<left.Length;i++)if((i<9||i>=13)&&left[i]!=right[i])return false;return true;
        }
        finally{a?.entityData?.Dispose();a?.bagData?.Dispose();b?.entityData?.Dispose();b?.bagData?.Dispose();}
    }

    internal static bool TryChargeClient(Notice notice,int before,int after)
    {
        if(before<1||before>MaximumFrame||after<1||after>MaximumFrame)return false;
        long charge=checked(12L*(before+after)+65536);
        while(true){long old=Interlocked.Read(ref clientRetained);if(charge>MaximumRetained-old)return false;if(Interlocked.CompareExchange(ref clientRetained,old+charge,old)==old){notice.ClientCharge=charge;return true;}}
    }
    private static void Preflight(byte[] frame)=>RebirthLogisticsNativeBodyPreflight.Validate(frame,true);
    private static EntityCreationData Decode(byte[] frame)
    {
        if(frame==null||frame.Length<1||frame.Length>MaximumFrame)throw new InvalidDataException();
        Preflight(frame);var native=new EntityCreationData();
        try
        {
            using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||!RebirthLogisticsNativeInvocationJournal.Encode(native).SequenceEqual(frame))throw new InvalidDataException();}
            if(native.id<0||!EntityClass.list.TryGetValue(native.entityClass,out var definition)||definition.classname!=typeof(EntityBackpack)||native.entityData==null||native.entityData.Length>ushort.MaxValue||native.bagData==null||native.bagData.Length<1||native.bagData.Length>MaximumFrame)throw new InvalidDataException();
            foreach(float f in new[]{native.pos.x,native.pos.y,native.pos.z,native.rot.x,native.rot.y,native.rot.z})if(float.IsNaN(f)||float.IsInfinity(f))throw new InvalidDataException();
            return native;
        }
        catch{native.entityData?.Dispose();native.bagData?.Dispose();throw;}
    }
    // Pure detached inverse of the one allowed four-byte metadata transition.
    internal static byte[] Project(byte[] canonical,int from,int to)=>ProjectReference(canonical,from,to,false);
    internal static byte[] NativeSuppressedFrame(byte[] canonical,int from)=>ProjectReference(canonical,from,-1,true);
    private static byte[] ProjectReference(byte[] canonical,int from,int to,bool nativeSuppression)
    {
        if(from<0||to<0&&!(nativeSuppression&&to==-1))throw new InvalidDataException();var native=Decode(canonical);
        try
        {
            byte[] body=native.entityData.ToArray(),bag=native.bagData.ToArray();int offset=body.Length-bag.Length-4;
            if(offset<0||BitConverter.ToInt32(body,offset)!=from||!body.Skip(offset+4).SequenceEqual(bag))throw new InvalidDataException();
            Buffer.BlockCopy(BitConverter.GetBytes(to),0,body,offset,4);native.entityData.Dispose();native.entityData=new MemoryStream(body);
            return RebirthLogisticsNativeInvocationJournal.Encode(native);
        }
        finally{native.entityData?.Dispose();native.bagData?.Dispose();}
    }
    private static bool TryCurrentReadinessMode(Notice n,EntityPlayer actor,ClientInfo sender,EntityBackpack body,out bool suppressed,out string witness)
    {
        suppressed=false;witness=string.Empty;if(GamePrefs.GetInt(EnumGamePrefs.DeathPenalty)!=3)return true;
        var row=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(sender.InternalId);if(row==null||row.backpacksByID==null)return false;
        int positions=row.GetDroppedBackpackPositions().Count;
        if(positions>0)return row.backpacksByID.Count>0;
        int rows=GameManager.Instance.GetPersistentPlayerList().Players.Count;
        if(row.backpacksByID.Count!=0||rows>4096||n.Account==null||!n.Account.TryReserveAllocation(checked(1048576L+1024L*rows),out var allocation))return false;
        using(allocation)
        {
            if(!RebirthLogisticsParcelReferenceJournal.TryUntrackedNoticeWitness(actor,sender,body,out witness))return false;
            suppressed=true;return true;
        }
    }
    internal static bool HasQueued(RebirthLogisticsTransferJournal.Record original,Guid incarnation,Guid nonce)
        =>Enabled&&server.Values.Any(n=>n.Owner==original.Owner&&n.Operation==original.Operation&&n.Incarnation==incarnation&&n.Nonce==nonce&&ServerCurrent(n,out _));
    internal static bool QueuePublished(RebirthLogisticsTransferJournal.Record original,Guid incarnation,Guid nonce,EntityBackpack body,int oldOwner,int nextOwner,RebirthLogisticsBackpackPublication publication,Func<bool> current)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||current?.Invoke()!=true||original==null||original.Phase>4||incarnation==Guid.Empty||nonce==Guid.Empty||oldOwner<0||nextOwner<0||
            body?.world?.IsRemote()!=false||body.RefPlayerId!=nextOwner||!ReferenceEquals(GameManager.Instance?.World,body.world)||!ReferenceEquals(body.world.GetEntity(body.entityId),body)||!publication.Saved(out var proof))return false;
        EnsureWorld(body.world);Notice notice=null;
        try
        {
            var sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(nextOwner);var actor=body.world.GetEntity(nextOwner) as EntityPlayer;
            if(sender?.loginDone!=true||!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)||actor.Died<original.Deaths+1||RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation)return false;
            notice=new Notice{Operation=original.Operation,Incarnation=incarnation,Nonce=nonce,World=original.World,Owner=original.Owner,Creation=original.Creation,Command=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding,
                Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Proof=proof,Deaths=original.Deaths,Life=actor.Died,Entity=body.entityId,Class=body.entityClass,X=body.chunkPosAddedEntityTo.x,Z=body.chunkPosAddedEntityTo.z,Old=oldOwner,Next=nextOwner,Expires=DateTime.UtcNow.AddSeconds(45)};
            string key=Key(notice);if(server.TryGetValue(key,out var prior))
            {
                if(prior.Nonce==nonce&&prior.Proof==proof&&ServerCurrent(prior,out _))return true;
                if(ServerCurrent(prior,out _))return false;server.Remove(key);prior.Dispose();
            }
            if(server.Count>=8)return false;
            notice.Account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,current);
            if(notice.Account==null||!publication.TryCopyPublishedFrame(out notice.After,out notice.FrameAllocation)||!notice.Account.TryReserveAllocation(checked(12L*notice.After.Length+65536),out notice.Workspace))return false;
            if(!ExactLive(body,notice.After)){publication.Dispose();return false;}
            if(!TryCurrentReadinessMode(notice,actor,sender,body,out bool suppressed,out string policy))return false;
            notice.Suppressed=suppressed;notice.PolicyWitness=policy;
            if(RebirthLogisticsLoadedBodyNoticeJournal.FindMatchingWitness(original,incarnation,nonce,actor.Died,oldOwner,nextOwner,suppressed,policy,body,out var retained))
            {
                if(retained.Next!=nextOwner||retained.Old!=oldOwner||!ExactLive(body,retained.After)){retained.Dispose();return false;}
                notice.Dispose();notice=retained;
            }
            else notice.Before=Project(notice.After,nextOwner,oldOwner);
            if(!WireFits(notice)||!Project(notice.Before,oldOwner,nextOwner).SequenceEqual(notice.After)||current()!=true)return false;
            notice.Digest=RebirthLogisticsLoadedBodyNoticeJournal.Digest(notice);
            if(!RebirthLogisticsLoadedBodyNoticeJournal.Prepare(original,notice,current))return false;
            server.Add(key,notice);notice=null;return true;
        }
        catch{return false;}
        finally{notice?.Dispose();}
    }
    internal static bool ServerCurrent(Notice n,out ClientInfo sender)
    {
        sender=null;var actual=GameManager.Instance?.World;if(actual?.IsRemote()!=false||n.World!=RebirthLogisticsTransferJournal.WorldKey(actual)||n.Expires<=DateTime.UtcNow||
            !RebirthLogisticsTransferJournal.TryRead(n.Owner,n.Operation,out var original)||RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)!=n.Binding)return false;
        sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(n.Next);var actor=actual.GetEntity(n.Next) as EntityPlayer;
        return sender?.loginDone==true&&actor!=null&&!actor.IsDead()&&RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,n.Owner,out _)&&RebirthCamperCharacterAccess.CurrentCreation(actor)==n.Creation&&actor.Died==n.Life&&actor.Died>=n.Deaths+1&&
            actual.GetEntity(n.Entity) is EntityBackpack body&&body.entityClass==n.Class&&body.RefPlayerId==n.Next&&body.addedToChunk&&body.chunkPosAddedEntityTo.x==n.X&&body.chunkPosAddedEntityTo.z==n.Z&&ExactLive(body,n.After)&&
            (RebirthLogisticsParcelReferenceJournal.HasPublishedNoticeIdentity(original,n,body)||RebirthLogisticsAcceptedBodyJournal.HasPublishedNoticeIdentity(original,n,body))&&
            TryCurrentReadinessMode(n,actor,sender,body,out bool suppressed,out string policy)&&suppressed==n.Suppressed&&policy==n.PolicyWitness;
    }
    internal static bool ExactLive(EntityBackpack body,byte[] expected)
    {
        if(!RebirthLogisticsPhysicalLossWitness.OriginalBodyFitsNative(body))return false;EntityCreationData native=null;
        try{native=new EntityCreationData(body);return CompareLifetimeOnly(RebirthLogisticsNativeInvocationJournal.Encode(native),expected);}
        finally{native?.entityData?.Dispose();native?.bagData?.Dispose();}
    }
    internal static void Receive(World actual,Notice n,NetPackage packet)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||actual?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,actual)||n==null||
            !RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection)||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var received)||!ReferenceEquals(connection,received))return;
        EnsureWorld(actual);
        foreach(var stale in client.Where(pair=>!ClientConnectionCurrent(pair.Value)).ToArray()){stale.Value.Dispose();client.Remove(stale.Key);}
        n.ReceivedManager=manager;n.ReceivedConnection=connection;n.ReceivedWorld=actual;
        try{if(!Project(n.Before,n.Old,n.Next).SequenceEqual(n.After)||n.Digest!=RebirthLogisticsLoadedBodyNoticeJournal.Digest(n))return;n.PairValidated=true;}catch{return;}
        string key=Key(n)+":"+n.Nonce.ToString("N")+":"+n.Digest;if(client.TryGetValue(key,out var prior)){if(!Equivalent(prior,n))return;prior.Expires=DateTime.UtcNow.AddSeconds(45);return;}
        if(client.Count>=8)return;n.Expires=DateTime.UtcNow.AddSeconds(45);client.Add(key,n.Retain());
    }
    private static bool ClientConnectionCurrent(Notice n)=>ReferenceEquals(GameManager.Instance?.World,n.ReceivedWorld)&&
        RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection)&&ReferenceEquals(manager,n.ReceivedManager)&&ReferenceEquals(connection,n.ReceivedConnection);
    private static bool Equivalent(Notice a,Notice b)
        =>a.Operation==b.Operation&&a.Incarnation==b.Incarnation&&a.Nonce==b.Nonce&&a.World==b.World&&a.Owner==b.Owner&&a.Creation==b.Creation&&a.Command==b.Command&&a.Binding==b.Binding&&a.Proof==b.Proof&&
          a.Deaths==b.Deaths&&a.Life==b.Life&&a.Digest==b.Digest&&a.Suppressed==b.Suppressed&&a.PolicyWitness==b.PolicyWitness&&a.Entity==b.Entity&&a.Class==b.Class&&a.X==b.X&&a.Z==b.Z&&a.Old==b.Old&&a.Next==b.Next&&a.Before.SequenceEqual(b.Before)&&a.After.SequenceEqual(b.After);
    // Refuses a mixed/foreign nav even if it happens to use the same class.
    internal static bool CanRefreshOwnedNav(EntityBackpack body,out NavObject owned)
    {
        owned=body.NavObject;string name=EntityClass.list[body.entityClass].NavObject;var manager=NavObjectManager.Instance;
        var matches=manager.NavObjectList.Where(nav=>nav.IsTrackedTransform(body.transform)||nav.IsTrackedEntity(body)).Take(2).ToArray();
        if(owned==null)return matches.Length==0;
        return name!=""&&matches.Length==1&&ReferenceEquals(matches[0],owned)&&owned.IsTrackedTransform(body.transform)&&owned.OwnerEntity==null&&owned.NavObjectClassList.Count==1&&owned.NavObjectClassList[0].NavObjectClassName==name;
    }
    private static bool ApplyClient(Notice n)
    {
        var actual=GameManager.Instance?.World;var player=actual?.GetPrimaryPlayer();
        if(!ClientConnectionCurrent(n)||actual?.IsRemote()!=true||player==null||player.IsDead()||player.entityId!=n.Next||player.Died!=n.Life||player.Died<n.Deaths+1||RebirthCamperCharacterAccess.CurrentCreation(player)!=n.Creation||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner.StorageKey!=n.Owner||n.World!=RebirthLogisticsTransferJournal.WorldKey(actual)||
            !RebirthLogisticsOriginalTransport.TryReadReferenceCommandScope(player,n.Operation,out var command)||command.Binding!=n.Command||command.Deaths!=n.Deaths)return false;
        if(!(actual.GetEntity(n.Entity) is EntityBackpack body)||body.emodel==null||body.entityClass!=n.Class||!body.addedToChunk||body.chunkPosAddedEntityTo.x!=n.X||body.chunkPosAddedEntityTo.z!=n.Z||!n.PairValidated)return false;
        byte[] suppressedFrame=n.Suppressed||body.RefPlayerId==-1?NativeSuppressedFrame(n.After,n.Next):null;
        bool exact=body.RefPlayerId==n.Old&&ExactLive(body,n.Before)||body.RefPlayerId==n.Next&&ExactLive(body,n.After)||body.RefPlayerId==-1&&ExactLive(body,suppressedFrame);
        if(!exact||!CanRefreshOwnedNav(body,out var nav))return false;
        // Named native-suppressed reobservation permits only this exact -1
        // wholeframe; current normal policy is required before restoring nav.
        bool nativeSuppression=GamePrefs.GetInt(EnumGamePrefs.DeathPenalty)==3&&player.GetDroppedBackpackPositions().Count==0;
        if(n.Suppressed!=nativeSuppression)return false;
        if(n.Suppressed&&body.RefPlayerId==-1&&nav==null)return true; // Exact native-suppressed full image already read above.
        if(!n.Suppressed&&body.RefPlayerId==n.Next&&(EntityClass.list[body.entityClass].NavObject==""||nav!=null))return true;
        if(nav!=null){NavObjectManager.Instance.UnRegisterNavObject(nav);if(NavObjectManager.Instance.NavObjectList.Contains(nav))return false;body.NavObject=null;}
        // Native HandleNavObject itself produces -1 under the real policy.
        // No host pointer, native lifetime, item, tracker or player save is set.
        if(body.RefPlayerId!=n.Next)body.RefPlayerId=n.Next;body.HandleNavObject();
        if(n.Suppressed)return GamePrefs.GetInt(EnumGamePrefs.DeathPenalty)==3&&player.GetDroppedBackpackPositions().Count==0&&body.RefPlayerId==-1&&body.NavObject==null&&CanRefreshOwnedNav(body,out _)&&ExactLive(body,suppressedFrame);
        return body.RefPlayerId==n.Next&&CanRefreshOwnedNav(body,out _)&&(EntityClass.list[body.entityClass].NavObject==""||body.NavObject!=null)&&ExactLive(body,n.After);
    }
    private static bool CanObserveOriginal(RebirthLogisticsTransferJournal.Record original)
    {
        var actual=GameManager.Instance?.World;if(actual?.IsRemote()!=false||original.World!=RebirthLogisticsTransferJournal.WorldKey(actual))return false;
        var rows=GameManager.Instance.GetPersistentPlayerList()?.Players.Values.Where(row=>RebirthStablePlayerIdentity.TryFromPersistentPlayer(row,out var owner)&&owner.StorageKey==original.Owner).Take(2).ToArray();
        if(rows?.Length!=1)return false;var actor=actual.GetEntity(rows[0].EntityId) as EntityPlayer;var sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(rows[0].EntityId);
        return actor!=null&&!actor.IsDead()&&actor.Died>original.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==original.Creation&&RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _);
    }
    internal static void ReloadOriginal(RebirthLogisticsTransferJournal.Record original)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||server.Count>=8||!CanObserveOriginal(original))return;EnsureWorld(GameManager.Instance.World);
        foreach(var n in RebirthLogisticsLoadedBodyNoticeJournal.Pending(original))
        {
            n.Expires=DateTime.UtcNow.AddSeconds(45);if(server.Count<8&&!server.ContainsKey(Key(n))&&ServerCurrent(n,out _))server.Add(Key(n),n);else n.Dispose();
        }
    }
    internal static bool CanRetire(RebirthLogisticsTransferJournal.Record original,Guid incarnation,EntityBackpack body)
    {
        if(!Enabled)return false;
        return RebirthLogisticsLoadedBodyNoticeJournal.HasAcknowledgedBody(original,incarnation,body);
    }
    internal static void Ack(ClientInfo sender,Guid operation,Guid incarnation,Guid nonce,int life,string digest,bool suppressed)
    {
        try
        {
            if(!Enabled||!ThreadManager.IsMainThread()||sender==null||!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||!RebirthLogisticsLoadedBodyNoticeJournal.TryLoad(original,incarnation,nonce,life,out var n,out _,digest))return;
            using(n)
            {
                bool Current()=>n.Digest==digest&&n.Suppressed==suppressed&&ServerCurrent(n,out var actual)&&ReferenceEquals(actual,sender);
                if(Current())RebirthLogisticsLoadedBodyNoticeJournal.Acknowledge(original,n,Current);
            }
        }catch{/* No metadata readiness from a malformed/stale ACK. */}
    }
    internal static void Tick()
    {
        if(!Enabled)return;var actual=GameManager.Instance?.World;
        EnsureWorld(actual);
        if(actual?.IsRemote()==false)
        {
            var entities=actual.Entities.list;
            for(int examined=0;examined<16&&entities.Count>0;examined++)
            {if(bodyCursor>=entities.Count)bodyCursor=0;if(entities[bodyCursor++] is EntityBackpack body)RebirthLogisticsParcelReferenceJournal.PulseTerminalBody(body);}
        }
        if(actual?.IsRemote()==false&&DateTime.UtcNow>=nextReload)
        {
            nextReload=DateTime.UtcNow.AddSeconds(2);
            try
            {
            if(coldOriginals==null)coldOriginals=RebirthLogisticsLoadedBodyNoticeJournal.Originals().GetEnumerator();
            for(int work=0;work<4;work++)
            {
                if(!coldOriginals.MoveNext()){coldOriginals.Dispose();coldOriginals=null;break;}var original=coldOriginals.Current;
                if(!CanObserveOriginal(original))continue;ReloadOriginal(original);
                // Explicit terminal metadata lane. Original item Phase4 stays
                // closed; the native GUID/body and append-only ref history govern.
                if(original.Phase==4){RebirthLogisticsParcelReferenceJournal.Tick(original);RebirthLogisticsAcceptedBodyJournal.Tick(original);RebirthLogisticsDeathSettlement.TryAcquire(original,out _);RebirthLogisticsParcelReferenceJournal.CleanupTerminalMetadata(original);}
            }
            }
            catch{coldOriginals?.Dispose();coldOriginals=null;} // Unknown IO never becomes readiness.
        }
        foreach(var pair in server.ToArray())
        {
            var n=pair.Value;
            try
            {
                if(!WireFits(n)||!ServerCurrent(n,out var sender)){n.Dispose();server.Remove(pair.Key);continue;}
                if(n.NextSend>DateTime.UtcNow||!RebirthLogisticsOriginalTransport.TryCurrentPeer(actual.GetEntity(n.Next) as EntityPlayer,out var liveSender,out var connection)||!ReferenceEquals(liveSender,sender)||!RebirthLogisticsOriginalTransport.CanSendNative(connection))continue;
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLogisticsBodyReferenceNotice>().Setup(n),_onlyClientsAttachedToAnEntity:false,n.Next);n.NextSend=DateTime.UtcNow.AddSeconds(2);
            }
            catch{/* Same immutable metadata notice retained; no body replay. */}
        }
        foreach(var pair in client.ToArray())try
        {
            var n=pair.Value;if(n.Expires<=DateTime.UtcNow||!ClientConnectionCurrent(n)){n.Dispose();client.Remove(pair.Key);continue;}
            if(n.NextApply>DateTime.UtcNow)continue;n.NextApply=DateTime.UtcNow.AddMilliseconds(250);
            if(ApplyClient(n))
            {
                if(!ClientConnectionCurrent(n)||!RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection)||!RebirthLogisticsOriginalTransport.CanSendNative(connection))continue;
                manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthLogisticsBodyReferenceAck>().Setup(n));
                n.Dispose();client.Remove(pair.Key);
            }
        }catch{/* Native metadata/nav failure retains bounded notice. */}
    }
    internal static bool ValidHeader(Notice n)=>n.Operation!=Guid.Empty&&n.Incarnation!=Guid.Empty&&n.Nonce!=Guid.Empty&&n.World?.Length==32&&n.World.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')&&Hex(n.Owner)&&Hex(n.Command)&&Hex(n.Binding)&&Hex(n.Proof)&&n.Creation?.Length>0&&n.Creation.Length<=512&&n.Deaths>=0&&n.Deaths<int.MaxValue&&n.Life>n.Deaths&&n.Entity>=0&&n.Class>=0&&n.Old>=0&&n.Next>=0&&(n.Suppressed?Hex(n.PolicyWitness):n.PolicyWitness==string.Empty);
}

public sealed class NetPackageRebirthLogisticsBodyReferenceNotice:NetPackage,IMemoryPoolableObject
{
    private RebirthLogisticsLoadedBodyNotice.Notice notice;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsBodyReferenceNotice Setup(RebirthLogisticsLoadedBodyNotice.Notice value){notice?.Dispose();notice=value.Retain();return this;}
    // Native FreePackage calls Reset/Cleanup, including cancellation before
    // serialization. Only this owned immutable notice lease is released.
    public void Reset(){var prior=Interlocked.Exchange(ref notice,null);prior?.Dispose();Sender=null;}
    public void Cleanup()=>Reset();
    public override void read(PooledBinaryReader r)
    {
        Reset();if(GameManager.Instance?.World?.IsRemote()!=true||SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]==null)throw new InvalidDataException();
        var n=new RebirthLogisticsLoadedBodyNotice.Notice();
        try
        {
            if(r.ReadByte()!=3)throw new InvalidDataException();
            n.Operation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(r,32),"N");n.Incarnation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(r,32),"N");n.Nonce=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(r,32),"N");
            n.World=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);n.Owner=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);n.Creation=RebirthSurvivorNetworkCodec.ReadBoundedString(r,512);
            n.Command=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);n.Binding=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);n.Proof=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);
            n.Digest=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);byte mode=r.ReadByte();if(mode>1)throw new InvalidDataException();n.Suppressed=mode==1;n.PolicyWitness=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);n.Deaths=r.ReadInt32();n.Life=r.ReadInt32();n.Entity=r.ReadInt32();n.Class=r.ReadInt32();n.X=r.ReadInt32();n.Z=r.ReadInt32();n.Old=r.ReadInt32();n.Next=r.ReadInt32();int before=r.ReadInt32(),after=r.ReadInt32();
            if(!RebirthLogisticsLoadedBodyNotice.ValidHeader(n)||!RebirthLogisticsLoadedBodyNotice.TryChargeClient(n,before,after))throw new InvalidDataException();
            n.Before=r.ReadBytes(before);n.After=r.ReadBytes(after);if(n.Before.Length!=before||n.After.Length!=after)throw new EndOfStreamException();notice=n;n=null;
        }
        finally{n?.Dispose();}
    }
    public override void write(PooledBinaryWriter w)
    {
        var n=notice;if(n==null)throw new InvalidDataException();
        try
        {
            base.write(w);w.Write((byte)3);RebirthSurvivorNetworkCodec.WriteString(w,n.Operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(w,n.Incarnation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(w,n.Nonce.ToString("N"),32);
            RebirthSurvivorNetworkCodec.WriteString(w,n.World,64);RebirthSurvivorNetworkCodec.WriteString(w,n.Owner,64);RebirthSurvivorNetworkCodec.WriteString(w,n.Creation,512);RebirthSurvivorNetworkCodec.WriteString(w,n.Command,64);RebirthSurvivorNetworkCodec.WriteString(w,n.Binding,64);RebirthSurvivorNetworkCodec.WriteString(w,n.Proof,64);
            RebirthSurvivorNetworkCodec.WriteString(w,n.Digest,64);w.Write((byte)(n.Suppressed?1:0));RebirthSurvivorNetworkCodec.WriteString(w,n.PolicyWitness,64);w.Write(n.Deaths);w.Write(n.Life);w.Write(n.Entity);w.Write(n.Class);w.Write(n.X);w.Write(n.Z);w.Write(n.Old);w.Write(n.Next);w.Write(n.Before.Length);w.Write(n.After.Length);w.Write(n.Before);w.Write(n.After);
        }
        finally{notice=null;n.Dispose();}
    }
    public override void ProcessPackage(World actual,GameManager callbacks){var n=notice;notice=null;try{if(RebirthLogisticsOriginalTransport.AcceptPacket(this,actual,callbacks,false))RebirthLogisticsLoadedBodyNotice.Receive(actual,n,this);}finally{n?.Dispose();}}
    public int GetLength()=>8192+(notice?.Before?.Length??0)+(notice?.After?.Length??0);
}

public sealed class NetPackageRebirthLogisticsBodyReferenceAck:NetPackage
{
    private Guid operation,incarnation,nonce;private int life;private string digest;private bool suppressed;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsBodyReferenceAck Setup(RebirthLogisticsLoadedBodyNotice.Notice n){operation=n.Operation;incarnation=n.Incarnation;nonce=n.Nonce;life=n.Life;digest=n.Digest;suppressed=n.Suppressed;return this;}
    public override void read(PooledBinaryReader r)
    {if(r.ReadByte()!=2)throw new InvalidDataException();byte mode=r.ReadByte();if(mode>1)throw new InvalidDataException();suppressed=mode==1;operation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(r,32),"N");incarnation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(r,32),"N");nonce=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(r,32),"N");life=r.ReadInt32();digest=RebirthSurvivorNetworkCodec.ReadBoundedString(r,64);}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((byte)2);w.Write((byte)(suppressed?1:0));RebirthSurvivorNetworkCodec.WriteString(w,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(w,incarnation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(w,nonce.ToString("N"),32);w.Write(life);RebirthSurvivorNetworkCodec.WriteString(w,digest,64);}
    public override void ProcessPackage(World actual,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,actual,callbacks,true))RebirthLogisticsLoadedBodyNotice.Ack(Sender,operation,incarnation,nonce,life,digest,suppressed);}
    public int GetLength()=>256;
}
