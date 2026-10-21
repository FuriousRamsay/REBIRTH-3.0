using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// One typed original native request invocation. Attempted is not accepted-body
// publication and cannot supply item/payment/partition proof. Native packets
// outside an authenticated retained logistics death remain outside this scope.
internal static class RebirthLogisticsNativeInvocationJournal
{
    private sealed class Permit:IDisposable
    {
        internal Permit Previous;internal string Owner,World,Binding,Nonce,Native;internal Guid Operation;internal ClientInfo Sender;internal NetPackageRequestToSpawnEntity Packet;internal World Reference;internal string Directory;internal RebirthLogisticsBodyReferenceJournal.Frozen Projection;
        public void Dispose(){if(ReferenceEquals(active,this))active=Previous;Projection?.Dispose();Projection=null;}
    }
    [ThreadStatic]private static Permit active;
    internal static IDisposable PermitPrepared(RebirthLogisticsTransferJournal.Record original,ClientInfo sender,byte[] canonical,string nonce,NetPackageRequestToSpawnEntity packet)
    {
        if(active!=null||original==null||RebirthLogisticsBodyReferenceJournal.HasCandidate(original)||packet==null||sender==null||GameManager.Instance?.World?.IsRemote()!=false||original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World)||!ReferenceEquals(packet.Sender,sender)||!ThreadManager.IsMainThread()||!TryReadStored(original,out var storedNonce,out var native,out int state)||state!=0||storedNonce!=nonce||native!=Convert.ToBase64String(canonical)||packet.ecd==null||native!=Convert.ToBase64String(Encode(packet.ecd)))return null;
        var result=new Permit{Previous=active,Owner=original.Owner,World=original.World,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Nonce=nonce,Native=native,Operation=original.Operation,Sender=sender,Packet=packet,Reference=GameManager.Instance.World,Directory=GameIO.GetSaveGameDir()};active=result;return result;
    }
    // Named projected packet capability. The original v2 canonical bytes stay
    // untouched; its Frozen scope owns every decoded projected allocation.
    internal static IDisposable PermitProjectedPrepared(RebirthLogisticsTransferJournal.Record original,ClientInfo sender,string nonce,NetPackageRequestToSpawnEntity packet)
    {
        if(active!=null||original==null||packet?.ecd==null||sender==null||!ThreadManager.IsMainThread()||!ReferenceEquals(packet.Sender,sender)||
            !(GameManager.Instance?.World?.GetEntity(sender.entityId) is EntityPlayer actor))return null;
        var frozen=RebirthLogisticsBodyReferenceJournal.TryOpenFrozen(original);
        try
        {
            if(frozen==null||frozen.State!="prepared"||!frozen.IsCurrent(actor,sender)||frozen.Witness.Nonce!=nonce||
                !RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||!RebirthLogisticsBodyReferenceWitness.MatchesOriginalFatal(frozen.Witness,fatal)||
                frozen.Witness.Projected!=Convert.ToBase64String(Encode(packet.ecd)))return null;
            var result=new Permit{Owner=original.Owner,World=original.World,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Nonce=nonce,
                Native=frozen.Witness.Projected,Operation=original.Operation,Sender=sender,Packet=packet,Reference=GameManager.Instance.World,Directory=GameIO.GetSaveGameDir(),Projection=frozen};
            active=result;frozen=null;return result;
        }
        finally{frozen?.Dispose();}
    }
    // Fixed domain-purpose selection only for this exact authenticated native
    // packet rebuilt under our retained Prepared permit. No hash-only bypass.
    internal static bool IsExecutingPrepared(NetPackageRequestToSpawnEntity packet,World world)
    {
        try
        {
            var p=active;var sender=packet?.Sender;
            return p!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(p.Packet,packet)&&ReferenceEquals(p.Sender,sender)&&sender!=null&&world?.IsRemote()==false&&
                ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(p.Reference,world)&&GameIO.GetSaveGameDir()==p.Directory&&RebirthLogisticsTransferJournal.WorldKey(world)==p.World&&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)&&owner.StorageKey==p.Owner&&
                RebirthLogisticsTransferJournal.TryRead(p.Owner,p.Operation,out var original)&&original.Phase<4&&world.GetEntity(sender.entityId) is EntityPlayer actor&&RebirthCamperCharacterAccess.CurrentCreation(actor)==original.Creation&&RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)==p.Binding&&
                TryReadStored(original,out var nonce,out var native,out int state)&&state>=0&&state<=1&&nonce==p.Nonce&&
                (p.Projection==null?native==p.Native:p.Projection.IsCurrent(actor,sender)&&native==p.Projection.Witness.Original&&p.Native==p.Projection.Witness.Projected);
        }
        catch{return false;}
    }
    private const int MaximumBytes=524288,MaximumPending=128;
    private static string PathFor(RebirthLogisticsTransferJournal.Record original)
        =>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeInvocations",original.Owner,original.Operation.ToString("N")+".xml");
    private sealed class Limited:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position>262144-count)throw new InvalidDataException();base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=262144)throw new InvalidDataException();base.WriteByte(value);}
    }
    internal static byte[] Encode(EntityCreationData value)
    {using(var stream=new Limited())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);value.write(writer,StreamModeWrite.Persistency);writer.Flush();return stream.ToArray();}}
    private static XElement Read(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))return XElement.Load(reader);
    }
    private static bool Capacity(string root,int bytes)
    {
        if(!Directory.Exists(root))return true;if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return false;
        var directories=Directory.EnumerateDirectories(root).Take(4097).ToArray();if(directories.Length>4096)return false;
        int inspected=0;
        foreach(var directory in directories)
        {
            if(Path.GetFileName(directory)=="archive")continue;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            foreach(var path in Directory.EnumerateFiles(directory,"*.xml").Take(MaximumPending+1))
            {
                if(++inspected>MaximumPending*2)return false;
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var operation))return false;
                string owner=Path.GetFileName(directory);
                if(!RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)||original.Phase!=4)continue;
                var value=Read(path);
                if(value.Name!="logisticsNativeInvocation"||value.Attributes().Count()!=((string)value.Attribute("version")=="2"?11:10)||value.Elements().Count()!=1||value.Element("native")==null||
                    !((string)value.Attribute("version")=="1"&&(string)value.Attribute("attempted")=="1"||(string)value.Attribute("version")=="2"&&((string)value.Attribute("state")=="0"||(string)value.Attribute("state")=="1"))||(string)value.Attribute("operation")!=operation.ToString("N")||
                    (string)value.Attribute("owner")!=original.Owner||(string)value.Attribute("world")!=original.World||(string)value.Attribute("creation")!=original.Creation||
                    (string)value.Attribute("binding")!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||(int)value.Attribute("deaths")!=original.Deaths)return false;
                string archive=Path.Combine(root,"archive",owner,Path.GetFileName(path));
                if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
                if(File.Exists(archive)){if(!XNode.DeepEquals(value,Read(archive)))return false;continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(archive));File.Move(path,archive);
            }
        }
        int count=0;long total=bytes;
        foreach(var directory in directories)
        {
            if(Path.GetFileName(directory)=="archive")continue;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            foreach(var path in Directory.EnumerateFiles(directory).Take(MaximumPending+1))
            {var file=new FileInfo(path);if(++count>MaximumPending||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;total+=file.Length;if(total>32L*1024*1024)return false;}
        }
        return count<MaximumPending;
    }
    internal static bool HasAttempt(RebirthLogisticsTransferJournal.Record original)
    {
        if(RebirthLogisticsBodyReferenceJournal.BlocksNewAttempt(original))return true;
        string path=PathFor(original),archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeInvocations","archive",original.Owner,original.Operation.ToString("N")+".xml");
        if(File.Exists(path+".tmp")||File.Exists(path+".bak")||File.Exists(archive))return true;
        if(!File.Exists(path))return false;
        return !IsPrepared(original);
    }
    internal static bool IsPrepared(RebirthLogisticsTransferJournal.Record original)
        =>TryReadStored(original,out _,out _,out int state)&&state==0;
    internal static bool TryReadStored(RebirthLogisticsTransferJournal.Record original,out string nonce,out string native,out int state)
        =>TryReadStoredCore(original,false,out nonce,out native,out state);
    internal static bool TryReadOriginalReferenceEvidence(RebirthLogisticsTransferJournal.Record original,out string nonce,out string native,out int state)
        =>TryReadStoredCore(original,true,out nonce,out native,out state); // readonly archived evidence only
    private static bool TryReadStoredCore(RebirthLogisticsTransferJournal.Record original,bool terminalArchive,out string nonce,out string native,out int state)
    {
        nonce=native=null;state=-1;
        try
        {
            string path=PathFor(original);
            if(terminalArchive)
            {string archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeInvocations","archive",original.Owner,original.Operation.ToString("N")+".xml");if(File.Exists(path)&&File.Exists(archive)||File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;if(File.Exists(archive))path=archive;}
            if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;var value=Read(path);
            if(value.Name!="logisticsNativeInvocation"||value.Attributes().Count()!=11||value.Elements().Count()!=1||value.Element("native")==null||value.Element("native").HasElements||
                (string)value.Attribute("version")!="2"||!int.TryParse((string)value.Attribute("state"),out state)||state<0||state>1||
                !Guid.TryParseExact((string)value.Attribute("nonce"),"N",out var id)||id==Guid.Empty||(string)value.Attribute("operation")!=original.Operation.ToString("N")||
                (string)value.Attribute("owner")!=original.Owner||(string)value.Attribute("world")!=original.World||(string)value.Attribute("creation")!=original.Creation||
                (string)value.Attribute("binding")!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||(int)value.Attribute("deaths")!=original.Deaths||value.Element("native").Value.Length>349528)return false;
            string[] names={"version","state","nonce","operation","owner","world","creation","binding","deaths","nativeOwner","mode"};
            if(value.Attributes().Any(attribute=>attribute.Name.Namespace!=XNamespace.None||!names.Contains(attribute.Name.LocalName))||value.Element("native").HasAttributes||
                (string)value.Attribute("nonce")!=id.ToString("N")||(string)value.Attribute("state")!=state.ToString(System.Globalization.CultureInfo.InvariantCulture)||
                !int.TryParse((string)value.Attribute("nativeOwner"),out int entity)||entity<0||!int.TryParse((string)value.Attribute("mode"),out int mode)||mode<1||mode>5||
                (string)value.Attribute("nativeOwner")!=entity.ToString(System.Globalization.CultureInfo.InvariantCulture)||(string)value.Attribute("mode")!=mode.ToString(System.Globalization.CultureInfo.InvariantCulture))return false;
            nonce=id.ToString("N");native=value.Element("native").Value;return native.Length>0;
        }
        catch{return false;}
    }
    // REFUSAL ONLY: exact retained old native body while this authenticated
    // owner still carries its original hold. Never a relocation/spawn permit.
    internal static bool MustRefuseRetainedOldReference(NetPackageRequestToSpawnEntity packet,World world,RebirthLogisticsTransferJournal.Record heldOriginal)
    {
        bool classified=false;
        try
        {
            var sender=packet?.Sender;var native=packet?.ecd;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||sender==null||native==null||native.id< -1||native.entityData==null||native.bagData==null||native.entityData.Length>ushort.MaxValue||native.bagData.Length<1||native.bagData.Length>131072||native.entityData.Length<native.bagData.Length+4||!EntityClass.list.TryGetValue(native.entityClass,out var definition)||definition.classname!=typeof(EntityBackpack))return false;
            int bodyOwner=0;long position=native.entityData.Position;
            try{native.entityData.Position=native.entityData.Length-native.bagData.Length-4;for(int i=0;i<4;i++){int value=native.entityData.ReadByte();if(value<0)return false;bodyOwner|=value<<(8*i);}}finally{native.entityData.Position=position;}
            if(bodyOwner<0||bodyOwner==sender.entityId)return false;
            if(!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||
                !(world.GetEntity(sender.entityId) is EntityPlayer actor)||heldOriginal==null||heldOriginal.Owner!=owner.StorageKey||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,heldOriginal.Operation,out var original)||RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(heldOriginal)||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                !RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.OwnerEntity!=bodyOwner||fatal.OwnerEntity==sender.entityId||fatal.Mode<1||fatal.Mode>5)return false;
            string claim=PathFor(original),archiveClaim=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeInvocations","archive",original.Owner,original.Operation.ToString("N")+".xml");
            classified=File.Exists(claim)||File.Exists(claim+".tmp")||File.Exists(claim+".bak")||File.Exists(archiveClaim);
            if(!classified)return false;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,world)))
            {
                if(account==null||!account.TryReserveAllocation(4L*1024*1024,out var allocation))return true;
                using(allocation)
                {
                    if(!TryReadStoredCore(original,true,out _,out var retained,out _))return true;
                    byte[] bytes=Encode(packet.ecd);var copy=new EntityCreationData();using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);copy.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
                    copy.id=-1;return Convert.ToBase64String(Encode(copy))==retained;
                }
            }
        }
        catch{return classified;} // Known held old-body uncertainty cannot become native replay permission.
    }
    internal static bool IsAttempted(RebirthLogisticsTransferJournal.Record original,byte[] canonical)
    {
        try
        {
            string path=PathFor(original);if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
            var value=Read(path);
            return value.Name=="logisticsNativeInvocation"&&value.Attributes().Count()==((string)value.Attribute("version")=="2"?11:10)&&value.Elements().Count()==1&&
                (string)value.Attribute("operation")==original.Operation.ToString("N")&&(string)value.Attribute("owner")==original.Owner&&(string)value.Attribute("world")==original.World&&
                (string)value.Attribute("creation")==original.Creation&&(string)value.Attribute("binding")==RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)&&(int)value.Attribute("deaths")==original.Deaths&&
                ((string)value.Attribute("version")=="2"&&(string)value.Attribute("state")=="1"||(string)value.Attribute("version")=="1"&&(string)value.Attribute("attempted")=="1")&&
                value.Element("native")?.Value==Convert.ToBase64String(canonical);
        }
        catch{return false;}
    }
    internal static bool IsNativeBody(NetPackageRequestToSpawnEntity packet,int owner)
    {
        try
        {
            var native=packet?.ecd;if(native==null||native.id< -1||native.entityData==null||native.bagData==null||native.entityData.Length>ushort.MaxValue||native.bagData.Length<1||native.bagData.Length>131072||
                !EntityClass.list.TryGetValue(native.entityClass,out var definition)||definition.classname!=typeof(EntityBackpack)||!packet.ValidEntityIdForSender(owner))return false;
            var body=native.entityData;var bag=native.bagData;if(body.Length<bag.Length+4)return false;
            long bodyPosition=body.Position,bagPosition=bag.Position;
            try
            {
                body.Position=body.Length-bag.Length-4;int id=0;
                for(int i=0;i<4;i++){int part=body.ReadByte();if(part<0)return false;id|=part<<(8*i);}
                if(id!=owner)return false;bag.Position=0;
                for(long i=0;i<bag.Length;i++)if(body.ReadByte()!=bag.ReadByte())return false;return true;
            }
            finally{body.Position=bodyPosition;bag.Position=bagPosition;}
        }
        catch{return false;}
    }
    // Called only after actual sender, final native held original and actual
    // server fatal lineage have been rederived by the sole native packet hook.
    // Once classified, all I/O uncertainty refuses native re-execution.
    internal static bool TryBegin(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeDeathReceipt.Record fatal,EntityPlayer actor,NetPackageRequestToSpawnEntity packet)
        =>PrepareOrBegin(original,fatal,actor,packet,true);
    internal static bool TryPrepare(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeDeathReceipt.Record fatal,EntityPlayer actor,NetPackageRequestToSpawnEntity packet)
        =>PrepareOrBegin(original,fatal,actor,packet,false);
    internal static bool TryPrepareHistorical(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeDeathReceipt.Record fatal,EntityPlayer actor,NetPackageRequestToSpawnEntity packet)
        =>PrepareOrBegin(original,fatal,actor,packet,false,true);
    private static bool PrepareOrBegin(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeDeathReceipt.Record fatal,EntityPlayer actor,NetPackageRequestToSpawnEntity packet,bool attempt,bool historical=false)
    {
        try
        {
            // A surviving actual body/settlement receipt is never a reason
            // to recreate a missing generic Prepared invocation as unused.
            if(!attempt&&(RebirthLogisticsAcceptedBodyJournal.HasAnyCandidate(original)||RebirthLogisticsDeathJournal.HasAnyCandidate(original)))return false;
            bool projected=active?.Projection!=null&&ReferenceEquals(active.Packet,packet)&&IsExecutingPrepared(packet,actor?.world);
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||packet?.ecd==null||fatal==null||
                (projected?fatal.OwnerEntity!=active.Projection.Witness.HistoricalOwner:!historical&&fatal.OwnerEntity!=actor.entityId)||
                fatal.Operation!=original.Operation||fatal.Owner!=original.Owner||fatal.World!=original.World||fatal.Creation!=original.Creation||fatal.Deaths!=original.Deaths||
                fatal.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||fatal.Mode<1||fatal.Mode>5||actor.Died<original.Deaths||actor.Died>original.Deaths+1)return false;
            var sender=packet.Sender;
            if(sender==null||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||sender.entityId!=actor.entityId||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId),sender)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||owner.StorageKey!=original.Owner||RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||retained.Phase>=4||
                RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original)||
                !RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var savedOperation,out int savedPhase,out float held,out var saved)||savedOperation!=original.Operation||held!=1f||savedPhase>4||saved.deaths<original.Deaths||saved.deaths>original.Deaths+1||
                !RebirthLogisticsNativeDeathReceipt.TryRead(retained,out var actualFatal)||actualFatal.Binding!=fatal.Binding||actualFatal.OwnerEntity!=fatal.OwnerEntity||actualFatal.Mode!=fatal.Mode)return false;
            if(!RebirthLogisticsHeaderAdmission.TryBodyIndexHeader(original,out var acceptedHeader,out bool requiresIndex)||
                !RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var nativeHeader)||nativeHeader!=acceptedHeader)return false;
            if(historical&&(!requiresIndex||attempt||!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)||fatal.OwnerEntity==actor.entityId))return false;
            var native=packet.ecd;if(native.entityData==null||native.bagData==null||native.entityData.Length<4||native.entityData.Length>ushort.MaxValue||native.bagData.Length<1||native.bagData.Length>131072||native.id!=-1||
                !EntityClass.list.TryGetValue(native.entityClass,out var definition)||definition.classname!=typeof(EntityBackpack))return false;
            foreach(float coordinate in new[]{native.pos.x,native.pos.y,native.pos.z,native.rot.x,native.rot.y,native.rot.z})if(float.IsNaN(coordinate)||float.IsInfinity(coordinate))return false;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,actor.world)&&RebirthLogisticsTransferJournal.WorldKey(actor.world)==original.World))
            {
            long required=checked(8L*(native.bagData.Length+native.entityData.Length+262144L)+1048576L);
            if(account==null||!account.TryReserveAllocation(required,out var allocation))return false;
            using(allocation)
            {
            byte[] bag=native.bagData.ToArray(),body=native.entityData.ToArray();
            if(body.Length<bag.Length+4||BitConverter.ToInt32(body,body.Length-bag.Length-4)!=(historical?fatal.OwnerEntity:actor.entityId)||!packet.ValidEntityIdForSender(actor.entityId))return false;
            for(int i=0;i<bag.Length;i++)if(body[body.Length-bag.Length+i]!=bag[i])return false;
            if(historical&&((fatal.Physical?.Stage??0)<4||fatal.Physical.Requests!=1||fatal.Physical.BodyByteRefused||fatal.Physical.BodyClass!=native.entityClass||!fatal.Physical.BodyPosition.Equals(native.pos)||
                fatal.Physical.BodyBagHash!=RebirthLogisticsPhysicalLossWitness.BlobHash(native.bagData)||fatal.Physical.BodyEntityHash!=RebirthLogisticsPhysicalLossWitness.BlobHash(native.entityData)))return false;
            byte[] bytes=Encode(native);var copy=new EntityCreationData();
            try{using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);copy.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||!Encode(copy).SequenceEqual(bytes))return false;}}
            finally{copy.entityData?.Dispose();copy.bagData?.Dispose();}
            if(projected)
            {
                // Same actual packet purpose and exact canonical projected
                // bytes; fsynced original-wide Attempted BEFORE native spawn.
                if(!attempt||active.Native!=Convert.ToBase64String(bytes)||active.Projection.State!="prepared")return false;
                if(requiresIndex&&!RebirthLogisticsBodyAdmissionIndex.Attempt(original,acceptedHeader,active.Nonce,active.Projection.Witness.Original))return false;
                return active.Projection.TryMarkAttempted(actor,sender);
            }
            string path=PathFor(original),temp=path+".tmp";string nonce=Guid.NewGuid().ToString("N");
            if(File.Exists(path))nonce=(string)Read(path).Attribute("nonce");
            else if(File.Exists(temp))nonce=(string)Read(temp).Attribute("nonce");
            else if(requiresIndex&&RebirthLogisticsBodyAdmissionIndex.TryPrepared(original,acceptedHeader,out var indexedNonce,out var indexedNative))
            {if(indexedNative!=Convert.ToBase64String(bytes))return false;nonce=indexedNonce;}
            // Legacy absent invocation cannot become explicit None. Only an
            // already retained exact Prepared/Attempted participates in migration.
            if(!requiresIndex&&!File.Exists(path)&&!File.Exists(temp))return false;
            if(!Guid.TryParseExact(nonce,"N",out var nonceId)||nonceId==Guid.Empty)return false;
            if(attempt&&(active==null||active.Owner!=original.Owner||active.World!=original.World||active.Operation!=original.Operation||active.Binding!=fatal.Binding||
                active.Nonce!=nonce||active.Native!=Convert.ToBase64String(bytes)||!ReferenceEquals(active.Packet,packet)||!ReferenceEquals(active.Sender,sender)||!File.Exists(path)))return false;
            var value=new XElement("logisticsNativeInvocation",new XAttribute("version",2),new XAttribute("state",0),new XAttribute("nonce",nonce),new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("owner",original.Owner),new XAttribute("world",original.World),new XAttribute("creation",original.Creation),new XAttribute("binding",fatal.Binding),
                new XAttribute("deaths",original.Deaths),new XAttribute("nativeOwner",fatal.OwnerEntity),new XAttribute("mode",fatal.Mode),new XElement("native",Convert.ToBase64String(bytes)));
            // Original index Prepared is durable before the generic journal.
            // Attempted/missing Ready index can never reconstruct Prepared.
            if(requiresIndex&&!RebirthLogisticsBodyAdmissionIndex.Prepare(original,acceptedHeader,nonce,Convert.ToBase64String(bytes)))return false;
            // Only positively retained exact Prepared0 can execute. Existing
            // attempted/unknown candidates never become replay permission.
            if(!attempt&&!File.Exists(path)&&!File.Exists(path+".bak")&&File.Exists(temp))
            {if(!XNode.DeepEquals(value,Read(temp)))return false;File.Move(temp,path);}
            if(File.Exists(temp)||File.Exists(path+".bak")||HasAttempt(original))return false;
            if(File.Exists(path)){if(!XNode.DeepEquals(value,Read(path)))return false;}
            else
            {
                byte[] prepared=new UTF8Encoding(false).GetBytes(value.ToString(SaveOptions.DisableFormatting));
                string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeInvocations");if(prepared.Length>MaximumBytes||!Capacity(root,prepared.Length))return false;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(prepared,0,prepared.Length);stream.Flush(true);}
                File.Move(temp,path);if(!XNode.DeepEquals(value,Read(path)))return false;
            }
            if(!attempt)return true;
            if(requiresIndex&&!RebirthLogisticsBodyAdmissionIndex.Attempt(original,acceptedHeader,nonce,Convert.ToBase64String(bytes)))return false;
            value.SetAttributeValue("state",1);byte[] attempted=new UTF8Encoding(false).GetBytes(value.ToString(SaveOptions.DisableFormatting));
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(attempted,0,attempted.Length);stream.Flush(true);}
            File.Replace(temp,path,null);return XNode.DeepEquals(value,Read(path));
            }
            }
        }
        catch{return false;}
    }
}
