using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private sealed class NativeBodySourceSnapshot
    {internal string World,Owner,Creation,Digest,Actor,Gear,Native;internal Guid Episode;internal int Entity,Deaths;internal bool Owed;}
    private static string NativeBodySnapshotPath(string digest)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","native-body-source-snapshots",digest+".xml");
    private static string NativeBodySnapshotImage(NativeBodySourceSnapshot s)
    {var x=new XElement("placedNativeBodySourceSnapshot",new XAttribute("version",1),new XAttribute("world",s.World),new XAttribute("owner",s.Owner),new XAttribute("creation",s.Creation),new XAttribute("digest",s.Digest),new XAttribute("actor",s.Actor),new XAttribute("gear",s.Gear),new XAttribute("native",s.Native),new XAttribute("episode",s.Episode.ToString("N")),new XAttribute("entity",s.Entity),new XAttribute("deaths",s.Deaths),new XAttribute("owed",s.Owed));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool ReadNativeBodySnapshot(string digest,out NativeBodySourceSnapshot s)
    {
        s=null;try{string path=NativeBodySnapshotPath(digest);if(!FullHash(digest))return false;string candidate=path;if(!File.Exists(path)){if(File.Exists(path+".bak")||!Directory.Exists(Path.GetDirectoryName(path)))return false;var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=1)return false;candidate=pending[0];}if(new FileInfo(candidate).Length>4096)return false;string image=File.ReadAllText(candidate,new UTF8Encoding(false,true));var x=OwedXml(image);s=new NativeBodySourceSnapshot{World=(string)x.Attribute("world"),Owner=(string)x.Attribute("owner"),Creation=(string)x.Attribute("creation"),Digest=(string)x.Attribute("digest"),Actor=(string)x.Attribute("actor"),Gear=(string)x.Attribute("gear"),Native=(string)x.Attribute("native"),Episode=Guid.ParseExact((string)x.Attribute("episode"),"N"),Entity=(int)x.Attribute("entity"),Deaths=(int)x.Attribute("deaths"),Owed=(bool)x.Attribute("owed")};return s.Digest==digest&&s.Episode!=Guid.Empty&&s.Entity>0&&s.Deaths>0&&FullHash(s.Actor)&&FullHash(s.Gear)&&FullHash(s.Native)&&NativeBodySnapshotImage(s)==image&&(candidate==path||RebirthDurableFileCommit.TryPublish(candidate,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image);}catch{s=null;return false;}
    }
    private static bool ReadNativeBodyDigest(PlayerDataFile file,bool owed,out string digest)
    {
        digest=null;try{var bytes=file.buffData?.ToArray();if(bytes==null||bytes.Length>1024*1024)return false;string prefix=owed?OwedMarkerPrefix:Prefix;var values=new Dictionary<string,float>(StringComparer.Ordinal);using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);count=reader.ReadUInt16();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key.StartsWith(prefix,StringComparison.Ordinal)){if(values.ContainsKey(key)||float.IsNaN(value)||float.IsInfinity(value))return false;values.Add(key,value);}}if(stream.Position!=stream.Length)return false;}if(!values.TryGetValue(prefix+(owed?"Phase":"Stage"),out float phase)||phase!=(owed?5:3))return false;var text=new StringBuilder(64);for(int i=0;i<16;i++){if(!values.TryGetValue(prefix+i,out float word)||word<0||word>ushort.MaxValue||word!=(int)word)return false;text.Append(((ushort)word).ToString("x4"));}digest=text.ToString();return true;}catch{return false;}
    }
    // Preserve the first positive final native image while its large full trace
    // is still in transit. This observation has no item/source settlement power.
    internal static void ObserveNativeBodySourceSnapshot(EntityPlayer actor)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);TerminalFatalScope(world);if(!TryCurrentTerminalFatal(actor,TerminalFatalKey(owner.StorageKey,creation,actor.entityId),out var fatal)||!EnsureTerminalFatalArchive(fatal)||!RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var file)||file.id!=actor.entityId||fatal.Deaths==int.MaxValue||file.deaths!=fatal.Deaths+1)return;
            string native;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(file.id);writer.Write(file.deaths);foreach(var blob in new[]{file.bagData,file.inventoryData,file.equipmentData,file.buffData}){var bytes=blob.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();native=Hash(Convert.ToBase64String(stream.ToArray()));}
            foreach(bool owed in new[]{false,true})
            {
                if(!ReadNativeBodyDigest(file,owed,out var digest))continue;var s=new NativeBodySourceSnapshot{World=world.worldState.Guid,Owner=owner.StorageKey,Creation=creation,Digest=digest,Actor=Hash(RebirthPlacedOriginal.EncodeActor(actual)),Gear=Hash(Convert.ToBase64String(SavedEquipment(file))),Native=native,Episode=fatal.Episode,Entity=file.id,Deaths=file.deaths,Owed=owed};string path=NativeBodySnapshotPath(digest),image=NativeBodySnapshotImage(s),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak"))continue;Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=image)continue;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}RebirthDurableFileCommit.TryPublish(temp,path,out _);
            }
        }catch{/* Missing/ambiguous native lineage remains retained. */}
    }
    private static bool MatchNativeBodySnapshot(TerminalBody t,out NativeBodySourceSnapshot s)
    {return ReadNativeBodySnapshot(Hash(t.Trace),out s)&&s.World==t.World&&s.Owner==t.Owner&&s.Creation==t.Creation&&s.Entity==t.NativeOwner&&s.Deaths==t.Deaths&&s.Owed==t.Owed&&s.Episode==t.Episode&&s.Actor==Hash(t.Actor)&&s.Gear==Hash(t.Gear);}
    // Corroboration only. No placement/owed admission, grant, clear or native
    // primary save is performed at the fatal observation boundary.
    private sealed class TerminalFatalBoundary
    {internal EntityPlayer Actor;internal World World;internal string Directory,Owner,Creation;internal int Entity,Deaths;}
    private sealed class TerminalFatal
    {internal Guid Episode;internal string World,Owner,Creation,Policy;internal int Entity,Deaths;internal EntityPlayer Actor;internal World Reference;internal string Directory;}
    private static readonly Dictionary<string,TerminalFatal> terminalFatalMemory=new Dictionary<string,TerminalFatal>(StringComparer.Ordinal);
    private static World terminalFatalWorld;private static string terminalFatalDirectory;
    private static void TerminalFatalScope(World world)
    {if(ReferenceEquals(world,terminalFatalWorld)&&terminalFatalDirectory==GameIO.GetSaveGameDir())return;terminalFatalMemory.Clear();terminalFatalWorld=world;terminalFatalDirectory=GameIO.GetSaveGameDir();}
    private static string TerminalFatalKey(string owner,string creation,int entity)=>owner+"|"+creation+"|"+entity;
    private static string TerminalFatalPath(TerminalFatal f)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-fatal",f.Episode.ToString("N")+".xml");
    private static string TerminalFatalImage(TerminalFatal f)
    {var x=new XElement("placedTerminalNativeFatal",new XAttribute("version",1),new XAttribute("episode",f.Episode.ToString("N")),new XAttribute("world",f.World),new XAttribute("owner",f.Owner),new XAttribute("creation",f.Creation),new XAttribute("entity",f.Entity),new XAttribute("deaths",f.Deaths),new XAttribute("policy",f.Policy));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static string TerminalFatalCurrentPath(string key)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-fatal-current",Hash(key)+".xml");
    private static bool DecodeTerminalFatal(string image,out TerminalFatal f)
    {
        f=null;try{if(image==null||image.Length>4096)return false;var x=OwedXml(image);if(x.Name!="placedTerminalNativeFatal"||(int?)x.Attribute("version")!=1)return false;f=new TerminalFatal{Episode=Guid.ParseExact((string)x.Attribute("episode"),"N"),World=(string)x.Attribute("world"),Owner=(string)x.Attribute("owner"),Creation=(string)x.Attribute("creation"),Policy=(string)x.Attribute("policy"),Entity=(int)x.Attribute("entity"),Deaths=(int)x.Attribute("deaths")};return f.Episode!=Guid.Empty&&f.Entity>0&&f.Deaths>=0&&ValidPolicy(f.Policy)&&RebirthOriginalActionScope.Validate(f.Owner,f.Creation)&&TerminalFatalImage(f)==image;}catch{f=null;return false;}
    }
    private static bool SaveTerminalFatalCurrent(TerminalFatal f)
    {
        try{string key=TerminalFatalKey(f.Owner,f.Creation,f.Entity),path=TerminalFatalCurrentPath(key),image=TerminalFatalImage(f),folder=Path.GetDirectoryName(path);Directory.CreateDirectory(folder);if(File.Exists(path)||File.Exists(path+".bak")){if(!File.Exists(path)||new FileInfo(path).Length>4096||!DecodeTerminalFatal(File.ReadAllText(path,new UTF8Encoding(false,true)),out var old)||old.World!=f.World||old.Owner!=f.Owner||old.Creation!=f.Creation||old.Entity!=f.Entity)return false;}var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=image)return false;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;}catch{return false;}
    }
    private static bool EnsureTerminalFatalArchive(TerminalFatal f)
    {
        try{string path=TerminalFatalPath(f),image=TerminalFatalImage(f),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak"))return File.Exists(path)&&new FileInfo(path).Length<=4096&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>4096||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=image)return false;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;}catch{return false;}
    }    private static bool TryCurrentTerminalFatal(EntityPlayer actor,string key,out TerminalFatal f)
    {
        f=null;try{if(terminalFatalMemory.TryGetValue(key,out var live)&&ReferenceEquals(live.Actor,actor)&&ReferenceEquals(live.Reference,actor.world)&&live.Directory==GameIO.GetSaveGameDir()){f=live;return true;}string path=TerminalFatalCurrentPath(key);if(!File.Exists(path)||new FileInfo(path).Length>4096||!DecodeTerminalFatal(File.ReadAllText(path,new UTF8Encoding(false,true)),out var saved)||saved.World!=actor.world.worldState.Guid||saved.Entity!=actor.entityId||RebirthCamperCharacterAccess.CurrentCreation(actor)!=saved.Creation||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=saved.Owner)return false;saved.Actor=actor;saved.Reference=(World)actor.world;saved.Directory=GameIO.GetSaveGameDir();f=saved;return true;}catch{return false;}
    }    private sealed class TerminalBody
    {
        internal Guid Operation,Original,Episode;internal bool Owed;internal string Trace,Terminal,World,Owner,Creation,Native,Policy,Actor,Gear,Image,Digest;internal int Deaths,NativeOwner;internal byte[] Selected;internal BuilderInput[] Inputs;internal bool Overflow;
    }
    private static string TerminalBodyImage(TerminalBody t)=>new XElement("placedTerminalNativeBody",new XAttribute("version",1),new XAttribute("operation",t.Operation.ToString("N")),new XAttribute("episode",t.Episode.ToString("N")),new XAttribute("owed",t.Owed),new XAttribute("terminal",t.Terminal),new XElement("trace",t.Trace)).ToString(SaveOptions.DisableFormatting);
    private static TerminalBody DecodeTerminalBody(string image)
    {
        if(image==null||Encoding.UTF8.GetByteCount(image)>DeferredMaximum)throw new InvalidDataException();XElement x;using(var input=new StringReader(image))using(var reader=System.Xml.XmlReader.Create(input,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=DeferredMaximum}))x=XElement.Load(reader);if(x.Name!="placedTerminalNativeBody"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();
        var t=new TerminalBody{Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),Episode=Guid.ParseExact((string)x.Attribute("episode"),"N"),Owed=(bool)x.Attribute("owed"),Terminal=(string)x.Attribute("terminal"),Trace=(string)x.Element("trace"),Image=image};
        if(t.Owed){var old=DecodeOwedPhysical(t.Trace);t.Original=old.Child.Operation;t.World=old.Child.World;t.Owner=old.Child.Owner;t.Creation=old.Child.Creation;t.Deaths=checked(old.Child.Deaths+1);t.NativeOwner=old.Observation.NativeOwnerEntity;t.Policy=old.Observation.Policy;t.Native=old.Creation;t.Actor=old.Actor;t.Gear=old.Gear;t.Selected=old.Selected;t.Inputs=old.Inputs;t.Overflow=old.Overflow;}
        else{var old=DecodePhysical(t.Trace);t.Original=old.Observation.Original.Operation;t.World=old.Observation.Original.World;t.Owner=old.Observation.Original.Owner;t.Creation=old.Observation.Original.Creation;t.Deaths=checked(old.Observation.Original.Deaths+1);t.NativeOwner=old.Observation.NativeOwnerEntity;t.Policy=old.Observation.Policy;t.Native=old.Creation;t.Actor=old.FinalActor;t.Gear=old.FinalEquipment;t.Selected=old.Selected;t.Inputs=old.Inputs;t.Overflow=old.Overflow;}
        if(t.Operation==Guid.Empty||t.Episode==Guid.Empty||t.Operation==t.Original||t.Deaths<1||!FullHash(t.Terminal)||TerminalBodyImage(t)!=image)throw new InvalidDataException();t.Digest=Hash(image);return t;
    }
    private static string TerminalBodyMapPath(string trace)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-intents",Hash(trace)+".xml");
    private static bool ReadTerminalBody(string path,out TerminalBody t)
    {t=null;try{if(!File.Exists(path)||new FileInfo(path).Length>DeferredMaximum)return false;t=DecodeTerminalBody(File.ReadAllText(path,new UTF8Encoding(false,true)));return true;}catch{return false;}}
    private static bool TryHostTerminalMirror(EntityPlayer actor,bool owed,string trace,out string terminal)
    {
        terminal=null;try{if(owed){var body=DecodeOwedPhysical(trace);if(!TrySavedOwedTerminal(actor,body.Child,out var saved))return false;terminal=Hash(ChildBody(saved));return true;}var original=DecodePhysical(trace).Observation.Original;if(!RebirthPlacedOriginal.SamePartitionOwner(actor,original)||!RebirthPlacedOriginal.TryReadTerminal((World)actor.world,original.Operation,out var root)||!RebirthPlacedOriginal.SamePartitionOriginal(root,original))return false;terminal=Hash(RebirthPlacedOriginal.EncodeRecord(root));return true;}catch{return false;}
    }
    private static bool TryPrepareTerminalNativeBody(EntityPlayer actor,DeferredBody d,out TerminalBody t)
    {return TryPrepareTerminalNativeBody(actor,d.Owed,d.Owed?d.Trace:DecodeIntent(d.Trace).Image,out t);}    private static bool TryPrepareTerminalNativeBody(EntityPlayer actor,bool owed,string trace,out TerminalBody t)
    {
        t=null;try
        {
            if(!TryHostTerminalMirror(actor,owed,trace,out var terminal)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;string existingPath=TerminalBodyMapPath(trace);if(File.Exists(existingPath)||File.Exists(existingPath+".bak")){return ReadTerminalBody(existingPath,out t)&&t.Trace==trace&&t.Terminal==terminal&&TerminalOwner(actor,t)&&TerminalFatalLineage(t);}ObserveNativeBodySourceSnapshot(actor);if(!ReadNativeBodySnapshot(Hash(trace),out var snapshot))return false;string fatalPath=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","terminal-native-fatal",snapshot.Episode.ToString("N")+".xml");if(!File.Exists(fatalPath)||new FileInfo(fatalPath).Length>4096||!DecodeTerminalFatal(File.ReadAllText(fatalPath,new UTF8Encoding(false,true)),out var fatal)||fatal.Episode!=snapshot.Episode)return false;
            var candidate=new TerminalBody{Operation=Guid.NewGuid(),Episode=fatal.Episode,Owed=owed,Trace=trace,Terminal=terminal};candidate=DecodeTerminalBody(TerminalBodyImage(candidate));if(candidate.Policy!=fatal.Policy||fatal.Deaths!=candidate.Deaths-1||!MatchNativeBodySnapshot(candidate,out _))return false;
            string path=TerminalBodyMapPath(trace),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadTerminalBody(path,out t)||t.Trace!=trace||t.Terminal!=terminal)return false;return t.Episode==fatal.Episode;}
            Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();string temp;if(pending.Length!=0){if(pending.Length!=1||!ReadTerminalBody(pending[0],out var old)||old.Trace!=trace||old.Terminal!=terminal||old.Episode!=fatal.Episode)return false;candidate=old;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(candidate.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
            return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadTerminalBody(path,out t)&&t.Image==candidate.Image;
        }catch{return false;}
    }    internal static object BeforeTerminalNativeFatal(EntityPlayer actor)
    {
        try{var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor==null||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.bDead||actor.Died<0||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return null;TerminalFatalScope(world);return new TerminalFatalBoundary{Actor=actor,World=world,Directory=GameIO.GetSaveGameDir(),Owner=owner.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Entity=actor.entityId,Deaths=actor.Died};}catch{return null;}
    }
    internal static void AfterTerminalNativeFatal(EntityPlayer actor,object token)
    {
        try
        {
            if(token is not TerminalFatalBoundary before||!ReferenceEquals(before.Actor,actor)||!ReferenceEquals(before.World,GameManager.Instance?.World)||before.Directory!=GameIO.GetSaveGameDir()||!ReferenceEquals(before.World.GetEntity(before.Entity),actor)||!actor.bDead||!actor.IsDead()||actor.Health>0||actor.entityId!=before.Entity||actor.Died!=before.Deaths||RebirthCamperCharacterAccess.CurrentCreation(actor)!=before.Creation||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=before.Owner)return;
            TerminalFatalScope(before.World);foreach(var prior in terminalFatalMemory.Values.ToArray())if(!ReferenceEquals(before.World.GetEntity(prior.Entity),prior.Actor))terminalFatalMemory.Remove(TerminalFatalKey(prior.Owner,prior.Creation,prior.Entity));string key=TerminalFatalKey(before.Owner,before.Creation,before.Entity);var f=new TerminalFatal{Episode=Guid.NewGuid(),World=before.World.worldState.Guid,Owner=before.Owner,Creation=before.Creation,Entity=before.Entity,Deaths=before.Deaths,Policy=ServerPolicy(),Actor=actor,Reference=before.World,Directory=before.Directory};if(!SaveTerminalFatalCurrent(f))return;string image=TerminalFatalImage(f),path=TerminalFatalPath(f);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(RebirthDurableFileCommit.TryPublish(temp,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image){if(terminalFatalMemory.Count>=128&&!terminalFatalMemory.ContainsKey(key))return;terminalFatalMemory[key]=f;ObserveNativeBodySourceSnapshot(actor);}
        }catch{/* No corroboration authority is invented on observation failure. */}
    }
}
