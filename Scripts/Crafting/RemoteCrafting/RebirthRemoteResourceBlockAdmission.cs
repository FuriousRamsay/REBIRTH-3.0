using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Platform;

// Host target/owner admission, before the source coordinator may pay anything.
// No native block command or owner inventory mutation occurs here.
internal static class RebirthRemoteResourceBlockAdmission
{
    internal static bool IsMappedRoot(RebirthRemoteResourceDurableJournal.Record root)
    {
        if(root==null)return false;
        try
        {
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockSourceMap");
            if(!Directory.Exists(directory))return false;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return true;
            return Directory.EnumerateFiles(directory,root.Operation.ToString("N")+".*",SearchOption.TopDirectoryOnly).Any();
        }
        catch{return true;}
    }
    internal static bool CanPublishPlannedOwnerAction(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,ItemStack[] paid)
    {
        try
        {
            var record=Journal("RemoteBlockSourceMap").Read(root.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.OwnerCreation!=root.Creation||record.Revision!=0||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(new UTF8Encoding(false,true).GetString(record.Before),out var original)||record.Vehicle!=original.Operation||
                !Current(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||root.Owner!=owner.StorageKey||root.Creation!=original.Creation||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out var sourceWorld)||root.World!=sourceWorld||
                root.Operation!=RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,root.Creation,original.Epoch,original.Request,original.Kind)||
                !HasSavedTarget(original)||!TryReadNativeUser(original,out _)||!TryReadTarget(original,out var birth,out _,out _))return false;
            return RebirthRemoteResourceBlockOriginal.Canonical(original,root.World,root.Operation,root.Binding,birth,paid,out var action)&&
                RebirthRemoteResourceBlockProtocol.CanRetainOwnerAction(original,action);
        }
        catch{return false;}
    }
    private static RebirthRemoteResourceClientOriginalJournal Journal(string name)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name));
    internal static bool TryReadMappedOriginal(Guid source,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            var record=Journal("RemoteBlockSourceMap").Read(source);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Operation!=source||record.Revision!=0||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(new UTF8Encoding(false,true).GetString(record.Before),out var value)||record.Vehicle!=value.Operation||record.OwnerCreation!=value.Creation||
                !TryReadOriginal(value.Operation,out var admitted)||admitted.Digest!=value.Digest)return false;
            original=value;return true;
        }
        catch{return false;}
    }
    internal static bool TryReadOriginal(Guid operation,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        try
        {
            var record=Journal("RemoteBlockPreSourceAdmission").Read(operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=operation||record.Revision!=0||record.Before.Length>24*1024*1024||
                !record.Before.SequenceEqual(record.After)||!RebirthRemoteResourceClientOriginal.TryDecodeOriginal(new UTF8Encoding(false,true).GetString(record.Before),out var value)||
                value.Operation!=operation||value.Creation!=record.OwnerCreation||!RebirthRemoteResourceBlockPlan.Validate(value))return false;
            original=value;return true;
        }
        catch{return false;}
    }
    private static string NativeUserImage(PlatformUserIdentifierAbs user)
    {
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);user.ToStream(writer);writer.Flush();if(stream.Length>1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}
    }
    internal static bool TryReadNativeUser(RebirthRemoteResourceClientOriginal.Intent original,out PlatformUserIdentifierAbs user)
    {
        user=null;
        try
        {
            var record=Journal("RemoteBlockNativeOwner").Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||record.Before.Length>4096||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,4096);string image=body.Element("nativeUser")?.Value;
            if(image==null||image.Length>1368)return false;byte[] bytes=Convert.FromBase64String(image);if(bytes.Length>1024||Convert.ToBase64String(bytes)!=image)return false;
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);user=PlatformUserIdentifierAbs.FromStream(reader);if(user==null||stream.Position!=stream.Length||NativeUserImage(user)!=image){user=null;return false;}}
            var canonical=new XElement("blockNativeOwner",new XAttribute("version",1),new XAttribute("original",original.Digest),
                new XAttribute("owner",RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)),new XElement("nativeUser",image));
            if(text!=canonical.ToString(SaveOptions.DisableFormatting)){user=null;return false;}return true;
        }
        catch{user=null;return false;}
    }
    private static bool Publish(RebirthRemoteResourceClientOriginalJournal journal,RebirthRemoteResourceClientOriginal.Intent original,byte[] bytes)
    {
        if(bytes.Length>24*1024*1024)return false;
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    internal static bool Current(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthRemoteResourceBlockPlan.Validate(original)||actor.Died!=original.Deaths||actor.world.worldState.Guid!=original.World||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.CanonicalId!=original.Owner||!RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation))return false;
            var action=original.Copy().Element("action");var position=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));
            return (actor.position-position.ToVector3()).sqrMagnitude<=(Constants.cDigAndBuildDistance+2f)*(Constants.cDigAndBuildDistance+2f)&&
                !(World.SandboxUseTraderArea==TraderAreaStates.Default&&actor.world.IsWithinTraderArea(position));
        }
        catch{return false;}
    }
    private static bool TargetBefore(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!Current(actor,original))return false;
        var action=original.Copy().Element("action");var block=actor.world.GetBlock(new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z")));
        return Convert.ToUInt64(block.rawData)==(ulong)action.Attribute("blockRaw")&&block.damage==(int)action.Attribute("blockDamage");
    }
    internal static bool TryReadTarget(RebirthRemoteResourceClientOriginal.Intent original,out string birth,out sbyte density,out bool painted)
    {
        birth=null;density=0;painted=false;
        try
        {
            if(!RebirthRemoteResourceBlockPlan.Validate(original))return false;
            var record=Journal("RemoteBlockTargetAdmission").Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||
                record.Before.Length>4096||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,4096);
            if(body.Name!="blockTargetAdmission"||body.Attributes().Count()!=5||body.Nodes().Any()||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("original")!=original.Digest||(int)body.Attribute("density")<sbyte.MinValue||(int)body.Attribute("density")>sbyte.MaxValue||
                !Guid.TryParseExact((string)body.Attribute("birth"),"N",out var token)||token==Guid.Empty)return false;
            string known=token.ToString("N");sbyte value=(sbyte)(int)body.Attribute("density");bool paint=(bool)body.Attribute("painted");
            var canonical=new XElement("blockTargetAdmission",new XAttribute("original",original.Digest),new XAttribute("birth",known),new XAttribute("density",value),new XAttribute("painted",paint),new XAttribute("version",1));
            if(text!=canonical.ToString(SaveOptions.DisableFormatting))return false;
            birth=known;density=value;painted=paint;return true;
        }
        catch{birth=null;density=0;painted=false;return false;}
    }
    internal static bool ConfirmSavedTarget(RebirthRemoteResourceClientOriginal.Intent original,RebirthOriginalNativeBlockIdentity.Admission actual)
    {
        try
        {
            if(actual==null||actual.Purpose!=1||actual.Operation!=original.Operation||actual.Digest!=original.Digest||!actual.Saved()||actual.SavedProof?.Length!=64||
                !TryReadTarget(original,out string birth,out sbyte density,out bool painted)||actual.Id!=birth)return false;
            var body=new XElement("blockTargetSaved",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("birth",birth),
                new XAttribute("density",density),new XAttribute("painted",painted),new XAttribute("proof",actual.SavedProof));
            var journal=Journal("RemoteBlockTargetSaved");var prior=journal.Read(original.Operation);
            // A later same-original requery may publish a different whole chunk
            // after unrelated chunk work. Preserve the first positive proof.
            return prior!=null?CheckTargetReceipt(original,false)&&Publish(journal,original,prior.Before):Publish(journal,original,Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting)));
        }
        catch{return false;}
    }
    internal static bool HasSavedTarget(RebirthRemoteResourceClientOriginal.Intent original)=>CheckTargetReceipt(original,true);
    internal static bool TryReadTransitionContext(RebirthRemoteResourceClientOriginal.Intent original,out string context)
    {
        context=null;
        try
        {
            if(!TryReadTarget(original,out _,out var density,out var painted))return false;
            var record=Journal("RemoteBlockTransitionContext").Read(original.Operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||
                record.Before.Length>32768||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);
            if(!RebirthRemoteResourceBlockTransition.ValidateContext(original,density,painted,text))return false;
            context=text;return true;
        }
        catch{return false;}
    }
    private static bool CheckTargetReceipt(RebirthRemoteResourceClientOriginal.Intent original,bool requireApplied)
    {
        try
        {
            if(!TryReadTarget(original,out var birth,out var density,out var painted)||!TryReadTransitionContext(original,out _))return false;
            var record=Journal("RemoteBlockTargetSaved").Read(original.Operation);
            if(record==null||requireApplied&&record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||
                record.Before.Length>4096||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,4096);string proof=(string)body.Attribute("proof");
            if(proof?.Length!=64||proof.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
            var canonical=new XElement("blockTargetSaved",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("birth",birth),
                new XAttribute("density",density),new XAttribute("painted",painted),new XAttribute("proof",proof));
            return text==canonical.ToString(SaveOptions.DisableFormatting);
        }
        catch{return false;}
    }
    internal static bool TryBindTarget(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthOriginalNativeBlockIdentity.Admission admission)
    {
        admission=null;
        try
        {
            if(!TargetBefore(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;
            if(!RemoteResourceWorldIdentity.TryGet(actor.world,out var admissionWorld)||RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(
                RebirthRemoteResourceDurableJournal.Operation(admissionWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)))return false;
            if(!RebirthOriginalNativeBlockIdentity.RegisterBlockTerminalReader(RebirthRemoteResourceBlockSettlement.HasSavedTerminal))return false;
            RebirthOriginalNativeBlockIdentity.Install();
            var action=original.Copy().Element("action");var position=new Vector3i((int)action.Attribute("x"),(int)action.Attribute("y"),(int)action.Attribute("z"));
            var journal=Journal("RemoteBlockPreSourceAdmission");
            byte[] originalBytes=Encoding.UTF8.GetBytes(original.Copy().ToString(SaveOptions.DisableFormatting));
            var prior=journal.Read(original.Operation);
            if(prior==null)
            {
                // A replica or an incoming image cannot substitute for this
                // final primary native Before+phase10 from the same life.
                if(!RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                    !RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(actual)||
                    !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f)return false;
            }
            if(!Publish(journal,original,originalBytes))return false;
            var nativeId=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
            if(nativeId==null)return false;
            var nativeOwner=new XElement("blockNativeOwner",new XAttribute("version",1),new XAttribute("original",original.Digest),
                new XAttribute("owner",owner.StorageKey),new XElement("nativeUser",NativeUserImage(nativeId)));
            if(!Publish(Journal("RemoteBlockNativeOwner"),original,Encoding.UTF8.GetBytes(nativeOwner.ToString(SaveOptions.DisableFormatting))))return false;
            if(!RemoteResourceWorldIdentity.TryGet(actor.world,out string sourceWorld))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            var map=Journal("RemoteBlockSourceMap");var mapped=map.Prepare(source,original.Operation,original.Creation,0,originalBytes,originalBytes);
            if(!mapped.Before.SequenceEqual(originalBytes)||!mapped.After.SequenceEqual(originalBytes))return false;
            if(mapped.State==RebirthCamperTransferJournal.Phase.Prepared)mapped=map.Advance(source,mapped.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(mapped.State==RebirthCamperTransferJournal.Phase.Uncertain)mapped=map.Advance(source,mapped.State,RebirthCamperTransferJournal.Phase.Applied);
            if(mapped.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            var targets=Journal("RemoteBlockTargetAdmission");var targetRecord=targets.Read(original.Operation);
            string beforeBirth=null;sbyte beforeDensity=actor.world.GetDensity(position);bool beforePainted=actor.world.ChunkCache.GetTextureFull(position)!=0L;
            if(targetRecord!=null)
            {
                if(targetRecord.Vehicle!=original.Operation||targetRecord.OwnerCreation!=original.Creation||targetRecord.Revision!=0||!targetRecord.Before.SequenceEqual(targetRecord.After)||targetRecord.Before.Length>4096)return false;
                var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(targetRecord.Before),4096);
                if(body.Name!="blockTargetAdmission"||body.Attributes().Count()!=5||body.Nodes().Any()||(string)body.Attribute("original")!=original.Digest||
                    (int?)body.Attribute("version")!=1||(int)body.Attribute("density")<sbyte.MinValue||(int)body.Attribute("density")>sbyte.MaxValue||
                    (string)body.Attribute("density")!=((int)body.Attribute("density")).ToString(System.Globalization.CultureInfo.InvariantCulture)||
                    (string)body.Attribute("painted")!=((bool)body.Attribute("painted")?"true":"false"))return false;
                beforeBirth=(string)body.Attribute("birth");beforeDensity=(sbyte)(int)body.Attribute("density");beforePainted=(bool)body.Attribute("painted");
                if(!Guid.TryParseExact(beforeBirth,"N",out var token)||token==Guid.Empty||token.ToString("N")!=beforeBirth)return false;
                var canonical=new XElement("blockTargetAdmission",new XAttribute("original",original.Digest),new XAttribute("birth",beforeBirth),new XAttribute("density",(int)body.Attribute("density")),new XAttribute("painted",(bool)body.Attribute("painted")),new XAttribute("version",1));
                if(canonical.ToString(SaveOptions.DisableFormatting)!=new UTF8Encoding(false,true).GetString(targetRecord.Before)||!Publish(targets,original,targetRecord.Before))return false;
            }
            bool Auth()=>TargetBefore(actor,original)&&actor.world.GetDensity(position)==beforeDensity&&(actor.world.ChunkCache.GetTextureFull(position)!=0L)==beforePainted;
            if(!Auth())return false;
            if(beforeBirth==null&&RebirthOriginalNativeBlockIdentity.TryReadClaim(actor.world,position,1,original.Operation,original.Digest,out var retainedBirth,out _))beforeBirth=retainedBirth;
            admission=beforeBirth==null?RebirthOriginalNativeBlockIdentity.Begin(actor.world,position,1,original.Operation,original.Digest,Auth):
                RebirthOriginalNativeBlockIdentity.Rebind(actor.world,position,1,original.Operation,original.Digest,beforeBirth,Auth);
            if(admission==null)return false;
            if(targetRecord==null)
            {
                var body=new XElement("blockTargetAdmission",new XAttribute("original",original.Digest),new XAttribute("birth",admission.Id),
                    new XAttribute("density",beforeDensity),new XAttribute("painted",beforePainted),new XAttribute("version",1));
                if(!Publish(targets,original,Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting)))){admission.Dispose();admission=null;return false;}
            }
            var transitions=Journal("RemoteBlockTransitionContext");var priorContext=transitions.Read(original.Operation);
            if(priorContext==null)
            {
                if(!RebirthRemoteResourceBlockTransition.CaptureContext(actor.world,original,beforeDensity,beforePainted,out string context)||
                    !Publish(transitions,original,Encoding.UTF8.GetBytes(context))){admission.Dispose();admission=null;return false;}
            }
            if(!TryReadTransitionContext(original,out _)){admission.Dispose();admission=null;return false;}
            var originalBlock=new BlockValue((uint)(ulong)action.Attribute("blockRaw"));
            ushort originalDamage=(ushort)(int)action.Attribute("blockDamage");
            byte[] birthImage=RebirthOriginalNativeBlockIdentity.CopyMarker(actor.world,position);
            if(birthImage==null){admission.Dispose();admission=null;return false;}
            string birthKey=RebirthOriginalNativeBlockIdentity.NativeKey(position);
            admission.SnapshotProof=bytes=>RebirthOriginalNativeBlockSnapshot.TryProve(bytes,World.toChunkXZ(position.x),World.toChunkXZ(position.z),position,
                originalBlock.rawData,originalDamage,birthKey,birthImage,out _,beforeDensity,expectedPainted:beforePainted);
            return true; // Caller must wait on admission.Saved() before sources.
        }
        catch{admission?.Dispose();admission=null;return false;}
    }
}
