using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

// Native commit implementation for the original-purpose Block protocol.
// Native subclass/shape
// effects beyond the retained footprint remain explicitly unqualified.
internal static class RebirthRemoteResourceBlockHost
{
    internal enum Status{Unknown,Waiting,Published,Declined}
    private sealed class Watch
    {
        internal World World;internal Chunk Chunk;internal string Directory;internal Func<bool> ProviderCurrent;
        internal RebirthRemoteResourceClientOriginal.Intent Original;internal RebirthRemoteResourceDurableJournal.Record Root;
        internal RebirthRemoteResourceBlockCommand.Accepted Command;internal BlockValue After;internal sbyte Density;
        internal string AfterBirth;internal byte[] Stamp,BirthImage;internal RebirthStationSnapshotEvidence.Request Request;internal float Started;
    }
    private static readonly Dictionary<Guid,Watch> watches=new Dictionary<Guid,Watch>();
    internal static bool CaptureProjection(World world,RebirthRemoteResourceClientOriginal.Intent original,out string encoded)
    {
        encoded=null;
        try
        {
            if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||
                !(root.Phase==4&&RebirthRemoteResourceBlockSettlement.HasSavedTerminal(world,original.Operation,original.Digest,Position(original),command.Birth)||
                  RebirthRemoteResourceBlockCommand.TryReadDeclined(world,original,out _))||
                !RebirthRemoteResourceBlockProjection.TryCoordinates(original,command.Changes,out var positions))return false;
            var changes=new List<BlockChangeInfo>();
            foreach(var position in positions)
            {
                var chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
                if(position.y<0||position.y>=255||chunk==null||chunk.InProgressUnloading)return false;
                changes.Add(new BlockChangeInfo(new BlockValueRef(position),world.GetBlock(position),world.GetDensity(position),new TextureFullArray(world.ChunkCache.GetTextureFull(position))));
            }
            // Actual host images, including paint cleared optimistically by an
            // owner upgrade, never a client-proposed replacement or payment.
            return RebirthOriginalNativeBlockChange.TryEncode(changes,out encoded);
        }
        catch{encoded=null;return false;}
    }
    internal static JObject CaptureAuditState(World world,RebirthRemoteResourceDurableJournal.Record root)
    {
        var result=new JObject{["available"]=false,["producerActive"]=RebirthRemoteResourceBlockProtocol.IsActive};
        try
        {
            var image=root?.Copy();
            result["sourceReadable"]=root!=null;result["sourcePhase"]=root?.Phase??0;
            if(root!=null&&RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(root.Operation,out var mapped))
            {result["original"]=mapped.Operation.ToString("N");result["originalDigest"]=mapped.Digest;}
            result["ownerPermissionRecorded"]=image?.Element("ownerAction")!=null;
            result["ownerRejecting"]=(int?)image?.Attribute("ownerRejecting")??0;
            result["noUseRecorded"]=!string.IsNullOrEmpty((string)image?.Attribute("ownerNoUseProof"));
            var refund=image?.Element("ownerRefund")?.Element("blockRefund");
            result["refundRecipientDeaths"]=(int?)refund?.Attribute("deaths")??(int?)image?.Attribute("deaths")??-1;
            result["refundChild"]=(string)refund?.Attribute("child")??string.Empty;
            result["disposition"]=(string)image?.Attribute("disposition")??string.Empty;
            string child=(string)image?.Element("ownerAction")?.Attribute("child");
            if(!Guid.TryParseExact(child,"N",out var operation)||!RebirthRemoteResourceBlockAdmission.TryReadOriginal(operation,out var original)||
                !RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var actual,out var command)||actual.Operation!=root.Operation||actual.Binding!=root.Binding)return result;
            bool saved=TryReadSaved(world,original,out var proof),declined=RebirthRemoteResourceBlockCommand.TryReadDeclined(world,original,out var decline);
            result["available"]=true;result["original"]=operation.ToString("N");result["commandDigest"]=command.Digest;result["originalDeaths"]=original.Deaths;
            result["attemptCandidatePresent"]=AnyAttempt(operation);result["saved"]=saved;result["savedProof"]=saved?proof:"";result["declined"]=declined;result["declineProof"]=declined?decline:"";
            result["footprintReadable"]=RebirthRemoteResourceBlockFootprintAdmission.TryRead(original,command,out var cells);
            result["footprintCells"]=cells?.Length??0;return result;
        }
        catch{result["unknown"]=true;return result;}
    }
    private static World boundWorld;
    private static string boundDirectory;
    private static void Bind(World world)
    {
        string directory=GameIO.GetSaveGameDir();
        if(ReferenceEquals(boundWorld,world)&&boundDirectory==directory)return;
        foreach(var watch in watches.Values)watch.Request?.Dispose();
        watches.Clear();boundWorld=world;boundDirectory=directory;
        // Only transient observations are discarded. The attempted original
        // and its native effect remain durable and prohibit another invocation.
    }
    private static RebirthRemoteResourceClientOriginalJournal Journal(string name)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name));
    private static string Key(Guid operation)=>"rebirth.remote.block.effect.v1:"+operation.ToString("N");
    private static Vector3i Position(RebirthRemoteResourceClientOriginal.Intent original)
    {var action=original.Copy().Element("action");return new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));}
    private static bool Publish(string name,RebirthRemoteResourceClientOriginal.Intent original,XElement body)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>16384)return false;
        var journal=Journal(name);var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static bool ExactEvent(string name,RebirthRemoteResourceClientOriginal.Intent original,XElement expected)
    {
        var record=Journal(name).Read(original.Operation);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Operation&&record.OwnerCreation==original.Creation&&record.Revision==0&&
            record.Before.Length<=16384&&record.Before.SequenceEqual(record.After)&&record.Before.SequenceEqual(Encoding.UTF8.GetBytes(expected.ToString(SaveOptions.DisableFormatting)));
    }
    private static XElement Attempt(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceBlockCommand.Accepted command)
        =>new XElement("blockHostAttempt",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),
            new XAttribute("binding",root.Binding),new XAttribute("command",command.Digest),new XAttribute("birth",command.Birth),new XAttribute("ownerProof",command.OwnerProof));
    private static bool AnyAttempt(Guid operation)
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockHostAttempts");
        return Directory.Exists(directory)&&((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFiles(directory,operation.ToString("N")+".*").Any()||
            Directory.Exists(Path.Combine(directory,"completed",operation.ToString("N"))));
    }
    private static bool ReadEffect(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceBlockCommand.Accepted command,
        byte[] stamp,byte[] birthImage,out BlockValue after,out sbyte density,out string afterBirth)
    {
        after=BlockValue.Air;density=0;afterBirth=null;
        try
        {
            if(stamp==null||stamp.Length>2048||!RebirthOriginalNativeBlockChange.TryDecode(command.Changes,out var changes)||changes.Count!=1||!changes[0].bChangeBlockValue)return false;
            after=changes[0].blockValue;
            var originalAction=original.Copy().Element("action");
            if((int)originalAction.Attribute("y")==0){after=new BlockValue((uint)(ulong)originalAction.Attribute("blockRaw"));after.damage=(int)originalAction.Attribute("blockDamage");}
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(stamp),2048);
            int d=(int)body.Attribute("density");afterBirth=(string)body.Attribute("afterBirth");
            if(d<sbyte.MinValue||d>sbyte.MaxValue||!Guid.TryParseExact(afterBirth,"N",out var token)||token==Guid.Empty||token.ToString("N")!=afterBirth)return false;
            density=(sbyte)d;
            if(!stamp.SequenceEqual(Encoding.UTF8.GetBytes(Effect(original,root,command,after,density,afterBirth).ToString(SaveOptions.DisableFormatting)))||
                !RebirthOriginalNativeBlockIdentity.IsRecordedClaim(world,birthImage,1,original.Operation,original.Digest,command.Birth,afterBirth,after.rawData))return false;
            return true;
        }
        catch{after=BlockValue.Air;density=0;afterBirth=null;return false;}
    }
    private static bool TryRestoreWatch(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceBlockCommand.Accepted command,out Watch watch)
    {
        watch=null;
        try
        {
            if(watches.Count>=8||!ExactEvent("RemoteBlockHostAttempts",original,Attempt(original,root,command)))return false;
            var position=Position(original);var chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
            if(chunk==null||chunk.InProgressUnloading||!chunk.ChunkCustomData.dict.TryGetValue(Key(original.Operation),out var marker)||marker?.data==null||
                marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null)return false;
            byte[] birth=RebirthOriginalNativeBlockIdentity.CopyMarker(world,position);
            if(!ReadEffect(world,original,root,command,marker.data,birth,out var after,out var density,out var afterBirth)||world.GetBlock(position).rawData!=after.rawData||
                world.GetBlock(position).damage!=after.damage||world.GetDensity(position)!=density||command.ClearTexture&&world.ChunkCache.GetTextureFull(position)!=0L)return false;
            // The typed actual-effect marker, not coincidental current image,
            // is indispensable. Recovery never stamps or repeats ChangeBlocks.
            return MakeWatch(world,original,root,command,after,density,afterBirth,marker.data,out watch);
        }
        catch{return false;}
    }
    private static bool ValidSavedBody(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,
        RebirthRemoteResourceBlockCommand.Accepted command,byte[] bytes,out string proof,out byte[] stamp,out byte[] birth)
    {
        proof=null;stamp=birth=null;
        try
        {
            if(bytes==null||bytes.Length>16384)return false;string text=new UTF8Encoding(false,true).GetString(bytes);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,16384);
            string regionProof=(string)body.Attribute("regionProof");if(regionProof?.Length!=64||regionProof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
            if(!RebirthRemoteResourceBlockFootprintPublication.TryReadSaved(world,original,command,out var childProof)||(string)body.Attribute("childProof")!=childProof)return false;
            string encoded=body.Element("actualEffect")?.Value,encodedBirth=body.Element("birthImage")?.Value;
            if(encoded==null||encoded.Length>2732||encodedBirth==null||encodedBirth.Length>1368)return false;
            stamp=Convert.FromBase64String(encoded);birth=Convert.FromBase64String(encodedBirth);
            if(Convert.ToBase64String(stamp)!=encoded||Convert.ToBase64String(birth)!=encodedBirth||!ReadEffect(world,original,root,command,stamp,birth,out _,out _,out _))return false;
            var canonical=new XElement("blockHostSaved",new XAttribute("version",2),new XAttribute("original",original.Digest),new XAttribute("command",command.Digest),new XAttribute("regionProof",regionProof),new XAttribute("childProof",childProof),
                new XElement("actualEffect",encoded),new XElement("birthImage",encodedBirth));
            if(text!=canonical.ToString(SaveOptions.DisableFormatting))return false;proof=RebirthRemoteResourceDurableJournal.Digest(text);return true;
        }
        catch{proof=null;stamp=birth=null;return false;}
    }
    internal static bool TryReadSaved(World world,RebirthRemoteResourceClientOriginal.Intent original,out string proof)
    {
        proof=null;
        try
        {
            if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||
                RebirthRemoteResourceBlockCommand.HasDeclinedOrUnknown(original)||!ExactEvent("RemoteBlockHostAttempts",original,Attempt(original,root,command)))return false;
            var record=Journal("RemoteBlockHostSaved").Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||
                !record.Before.SequenceEqual(record.After)||!ValidSavedBody(world,original,root,command,record.Before,out proof,out _,out _))return false;
            return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool RetirePublished(World world,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||
                !RebirthRemoteResourceBlockSettlement.HasSavedTerminal(world,original.Operation,original.Digest,Position(original),command.Birth))return false;
            var record=Journal("RemoteBlockHostSaved").Read(original.Operation);
            if(!ValidSavedBody(world,original,root,command,record.Before,out _,out var expectedStamp,out _))return false;
            var position=Position(original);var chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;
            if(chunk==null||chunk.InProgressUnloading)return false;
            string key=Key(original.Operation);
            if(chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
            {
                if(marker?.data==null||!marker.data.SequenceEqual(expectedStamp)||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null)return false;
                chunk.ChunkCustomData.Remove(key);chunk.isModified=true;
            }
            // Only the already-published original's own metadata is retired.
            // The positive event and source terminal remain durable authority.
            RebirthOriginalNativeBlockIdentity.Retire(world,position,1,original.Operation,original.Digest,command.Birth);
            bool childrenRetired=RebirthRemoteResourceBlockFootprintPublication.Retire(world,original,command);
            RebirthRemoteResourceBlockFootprintAdmission.Release(original);
            return childrenRetired;
        }
        catch{return false;}
    }
    private static XElement Effect(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceBlockCommand.Accepted command,
        BlockValue after,sbyte density,string afterBirth)
        =>new XElement("blockActualEffect",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),
            new XAttribute("binding",root.Binding),new XAttribute("command",command.Digest),new XAttribute("beforeBirth",command.Birth),new XAttribute("afterBirth",afterBirth),
            new XAttribute("raw",after.rawData),new XAttribute("damage",after.damage),new XAttribute("density",density),new XAttribute("ownerProof",command.OwnerProof));
    private static bool BeforeCurrent(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command)
    {
        if(!RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out var birth,out var density,out var painted)||birth!=command.Birth)return false;
        var intent=original.Copy().Element("action");var position=Position(original);var before=world.GetBlock(position);
        return Convert.ToUInt64(before.rawData)==(ulong)intent.Attribute("blockRaw")&&before.damage==(int)intent.Attribute("blockDamage")&&world.GetDensity(position)==density&&
            (world.ChunkCache.GetTextureFull(position)!=0L)==painted&&RebirthOriginalNativeBlockIdentity.TryReadClaim(world,position,1,original.Operation,original.Digest,out var admitted,out var current)&&admitted==birth&&current==birth;
    }
    internal static bool Ready(World world,Vector3i position,out Chunk chunk,out ChunkProviderGenerateWorld provider,out Func<bool> providerCurrent)
    {
        chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;providerCurrent=null;
        var regions=provider?.m_RegionFileManager;if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||chunk.ChunkCustomData?.dict==null||chunk.ChunkCustomData.dict.Count>=ushort.MaxValue)return false;
        lock(chunk)if(chunk.InProgressUnloading||chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return false;
        var exactProvider=provider;var exactChunk=chunk;var access=regions.regionFileAccess;string path=Path.GetFullPath(regions.saveDirectory);string directory=GameIO.GetSaveGameDir();
        providerCurrent=()=>ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&GameIO.GetSaveGameDir()==directory&&
            ReferenceEquals(world.GetChunkSync(exactChunk.X,exactChunk.Z),exactChunk)&&!exactChunk.InProgressUnloading&&ReferenceEquals(world.ChunkCache?.ChunkProvider,exactProvider)&&
            ReferenceEquals(exactProvider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==path;
        return providerCurrent();
    }
    private static bool StampAfterActualCall(RebirthRemoteResourceBlockActorScope.Scope invoked,RebirthRemoteResourceDurableJournal.Record root,BlockValue expected,out Watch watch)
    {
        watch=null;
        if(invoked?.InvocationCompleted!=true||invoked.Calls!=1||!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(invoked.World,invoked.Original,out var currentRoot,out var command)||
            currentRoot.Operation!=root.Operation||currentRoot.Binding!=root.Binding||command.Digest!=invoked.Accepted.Digest||!ExactEvent("RemoteBlockHostAttempts",invoked.Original,Attempt(invoked.Original,root,command)))return false;
        var world=invoked.World;var original=invoked.Original;var position=Position(original);var actual=world.GetBlock(position);
        if(actual.rawData!=expected.rawData||actual.damage!=expected.damage||command.ClearTexture&&world.ChunkCache.GetTextureFull(position)!=0L||
            !RebirthOriginalNativeBlockIdentity.TryReadClaim(world,position,1,original.Operation,original.Digest,out var before,out var after)||before!=command.Birth)return false;
        var chunk=world.GetChunkSync(World.toChunkXZ(position.x),World.toChunkXZ(position.z)) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;
        sbyte density=world.GetDensity(position);byte[] stamp=Encoding.UTF8.GetBytes(Effect(original,root,command,actual,density,after).ToString(SaveOptions.DisableFormatting));if(stamp.Length>2048)return false;
        string key=Key(original.Operation);if(chunk.ChunkCustomData.dict.ContainsKey(key))return false;
        chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=stamp});chunk.isModified=true;
        return MakeWatch(world,original,root,command,actual,density,after,stamp,out watch);
    }
    private static bool MakeWatch(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceBlockCommand.Accepted command,
        BlockValue after,sbyte density,string afterBirth,byte[] stamp,out Watch watch)
    {
        watch=null;if(!Ready(world,Position(original),out var chunk,out _,out var provider))return false;
        byte[] birth=RebirthOriginalNativeBlockIdentity.CopyMarker(world,Position(original));
        if(!RebirthOriginalNativeBlockIdentity.IsRecordedClaim(world,birth,1,original.Operation,original.Digest,command.Birth,afterBirth,after.rawData))return false;
        watch=new Watch{World=world,Chunk=chunk,Directory=GameIO.GetSaveGameDir(),ProviderCurrent=()=>provider()&&world.worldState.Guid==original.World,Original=original,Root=root,Command=command,After=after,Density=density,AfterBirth=afterBirth,Stamp=(byte[])stamp.Clone(),BirthImage=birth};return true;
    }
    // Actual once-only call. Recovery never enters this branch if ANY attempt
    // primary/candidate exists, even when the current block resembles Before.
    internal static Status TryInvokeAccepted(World world,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            Bind(world);
            if(!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(world,original,out var root,out var command)||root.Phase!=2)return Status.Unknown;
            if(RebirthRemoteResourceBlockCommand.HasDeclinedOrUnknown(original))return RebirthRemoteResourceBlockCommand.TryReadDeclined(world,original,out _)?Status.Declined:Status.Unknown;
            if(TryReadSaved(world,original,out _))return Status.Published;
            if(watches.TryGetValue(original.Operation,out var retained))
            {
                if(!ReferenceEquals(retained.World,world)||retained.Original.Digest!=original.Digest||retained.Command.Digest!=command.Digest)return Status.Unknown;
                if(retained.ProviderCurrent?.Invoke()==true)return Observe(retained,out _)?Status.Published:Status.Waiting;
                retained.Request?.Dispose();watches.Remove(original.Operation);
                // An unloaded/replaced chunk object cannot be observed through
                // its old request. Rebind only the exact typed native effect;
                // the durable attempt below still forbids replay.
            }
            if(AnyAttempt(original.Operation))
            {
                if(!TryRestoreWatch(world,original,root,command,out var restored))return Status.Unknown;
                watches[original.Operation]=restored;return Observe(restored,out _)?Status.Published:Status.Waiting;
            }
            if(!BeforeCurrent(world,original,command))return RebirthRemoteResourceBlockCommand.DeclineAcceptedWorld(world,original,out _)?Status.Declined:Status.Unknown;
            if(!RebirthRemoteResourceBlockAdmission.TryReadTarget(original,out _,out var density,out var painted)||
                !RebirthRemoteResourceBlockTransition.AllowsFrozen(original,command.Changes,density,painted,command.ObservedClearTexture,command.Context,out var expected,out var hostClear)||hostClear!=command.ClearTexture)return Status.Unknown;
            var position=Position(original);
            if(!RebirthRemoteResourceBlockFootprintAdmission.Prepare(world,original,expected,out var children))return Status.Waiting;
            if(watches.Count>=8||!Ready(world,position,out _,out _,out _)||!RebirthOriginalNativeBlockChange.TryDecode(command.Changes,out var changes))return Status.Waiting;
            RebirthRemoteResourceBlockActorScope.Install();
            using(var scope=RebirthRemoteResourceBlockActorScope.Begin(world,original,changes))
            {
                if(scope==null)return Status.Unknown;
                if(!Publish("RemoteBlockHostAttempts",original,Attempt(original,root,command)))return Status.Unknown;
                if(!BeforeCurrent(world,original,command)||!RebirthRemoteResourceBlockFootprintAdmission.BeforeCurrent(world,original,command))return Status.Unknown;
                GameManager.Instance.ChangeBlocks(scope.NativeUser,changes);
                if(!scope.InvocationCompleted)return Status.Unknown;
                if(!RebirthRemoteResourceBlockFootprintPublication.CaptureAfter(scope))return Status.Unknown;
                if(command.ClearTexture)GameManager.Instance.SetBlockTextureServer(new BlockValueRef(position),BlockFace.None,0,command.NativeEntity);
                Broadcast(scope);
                if(!StampAfterActualCall(scope,root,expected,out var watch))return Status.Unknown;
                watches[original.Operation]=watch;
                return Observe(watch,out _)?Status.Published:Status.Waiting;
            }
        }
        catch{return Status.Unknown;}
    }
    private static void Broadcast(RebirthRemoteResourceBlockActorScope.Scope scope)
    {
        // Same native package fields as Setup; retained original primary ID is
        // valid even while its actual persistent player is disconnected.
        var packet=NetPackageManager.GetPackage<NetPackageSetBlock>();packet.persistentPlayerId=scope.NativeUser;packet.blockChanges=scope.Changes;
        packet.localPlayerThatChanged=GameManager.IsDedicatedServer?-1:GameManager.Instance.myPlayerId;
        int except=-1;var actor=scope.World.GetEntity(scope.Accepted.NativeEntity) as EntityPlayer;
        if(actor!=null&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.CanonicalId==scope.Original.Owner&&
            RebirthOriginalActionScope.Matches(actor,owner.StorageKey,scope.Original.Creation))except=actor.entityId;
        else if(scope.World.Players?.list!=null)foreach(var candidate in scope.World.Players.list)
            if(candidate!=null&&RebirthStablePlayerIdentity.TryResolveServerEntity(candidate,out var identity)&&identity.CanonicalId==scope.Original.Owner&&
                RebirthOriginalActionScope.Matches(candidate,identity.StorageKey,scope.Original.Creation)){except=candidate.entityId;break;}
        // The owner receives its correlated purpose-scoped projection instead
        // of an old-user native echo resolving a newer-life SpawnPoints actor.
        GameManager.Instance.SetBlocksOnClients(except,packet);
    }
    private static bool Observe(Watch watch,out string proof)
    {
        proof=null;
        if(watch.ProviderCurrent?.Invoke()!=true||watch.Directory!=GameIO.GetSaveGameDir())return false;
        if(watch.Request!=null)
        {
            if(RebirthStationSnapshotEvidence.HasPublished(watch.Request)&&RebirthStationSnapshotEvidence.TryCopy(watch.Request,out var bytes)&&
                RebirthOriginalNativeBlockSnapshot.TryProve(bytes,watch.Chunk.X,watch.Chunk.Z,Position(watch.Original),watch.After.rawData,(ushort)watch.After.damage,
                    Key(watch.Original.Operation),watch.Stamp,out proof,watch.Density,RebirthOriginalNativeBlockIdentity.NativeKey(Position(watch.Original)),watch.BirthImage,watch.Command.ClearTexture))
            {
                RebirthRemoteResourceBlockFootprintPublication.Observe(watch.World,watch.Original,watch.Command,watch.Chunk,watch.Request);
                if(!RebirthRemoteResourceBlockFootprintPublication.TryReadSaved(watch.World,watch.Original,watch.Command,out var childProof))return false;
                var body=new XElement("blockHostSaved",new XAttribute("version",2),new XAttribute("original",watch.Original.Digest),new XAttribute("command",watch.Command.Digest),new XAttribute("regionProof",proof),new XAttribute("childProof",childProof),
                    new XElement("actualEffect",Convert.ToBase64String(watch.Stamp)),new XElement("birthImage",Convert.ToBase64String(watch.BirthImage)));
                var prior=Journal("RemoteBlockHostSaved").Read(watch.Original.Operation);
                if(prior!=null)
                {
                    if(prior.Vehicle!=watch.Original.Operation||prior.OwnerCreation!=watch.Original.Creation||prior.Revision!=0||!prior.Before.SequenceEqual(prior.After)||
                        !ValidSavedBody(watch.World,watch.Original,watch.Root,watch.Command,prior.Before,out proof,out var oldStamp,out var oldBirth)||
                        !oldStamp.SequenceEqual(watch.Stamp)||!oldBirth.SequenceEqual(watch.BirthImage))return false;
                    // A fresh positive SAME-buffer readback can finish an
                    // interrupted publication of the first positive event;
                    // unrelated chunk work must not rewrite its original hash.
                    body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(prior.Before),16384);
                }
                if(!Publish("RemoteBlockHostSaved",watch.Original,body))return false;
                watch.Request.Dispose();watch.Request=null;watches.Remove(watch.Original.Operation);return true;
            }
            if(UnityEngine.Time.realtimeSinceStartup-watch.Started<15f)return false;watch.Request.Dispose();watch.Request=null;
        }
        var position=Position(watch.Original);var actual=watch.World.GetBlock(position);
        if(actual.rawData!=watch.After.rawData||actual.damage!=watch.After.damage||watch.World.GetDensity(position)!=watch.Density||watch.Command.ClearTexture&&watch.World.ChunkCache.GetTextureFull(position)!=0L||
            !watch.Chunk.ChunkCustomData.dict.TryGetValue(Key(watch.Original.Operation),out var stamp)||stamp?.data==null||!stamp.data.SequenceEqual(watch.Stamp)||stamp.expiresInWorldTime!=ulong.MaxValue||stamp.isSavedToNetwork||stamp.TriggerWriteDataDelegate!=null||
            !(RebirthOriginalNativeBlockIdentity.CopyMarker(watch.World,position)??Array.Empty<byte>()).SequenceEqual(watch.BirthImage))return false;
        watch.Request=RebirthStationSnapshotEvidence.Watch(watch.World,watch.Chunk,watch.Chunk.X,watch.Chunk.Z,watch.ProviderCurrent);if(watch.Request==null)return false;
        watch.Started=UnityEngine.Time.realtimeSinceStartup;
        lock(watch.Chunk)
        {if(!Ready(watch.World,position,out _,out var provider,out _)){watch.Request.Dispose();watch.Request=null;return false;}watch.Chunk.InProgressSaving=true;
            try{provider.m_RegionFileManager.SaveChunkSnapshot(watch.Chunk,true);}finally{watch.Chunk.InProgressSaving=false;}}
        return false;
    }
}
