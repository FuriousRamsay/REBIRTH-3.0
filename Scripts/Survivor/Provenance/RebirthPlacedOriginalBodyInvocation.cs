using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private static readonly Dictionary<Guid,PhysicalTrace> bodyIntentMemory=new Dictionary<Guid,PhysicalTrace>();private static World intentWorld;private static string intentDirectory;
    private static void IntentScope(World world){if(ReferenceEquals(intentWorld,world)&&intentDirectory==GameIO.GetSaveGameDir())return;bodyIntentMemory.Clear();intentWorld=world;intentDirectory=GameIO.GetSaveGameDir();}
    private static void RememberIntent(World world,PhysicalTrace trace)
    {IntentScope(world);foreach(var prior in bodyIntentMemory.Values.ToArray()){var actor=world.GetEntity(prior.Observation.NativeOwnerEntity) as EntityPlayer;if(!RebirthPlacedOriginal.ValidatePartition(actor,prior.Observation.Original,prior.Observation.Policy,prior.Observation.NativeOwnerEntity))bodyIntentMemory.Remove(prior.Observation.Original.Operation);}if(bodyIntentMemory.Count<128||bodyIntentMemory.ContainsKey(trace.Observation.Original.Operation))bodyIntentMemory[trace.Observation.Original.Operation]=trace;}
    private static bool TryBodyIntent(World world,RebirthPlacedOriginal.Record original,out PhysicalTrace trace)
    {IntentScope(world);if(ReadIntent(BodyIntentPath(original.Operation,true),out trace))return true;return bodyIntentMemory.TryGetValue(original.Operation,out trace)&&RebirthPlacedOriginal.SamePartitionOriginal(original,trace.Observation.Original);}
    private static string BodyIntentPath(Guid operation,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"body-intents-server":"body-intents-owner",operation.ToString("N")+".xml");
    private static string IntentBody(PhysicalTrace t){var x=new XElement("placedNativeBodyIntent",new XAttribute("version",1),new XElement("physical",t.Image));x.SetAttributeValue("checksum",Hash(t.Image));return x.ToString(SaveOptions.DisableFormatting);}
    private static PhysicalTrace DecodeIntent(string image)
    {var x=OwedXml(image);if(x.Name!="placedNativeBodyIntent"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();var t=DecodePhysical((string)x.Element("physical"));if(t.Overflow||t.Creation.Length==0||IntentBody(t)!=image)throw new InvalidDataException();return t;}
    private static bool ReadIntent(string path,out PhysicalTrace t)
    {t=null;try{if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;t=DecodeIntent(File.ReadAllText(path,new UTF8Encoding(false,true)));return true;}catch{return false;}}
    private static bool SaveIntent(PhysicalTrace t,bool server)
    {
        try
        {
            string path=BodyIntentPath(t.Observation.Original.Operation,server),image=IntentBody(t);DecodeIntent(image);if(File.Exists(path)||File.Exists(path+".bak"))return ReadIntent(path,out var old)&&old.Image==t.Image;Directory.CreateDirectory(Path.GetDirectoryName(path));var files=Directory.EnumerateFiles(Path.GetDirectoryName(path),t.Observation.Original.Operation.ToString("N")+".*.tmp").ToArray();
            if(files.Length!=0){if(files.Length!=1||!ReadIntent(files[0],out var prepared)||prepared.Image!=t.Image)return false;return RebirthDurableFileCommit.TryPublish(files[0],path,out _)&&ReadIntent(path,out var recovered)&&recovered.Image==t.Image;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return ReadIntent(temp,out var candidate)&&candidate.Image==t.Image&&RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadIntent(path,out var saved)&&saved.Image==t.Image;
        }catch{return false;}
    }
    private static PhysicalTrace FreezeRequestIntent(Physical p)
    {
        if(!PhysicalCurrent(p)||p.Projection==null||p.Adding||p.Overflow||p.Creation.Length==0||!p.Projection.DegradedEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(p.Context.Player.equipment)))throw new InvalidDataException();
        var t=new PhysicalTrace{Observation=p.Context.Observation,Selected=(byte[])p.Selected.Clone(),FinalActor=RebirthPlacedOriginal.EncodeActor(RebirthCamperEnergyActorImage.Capture(p.Context.Player)),FinalEquipment=Convert.ToBase64String(p.Projection.FinalEquipment),Creation=p.Creation,Inputs=p.Inputs.Select(input=>new BuilderInput{Item=input.Item.Clone(),Failed=input.Failed}).ToArray()};return DecodePhysical(PhysicalBody(t));
    }
    private static void PublishRequestIntent(Physical p)
    {
        if(p.Context.Owed!=null){PublishOwedPhysicalIntent(p);return;}
        var intent=FreezeRequestIntent(p);if(!SaveIntent(intent,false))throw new InvalidDataException("Original native body intent retained before request.");
        QueueDeferredBody(p.Context.Player,false,IntentBody(intent));
    }
    private static void RetryBodyIntent(EntityPlayerLocal player,Guid operation)
    {if(ReadIntent(BodyIntentPath(operation,false),out var t))QueueDeferredBody(player,false,IntentBody(t));}    private static void ReceiveBodyIntent(World world,int entity,string image)
    {
        try{var actor=world.GetEntity(entity) as EntityPlayer;var t=DecodeIntent(image);if(!RebirthPlacedOriginal.ValidatePartition(actor,t.Observation.Original,t.Observation.Policy,t.Observation.NativeOwnerEntity))return;RememberIntent(world,t);if(!Save(t.Observation,true)||!SaveIntent(t,true))return;if(RebirthPlacedOriginalBodyPublication.HasAccepted(world,t.Observation.Original,t.Creation))RecordBodyInvocation(t);}catch{}
    }
    private static string InvocationPath(PhysicalTrace t)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","body-invocations",t.Observation.Original.Operation.ToString("N")+".xml");
    private static string InvocationBody(PhysicalTrace t){var x=new XElement("placedNativeBodyInvocation",new XAttribute("version",1),new XAttribute("attempted",true),new XElement("intent",IntentBody(t)));x.SetAttributeValue("checksum",Hash(IntentBody(t)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool DecodeInvocation(string image,out PhysicalTrace t)
    {t=null;try{var x=OwedXml(image);if(x.Name!="placedNativeBodyInvocation"||(int?)x.Attribute("version")!=1||(bool?)x.Attribute("attempted")!=true)return false;t=DecodeIntent((string)x.Element("intent"));return InvocationBody(t)==image;}catch{return false;}}
    private static bool ReadInvocation(string path,out PhysicalTrace t)
    {t=null;try{return File.Exists(path)&&new FileInfo(path).Length<=Maximum&&DecodeInvocation(File.ReadAllText(path,new UTF8Encoding(false,true)),out t);}catch{return false;}}
    private static string KnownInvocationPath(PhysicalTrace t)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","body-invocation-index",Hash(t.Creation)+".xml");
    private static bool SaveInvocationAt(string path,PhysicalTrace t,bool index)
    {
        try
        {
            string image=InvocationBody(t);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadInvocation(path,out var old))return false;return index?old.Creation==t.Creation&&old.Observation.Original.World==t.Observation.Original.World&&old.Observation.Original.Owner==t.Observation.Original.Owner&&old.Observation.Original.Creation==t.Observation.Original.Creation:old.Image==t.Image;}
            Directory.CreateDirectory(Path.GetDirectoryName(path));var files=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(files.Length!=0){if(files.Length!=1||!ReadInvocation(files[0],out var pending)||InvocationBody(pending)!=image)return false;return RebirthDurableFileCommit.TryPublish(files[0],path,out _)&&ReadInvocation(path,out var recovered)&&InvocationBody(recovered)==image;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return ReadInvocation(temp,out var prepared)&&InvocationBody(prepared)==image&&RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadInvocation(path,out var saved)&&InvocationBody(saved)==image;
        }catch{return false;}
    }
    private static bool RecordBodyInvocation(PhysicalTrace t)=>SaveInvocationAt(InvocationPath(t),t,false)&&SaveInvocationAt(KnownInvocationPath(t),t,true);
    private static bool BeginBodyInvocation(PhysicalTrace t)
    {
        if(!HasDeferredPermit(false,t.Observation.Original.Operation,IntentBody(t)))return false;string path=InvocationPath(t);string folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak")||Directory.Exists(folder)&&Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").Any())return false;
        // A positively classified original gets exactly one native invocation.
        // Even a scheduling exception after this point remains attempted.
        return RecordBodyInvocation(t);
    }
    private static bool ValidStoredBodyFrame(World world,RebirthPlacedOriginal.Record original,string request)
    {return ReadIntent(BodyIntentPath(original.Operation,true),out var t)&&t.Creation==request&&RebirthPlacedOriginal.SamePartitionOriginal(original,t.Observation.Original)&&RebirthPlacedOriginal.ValidateStoredBodyOriginal(world,original,t.Observation.Policy,t.Observation.NativeOwnerEntity)&&ReadInvocation(InvocationPath(t),out var attempted)&&attempted.Image==t.Image;}
    internal static object EnterRequest(NetPackageRequestToSpawnEntity packet,World world,out bool allowNative)
    {
        if(RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(packet,world)){allowNative=true;return null;}
        if(preparedBodyExecution!=null)return EnterPreparedBody(packet,world,out allowNative);
        // New originals use the authenticated purpose-specific execute request.
        // Content equality alone must never adopt an unrelated vanilla death.
        allowNative=true;return null;
    }
}
