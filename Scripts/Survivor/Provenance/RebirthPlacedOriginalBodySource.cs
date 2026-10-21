using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    internal static bool CanReleaseBodyCustody(bool savedBody,bool savedClearedSource)=>savedBody&&savedClearedSource;
    private static string BodySourcePath(Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","body-source-checkpoints",operation.ToString("N")+".xml");
    private static string BodySourceImage(PhysicalTrace t,string proof)
    {var x=new XElement("placedBodyClearedSource",new XAttribute("version",1),new XAttribute("operation",t.Observation.Original.Operation.ToString("N")),new XAttribute("trace",t.Digest),new XAttribute("native",proof));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool ReadBodySource(string path,PhysicalTrace t)
    {try{if(!File.Exists(path)||new FileInfo(path).Length>4096)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));var x=OwedXml(image);string proof=(string)x.Attribute("native");return FullHash(proof)&&BodySourceImage(t,proof)==image;}catch{return false;}}
    private static bool CheckpointBodySource(PhysicalTrace t,RebirthCamperEnergyActorImage actual,PlayerDataFile file)
    {
        try
        {
            string path=BodySourcePath(t.Observation.Original.Operation);if(File.Exists(path)||File.Exists(path+".bak"))return ReadBodySource(path,t);
            var r=t.Observation.Original;var world=GameManager.Instance?.World;if(!RebirthPlacedOriginal.ValidateStoredBodyOriginal(world,r,t.Observation.Policy,t.Observation.NativeOwnerEntity)||r.Deaths==int.MaxValue||file.id!=t.Observation.NativeOwnerEntity||file.deaths!=r.Deaths+1||!Marker(file,t.Digest)||!actual.Matches(RebirthPlacedOriginal.ReadActor(t.FinalActor))||!SavedEquipment(file).SequenceEqual(Convert.FromBase64String(t.FinalEquipment)))return false;
            string proof;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(t.Image);writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.equipmentData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            Directory.CreateDirectory(Path.GetDirectoryName(path));string image=BodySourceImage(t,proof),temp;var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||!ReadBodySource(pending[0],t))return false;temp=pending[0];}else{temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}}
            return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadBodySource(path,t);
        }catch{return false;}
    }
    internal static bool HasSavedBodySource(World world,Guid operation,string request)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return false;
            if(ReadTerminalOperation(operation,out var terminal))return terminal.World==world.worldState.Guid&&terminal.Native==request&&TerminalFatalLineage(terminal)&&ReadTerminalSource(TerminalSourcePath(terminal),terminal,out _);
            string owed=OwedPhysicalPath(operation,true);if(File.Exists(owed)||File.Exists(owed+".bak")){if(!ReadOwedPhysical(operation,true,out var t)||t.Child.World!=world.worldState.Guid||t.Creation!=request)return false;if(ReadOwedNative(OwedNativePath(operation),t,out _))return true;var actor=world.GetEntity(t.Observation.NativeOwnerEntity) as EntityPlayer;return ValidHistoricalOwedPhysicalOwner(actor,t,true)&&CheckpointOwedPhysical(actor,t,out _);}
            string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","physical-server",operation.ToString("N")+".xml");if(!ReadPhysical(path,out var physical)||physical.Observation.Original.World!=world.worldState.Guid||physical.Creation!=request)return false;if(ReadBodySource(BodySourcePath(operation),physical))return true;var owner=world.GetEntity(physical.Observation.NativeOwnerEntity) as EntityPlayer;return RebirthPlacedOriginal.ValidatePartition(owner,physical.Observation.Original,physical.Observation.Policy,physical.Observation.NativeOwnerEntity)&&RebirthStablePlayerIdentity.TryResolveServerEntity(owner,out var identity)&&RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var file)&&CheckpointBodySource(physical,actual,file);
        }catch{return false;}
    }
}