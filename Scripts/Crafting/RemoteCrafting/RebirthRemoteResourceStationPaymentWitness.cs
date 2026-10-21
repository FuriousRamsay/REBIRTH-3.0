using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Read-only publication sink for the existing mobile native payment path.
// This is evidence, NOT fetch settlement authority. Legacy payment Frames have
// no original death binding; that qualification is explicit in every receipt.
internal static class RebirthRemoteResourceStationPaymentWitness
{
    private const int MaximumFrame=8*1024*1024,MaximumNative=64*1024*1024;
    private static string Root=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteStationNativePaymentWitnesses");
    internal static bool CanRetainExpected(RebirthCamperSavedVehicle.Expected expected)=>expected?.Bag!=null&&expected.Entity!=null&&
        expected.Bag.Length<=MaximumNative&&expected.Entity.Length<=ushort.MaxValue;
    private static string Hash(byte[] bytes)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    // The existing station frame admits up to 8 MiB of XML/native item strings.
    // Do not impose the smaller personal-fetch 6 MiB actor cap after payment.
    // Base64 of two 8 MiB frames is 21.34 MiB; plus 16 MiB actor text and
    // 1.34 MiB buff text plus bounded attributes remains below 40 MiB.
    private static string ActorImage(RebirthCamperEnergyActorImage actor)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){actor.Write(writer);writer.Flush();if(stream.Length>12*1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}}
    private static RebirthCamperEnergyActorImage ReadActor(string text)
    {
        if(text==null||text.Length>16*1024*1024)throw new InvalidDataException();
        using(var stream=new MemoryStream(Convert.FromBase64String(text),false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {var value=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length||ActorImage(value)!=text)throw new InvalidDataException();return value;}
    }
    private static XElement ReadFrame(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>MaximumFrame)throw new InvalidDataException();
        string text=new UTF8Encoding(false,true).GetString(bytes);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumFrame);
        int version=(int?)body.Attribute("version")??0;
        if(body.Name!="nativeActorPayment"||(version!=1&&version!=2)||body.Attributes().Count()!=(version==1?8:9)||body.Elements().Count()!=5||body.Nodes().Any(node=>node is not XElement)||
            version==2&&((int?)body.Attribute("deaths") is not int died||died<0)||body.ToString(SaveOptions.DisableFormatting)!=text||
            body.Elements().Any(node=>!new[]{"bag","belt","bagLocks","beltLocks","stationImage"}.Contains(node.Name.LocalName)||node.Name.NamespaceName.Length!=0)||body.Elements().GroupBy(node=>node.Name).Any(group=>group.Count()!=1))throw new InvalidDataException();
        foreach(var area in new[]{body.Element("bag"),body.Element("belt")})
        {
            int max=area.Name=="bag"?169:20;
            if(area.HasAttributes||area.Elements().Count()>max||area.Nodes().Any(node=>node is not XElement)||area.Elements().Any(slot=>slot.Name!="slot"||slot.Attributes().Count()!=2||slot.Nodes().Any()||
                (int?)slot.Attribute("count") is not int count||count<0||count>ushort.MaxValue||slot.Attribute("item")==null))throw new InvalidDataException();
        }
        foreach(string name in new[]{"bagLocks","beltLocks"})
        {var value=body.Element(name);if(value.Attributes().Count()!=1||value.Attribute("null")==null||value.Elements().Any()||((bool)value.Attribute("null")?value.Value.Length!=0:value.Value.Any(c=>c!='0'&&c!='1')))throw new InvalidDataException();}
        var image=body.Element("stationImage");if(image.HasAttributes||image.Elements().Any())throw new InvalidDataException();
        byte[] station=Convert.FromBase64String(image.Value);if(station.Length>MaximumFrame||Convert.ToBase64String(station)!=image.Value)throw new InvalidDataException();
        return body;
    }
    private static bool MatchesArea(XElement area,ItemStack[] actual)
    {
        if(actual==null)return false;var rows=area.Elements("slot").ToArray();
        return rows.Length==actual.Length&&rows.Select((row,index)=>actual[index]!=null&&(int)row.Attribute("count")==actual[index].count&&
            (string)row.Attribute("item")==RebirthNativeItemCodec.Encode(actual[index].itemValue)).All(value=>value);
    }
    private static bool HasPayment(PlayerDataFile saved,Guid operation)=>RebirthCookingRemoteAdmissionBackend.NativePhase(saved,"rbCamperNativePayment_"+operation.ToString("N"),out float phase,out bool present)&&present&&phase==1f;
    private static bool HasRetainedPayment(string encoded,Guid operation)
    {
        byte[] bytes=Convert.FromBase64String(encoded);if(bytes.Length>1024*1024||Convert.ToBase64String(bytes)!=encoded)return false;
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            int variables=reader.ReadUInt16();bool found=false;string wanted="rbCamperNativePayment_"+operation.ToString("N");
            for(int i=0;i<variables;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key==wanted){if(found||value!=1f)return false;found=true;}}
            return found&&stream.Position==stream.Length;
        }
    }
    private static bool PutNative(byte[] bytes,out string digest)
    {
        digest=null;if(bytes==null||bytes.Length>MaximumNative)return false;string hash=Hash(bytes),directory=Path.Combine(Root,"native");
        Directory.CreateDirectory(directory);if((File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
        string path=Path.Combine(directory,hash+".native");
        if(Directory.EnumerateFileSystemEntries(directory,hash+"*").Any(candidate=>candidate!=path))return false;
        if(File.Exists(path))
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length!=bytes.Length)return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
                if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=hash)return false;
        }
        else
        {
            string candidate=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.WriteThrough))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(candidate,path);
        }
        digest=hash;return true;
    }
    internal static bool PrepareExpected(RebirthCamperTransferJournal.Record original,RebirthCamperSavedVehicle.Expected expected)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||original?.State!=RebirthCamperTransferJournal.Phase.Prepared||!CanRetainExpected(expected)||
                expected.World?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,expected.World))return false;
            var vehicle=expected.World.GetEntity(expected.Id) as EntityVehicle;var before=ReadFrame(original.Before);var after=ReadFrame(original.After);
            if(vehicle==null||(string)before.Attribute("world")!=expected.World.worldState.Guid||(string)after.Attribute("world")!=expected.World.worldState.Guid)return false;
            Guid installation=Guid.ParseExact((string)before.Attribute("station"),"N");
            var runtime=RebirthCamperModuleRuntime.For(vehicle);var definition=runtime.Stations.Find(installation);
            if(runtime.Stations.VehicleIdentity!=original.Vehicle||definition==null||definition.State.BoundVehicle!=vehicle||!RebirthCamperStationSnapshot.Capture(installation,definition.State).SequenceEqual(Convert.FromBase64String(before.Element("stationImage").Value)))return false;
            string installed=RebirthNativeItemCodec.Encode(definition.InstalledItem.itemValue);
            if(installed.Length>262144||!RebirthNativeItemCodec.TryDecode(installed,out _))return false;
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,expected.World)&&ReferenceEquals(expected.World.GetEntity(expected.Id),vehicle)&&GameIO.GetSaveGameDir()==expected.SaveDirectory;
            if(!RebirthCamperSavedVehicle.Verify(vehicle,expected,Current)||!PutNative(expected.Bag,out var bag)||!PutNative(expected.Entity,out var entity))return false;
            byte[] bytes=Encoding.UTF8.GetBytes(new XElement("nativePaymentPreflight",new XAttribute("version",2),new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("creation",original.OwnerCreation),new XAttribute("world",expected.World.worldState.Guid),new XAttribute("vehicleId",expected.Id),new XAttribute("vehicleClass",expected.Class),
                new XAttribute("nativeBag",bag),new XAttribute("nativeEntity",entity),new XAttribute("before",Hash(original.Before)),new XAttribute("after",Hash(original.After)),
                new XAttribute("vehicleIdentity",runtime.Stations.VehicleIdentity.ToString("N")),new XAttribute("installation",installation.ToString("N")),new XElement("definition",installed)).ToString(SaveOptions.DisableFormatting));
            string directory=Path.Combine(Root,"preflight");Directory.CreateDirectory(directory);if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            string prefix=original.Operation.ToString("N")+".";
            if(Directory.EnumerateFileSystemEntries(directory,original.Operation.ToString("N")+"*").Any(path=>!new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj"}.Contains(Path.GetFileName(path))||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0))return false;
            var journal=new RebirthCamperTransferJournal(directory);var record=journal.Prepare(original.Operation,original.Vehicle,original.OwnerCreation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||!Current())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&Current();
        }
        catch{return false;}
    }
    internal static bool TryReadSavedBefore(World world,Guid operation,string address,string ownerKey,string creation,byte[] beforeBytes,byte[] afterBytes,out string proof)
        =>TryReadSavedBefore(world,operation,address,ownerKey,creation,beforeBytes,afterBytes,out proof,out _);
    internal static bool TryReadSavedBefore(World world,Guid operation,string address,string ownerKey,string creation,byte[] beforeBytes,byte[] afterBytes,out string proof,out string installedDefinition)
    {
        proof=installedDefinition=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||operation==Guid.Empty||
                !RebirthCamperStationAddress.TryParse(address,out var parsed))return false;
            var before=ReadFrame(beforeBytes);var after=ReadFrame(afterBytes);
            foreach(var frame in new[]{before,after})if((int?)frame.Attribute("version")!=2||(string)frame.Attribute("world")!=world.worldState.Guid||
                (string)frame.Attribute("station")!=parsed.Installation.ToString("N")||(string)frame.Attribute("ownerKey")!=ownerKey||(int?)frame.Attribute("deaths") is not int deaths||deaths<0)return false;
            if((int)before.Attribute("deaths")!=(int)after.Attribute("deaths")||(string)before.Attribute("owner")!=(string)after.Attribute("owner"))return false;
            string directory=Path.Combine(Root,"preflight");if(!Directory.Exists(directory)||(File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            var record=new RebirthCamperTransferJournal(directory).Read(operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=parsed.Vehicle||record.OwnerCreation!=creation||record.Revision!=0||record.Before.Length>512*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),512*1024);
            if(!ReadNative((string)body.Attribute("nativeBag"),MaximumNative,out var bag)||!ReadNative((string)body.Attribute("nativeEntity"),ushort.MaxValue,out _))return false;
            var expected=new RebirthCamperSavedVehicle.Expected{Id=(int)body.Attribute("vehicleId"),Class=(int)body.Attribute("vehicleClass"),World=world,Bag=bag};
            var original=new RebirthCamperTransferJournal.Record(operation,parsed.Vehicle,creation,0,RebirthCamperTransferJournal.Phase.Prepared,beforeBytes,afterBytes);
            if(!ReadPreflight(original,expected,out var entity,out var definition)||entity!=(string)body.Attribute("nativeEntity")||!RebirthNativeItemCodec.TryDecode(definition,out var installed))return false;
            var oldState=RebirthCamperStationSnapshot.Restore(parsed.Installation,new ItemStack(installed,1),Convert.FromBase64String(before.Element("stationImage").Value));
            var nextState=RebirthCamperStationSnapshot.Restore(parsed.Installation,new ItemStack(installed,1),Convert.FromBase64String(after.Element("stationImage").Value));
            int slot=(int)before.Attribute("queueSlot");if(slot!=(int)after.Attribute("queueSlot")||slot<0||slot>=oldState.Queue.Length||slot>=nextState.Queue.Length)return false;
            proof=Hash(record.Before);installedDefinition=definition;return true;
        }
        catch{proof=installedDefinition=null;return false;}
    }
    private static bool ReadPreflight(RebirthCamperTransferJournal.Record original,RebirthCamperSavedVehicle.Expected expected,out string beforeEntity,out string installed)
    {
        beforeEntity=installed=null;string directory=Path.Combine(Root,"preflight");if(!Directory.Exists(directory))return false;
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        string prefix=original.Operation.ToString("N")+".";
        if(Directory.EnumerateFileSystemEntries(directory,original.Operation.ToString("N")+"*").Any(path=>!new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj"}.Contains(Path.GetFileName(path))||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0))return false;
        var record=new RebirthCamperTransferJournal(directory).Read(original.Operation);
        if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=original.Vehicle||record.OwnerCreation!=original.OwnerCreation||record.Revision!=0||record.Before.Length>512*1024||!record.Before.SequenceEqual(record.After))return false;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),512*1024);
        // Earlier evidence lacks the original installation definition. It is
        // retained, never upgraded from a later current installation.
        if(body.Name!="nativePaymentPreflight"||body.Attributes().Count()!=12||body.Elements().Count()!=1||body.Nodes().Any(node=>node is not XElement)||body.Element("definition")==null||(int?)body.Attribute("version")!=2||
            (string)body.Attribute("operation")!=original.Operation.ToString("N")||(string)body.Attribute("creation")!=original.OwnerCreation||
            (string)body.Attribute("world")!=expected.World.worldState.Guid||(int?)body.Attribute("vehicleId")!=expected.Id||(int?)body.Attribute("vehicleClass")!=expected.Class||
            (string)body.Attribute("nativeBag")!=Hash(expected.Bag)||(string)body.Attribute("before")!=Hash(original.Before)||(string)body.Attribute("after")!=Hash(original.After)||
            (string)body.Attribute("installation")!=(string)ReadFrame(original.Before).Attribute("station")||
            !Guid.TryParseExact((string)body.Attribute("vehicleIdentity"),"N",out var vehicleIdentity)||vehicleIdentity!=original.Vehicle||vehicleIdentity.ToString("N")!=(string)body.Attribute("vehicleIdentity"))return false;
        var definition=body.Element("definition");installed=definition.Value;
        if(definition.HasAttributes||definition.Nodes().Any(node=>node is not XText)||installed.Length>262144||!RebirthNativeItemCodec.TryDecode(installed,out _))return false;
        string digest=(string)body.Attribute("nativeEntity");if(digest?.Length!=64||digest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
        string path=Path.Combine(Root,"native",digest+".native");if(!File.Exists(path)||(File.GetAttributes(Path.GetDirectoryName(path))&FileAttributes.ReparsePoint)!=0||
            Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(path),digest+"*").Any(candidate=>candidate!=path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>ushort.MaxValue)return false;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=digest)return false;
        beforeEntity=digest;return true;
    }
    internal static bool ObserveSavedNativePayment(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperTransferJournal.Record original,
        string address,string creation,RebirthCamperSavedVehicle.Expected expected)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||original==null||
                original.State!=RebirthCamperTransferJournal.Phase.Uncertain||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||original.OwnerCreation!=creation||!RebirthCamperStationAddress.TryParse(address,out var parsed)||
                station==null||station.InstallationId!=parsed.Installation||station.BoundVehicle==null||expected==null||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthStationLiveAccess.TryGetMobileLease(actor,parsed,creation,out var live,out var lease)||!ReferenceEquals(live,station))return false;
            var before=ReadFrame(original.Before);var after=ReadFrame(original.After);
            bool lifeKnown=(int)before.Attribute("version")==2&&(int)after.Attribute("version")==2;
            if(lifeKnown&&((int)before.Attribute("deaths")!=actor.Died||(int)after.Attribute("deaths")!=actor.Died))return false;
            bool preflight=ReadPreflight(original,expected,out var beforeEntity,out var installed);if(lifeKnown&&!preflight)return false;
            var installation=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(parsed.Installation);
            if(preflight&&(installation==null||!ReferenceEquals(installation.State,station)||RebirthNativeItemCodec.Encode(installation.InstalledItem.itemValue)!=installed))return false;
            string preflightDirectory=Path.Combine(Root,"preflight");
            if(!preflight&&Directory.Exists(preflightDirectory)&&Directory.EnumerateFileSystemEntries(preflightDirectory,original.Operation.ToString("N")+"*").Any())return false;
            foreach(var frame in new[]{before,after})
                if((string)frame.Attribute("station")!=parsed.Installation.ToString("N")||(string)frame.Attribute("owner")!=owner.CanonicalId||
                    (string)frame.Attribute("ownerKey")!=owner.StorageKey||(string)frame.Attribute("world")!=actor.world.worldState.Guid)return false;
            if((int)before.Attribute("queueSlot")!=(int)after.Attribute("queueSlot")||(int)after.Attribute("queueSlot")<0||(int)after.Attribute("queueSlot")>=station.Queue.Length)return false;
            byte[] stationAfter=Convert.FromBase64String(after.Element("stationImage").Value);
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,actor.world)&&!actor.IsDead()&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
                creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&RebirthStationLiveAccess.TryGetMobileLease(actor,parsed,creation,out var current,out var currentLease)&&
                ReferenceEquals(current,station)&&ReferenceEquals(currentLease,lease)&&RebirthCamperStationSnapshot.Capture(station.InstallationId,station).SequenceEqual(stationAfter);
            if(!Current()||!RebirthCamperEnergySavedOwner.TryRead(owner,out var savedActor,out var saved)||saved.id!=actor.entityId||saved.deaths!=actor.Died||
                !MatchesArea(after.Element("bag"),savedActor.Bag)||!MatchesArea(after.Element("belt"),savedActor.Belt)||!HasPayment(saved,original.Operation)||
                !RebirthCamperSavedVehicle.Verify(station.BoundVehicle,expected,Current))return false;
            if(!PutNative(expected.Bag,out var bag)||!PutNative(expected.Entity,out var entity))return false;
            string actorImage=ActorImage(savedActor);
            var body=new XElement("savedMobileNativePayment",new XAttribute("version",2),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("address",address),
                new XAttribute("creation",creation),new XAttribute("world",actor.world.worldState.Guid),new XAttribute("observedDeaths",actor.Died),new XAttribute("originalDeathsKnown",lifeKnown),
                new XAttribute("preflightKnown",preflight),new XAttribute("nativeBeforeEntity",beforeEntity??""),
                new XAttribute("vehicleId",expected.Id),new XAttribute("vehicleClass",expected.Class),new XAttribute("nativeBag",bag),new XAttribute("nativeEntity",entity),
                new XElement("before",Convert.ToBase64String(original.Before)),new XElement("after",Convert.ToBase64String(original.After)),new XElement("savedActor",actorImage),
                new XElement("savedBuffs",Convert.ToBase64String(saved.buffData.ToArray())),new XElement("definition",installed??string.Empty));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>40*1024*1024||!Current())return false;
            string directory=Path.Combine(Root,"originals");Directory.CreateDirectory(directory);if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            string prefix=original.Operation.ToString("N")+".";
            if(Directory.EnumerateFileSystemEntries(directory,original.Operation.ToString("N")+"*").Any(path=>
                !new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj"}.Contains(Path.GetFileName(path))||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0))return false;
            var journal=new RebirthCamperTransferJournal(directory);var record=journal.Read(original.Operation);
            // Unknown interrupted generations are evidence, never absence.
            if(record==null&&Directory.EnumerateFileSystemEntries(directory,original.Operation.ToString("N")+"*").Any())return false;
            if(record!=null)
            {
                if(record.Vehicle!=original.Vehicle||record.OwnerCreation!=creation||!record.Before.SequenceEqual(record.After)||record.Before.Length>40*1024*1024)return false;
                var retained=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),40*1024*1024);
                if(retained.Name!="savedMobileNativePayment"||retained.Attributes().Count()!=13||retained.Elements().Count()!=5||
                    retained.Attributes().Any(attribute=>body.Attribute(attribute.Name)==null||attribute.Value!=body.Attribute(attribute.Name).Value)||
                    retained.Element("before")?.Value!=body.Element("before").Value||retained.Element("after")?.Value!=body.Element("after").Value||retained.Element("definition")?.Value!=installed||
                    !HasRetainedPayment(retained.Element("savedBuffs")?.Value,original.Operation)||
                    ActorImage(ReadActor(retained.Element("savedActor")?.Value))!=retained.Element("savedActor").Value)return false;
                // The original observation is immutable. Unrelated cursor or
                // lock changes on a later saved owner upload do not rewrite it.
                bytes=record.Before;
            }
            record=journal.Prepare(original.Operation,original.Vehicle,creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||!Current())return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            return record.State==RebirthCamperTransferJournal.Phase.Applied&&Current();
        }
        catch{return false;}
    }

    // Detached historical observation only. Callers must separately bind an
    // original fetch request/payment and the exact changed native queue row;
    // this DTO never grants owner setters, source payment or queue mutation.
    internal sealed class SavedPayment
    {
        internal readonly Guid Operation;internal readonly string Address,Creation,OwnerKey,Proof;
        internal readonly int OriginalDeaths,QueueSlot;
        private readonly string definition,beforeFrame,afterActor;private readonly byte[] beforeStation,afterStation;
        internal string InstalledDefinition=>definition;
        internal SavedPayment(Guid operation,string address,string creation,string ownerKey,string proof,int deaths,int slot,string installed,
            string priorFrame,string actor,byte[] priorStation,byte[] nextStation)
        {Operation=operation;Address=address;Creation=creation;OwnerKey=ownerKey;Proof=proof;OriginalDeaths=deaths;QueueSlot=slot;definition=installed;
            beforeFrame=priorFrame;afterActor=actor;beforeStation=(byte[])priorStation.Clone();afterStation=(byte[])nextStation.Clone();}
        internal RebirthCamperStationState RestoreBefore()
            =>Restore(definition,Address,beforeStation);
        internal RebirthCamperStationState RestoreAfter()
            =>Restore(definition,Address,afterStation);
        // The native payment Frame records bag/belt, not a historical cursor.
        // Never manufacture a full Before actor using a later saved cursor.
        internal XElement CopyBeforeFrame()=>ReadFrame(Encoding.UTF8.GetBytes(beforeFrame));
        internal RebirthCamperEnergyActorImage RestoreAfterActor()=>ReadActor(afterActor);
        private static RebirthCamperStationState Restore(string item,string address,byte[] snapshot)
        {
            if(!RebirthCamperStationAddress.TryParse(address,out var parsed)||!RebirthNativeItemCodec.TryDecode(item,out var value))throw new InvalidDataException();
            return RebirthCamperStationSnapshot.Restore(parsed.Installation,new ItemStack(value,1),(byte[])snapshot.Clone());
        }
    }
    private static bool ReadNative(string digest,int maximum,out byte[] bytes)
    {
        bytes=null;if(digest?.Length!=64||digest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
        string directory=Path.Combine(Root,"native"),path=Path.Combine(directory,digest+".native");
        if(!Directory.Exists(directory)||(File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||
            !File.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory,digest+"*").Any(candidate=>candidate!=path))return false;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<0||stream.Length>maximum)return false;bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<=0)return false;offset+=read;}
            if(stream.ReadByte()!=-1||Hash(bytes)!=digest){bytes=null;return false;}
        }
        return true;
    }
    internal static bool TryReadSavedPayment(World world,Guid operation,string address,string ownerKey,string creation,out SavedPayment publication)
    {
        publication=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||operation==Guid.Empty||
                !RebirthCamperStationAddress.TryParse(address,out var parsed)||string.IsNullOrEmpty(ownerKey)||string.IsNullOrEmpty(creation))return false;
            string directory=Path.Combine(Root,"originals");if(!Directory.Exists(directory)||(File.GetAttributes(Root)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            string prefix=operation.ToString("N")+".";
            if(Directory.EnumerateFileSystemEntries(directory,operation.ToString("N")+"*").Any(path=>!new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj"}.Contains(Path.GetFileName(path))||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0))return false;
            var record=new RebirthCamperTransferJournal(directory).Read(operation);
            if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=parsed.Vehicle||record.OwnerCreation!=creation||record.Revision!=0||record.Before.Length>40*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,40*1024*1024);
            var names=new[]{"version","operation","address","creation","world","observedDeaths","originalDeathsKnown","preflightKnown","nativeBeforeEntity","vehicleId","vehicleClass","nativeBag","nativeEntity"};
            var elements=new[]{"before","after","savedActor","savedBuffs","definition"};
            if(body.Name!="savedMobileNativePayment"||body.Attributes().Count()!=names.Length||names.Any(name=>body.Attribute(name)==null)||body.Elements().Count()!=elements.Length||
                elements.Any(name=>body.Element(name)==null)||body.Nodes().Any(node=>node is not XElement)||body.Elements().Any(node=>node.HasAttributes||node.Nodes().Any(child=>child is not XText))||
                (int?)body.Attribute("version")!=2||(string)body.Attribute("operation")!=operation.ToString("N")||(string)body.Attribute("address")!=address||(string)body.Attribute("creation")!=creation||
                (string)body.Attribute("world")!=world.worldState.Guid||(bool?)body.Attribute("originalDeathsKnown")!=true||(bool?)body.Attribute("preflightKnown")!=true)return false;
            var canonical=new XElement("savedMobileNativePayment",names.Select(name=>new XAttribute(name,(string)body.Attribute(name))),elements.Select(name=>new XElement(name,body.Element(name).Value)));
            if(canonical.ToString(SaveOptions.DisableFormatting)!=text)return false;
            byte[] beforeBytes=Convert.FromBase64String(body.Element("before").Value),afterBytes=Convert.FromBase64String(body.Element("after").Value);
            if(Convert.ToBase64String(beforeBytes)!=body.Element("before").Value||Convert.ToBase64String(afterBytes)!=body.Element("after").Value)return false;
            var before=ReadFrame(beforeBytes);var after=ReadFrame(afterBytes);int deaths=(int)body.Attribute("observedDeaths"),slot=(int)after.Attribute("queueSlot");
            foreach(var frame in new[]{before,after})if((int)frame.Attribute("version")!=2||(int)frame.Attribute("deaths")!=deaths||(string)frame.Attribute("ownerKey")!=ownerKey||
                (string)frame.Attribute("station")!=parsed.Installation.ToString("N")||(string)frame.Attribute("world")!=world.worldState.Guid||(int)frame.Attribute("queueSlot")!=slot)return false;
            if(deaths<0||(string)before.Attribute("owner")!=(string)after.Attribute("owner")||!HasRetainedPayment(body.Element("savedBuffs").Value,operation)||
                !ReadNative((string)body.Attribute("nativeBag"),MaximumNative,out var bag)||!ReadNative((string)body.Attribute("nativeEntity"),ushort.MaxValue,out _))return false;
            var original=new RebirthCamperTransferJournal.Record(operation,parsed.Vehicle,creation,0,RebirthCamperTransferJournal.Phase.Uncertain,beforeBytes,afterBytes);
            var expected=new RebirthCamperSavedVehicle.Expected{Id=(int)body.Attribute("vehicleId"),Class=(int)body.Attribute("vehicleClass"),World=world,Bag=bag};
            if(!ReadPreflight(original,expected,out var priorEntity,out var installed)||priorEntity!=(string)body.Attribute("nativeBeforeEntity")||installed!=body.Element("definition").Value||
                !ReadNative(priorEntity,ushort.MaxValue,out _))return false;
            var actor=ReadActor(body.Element("savedActor").Value);
            if(!MatchesArea(after.Element("bag"),actor.Bag)||!MatchesArea(after.Element("belt"),actor.Belt))return false;
            var result=new SavedPayment(operation,address,creation,ownerKey,Hash(record.Before),deaths,slot,installed,before.ToString(SaveOptions.DisableFormatting),ActorImage(actor),
                Convert.FromBase64String(before.Element("stationImage").Value),Convert.FromBase64String(after.Element("stationImage").Value));
            var oldState=result.RestoreBefore();var newState=result.RestoreAfter();
            if(slot<0||slot>=oldState.Queue.Length||slot>=newState.Queue.Length||newState.Queue[slot]==null)return false;
            publication=result;return true;
        }
        catch{publication=null;return false;}
    }
}
