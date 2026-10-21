using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private static readonly Dictionary<string,OwedChild> currentOwedNative=new Dictionary<string,OwedChild>(StringComparer.Ordinal);private static World currentOwedWorld;private static string currentOwedDirectory;
    private static void CurrentOwedScope(World world){if(ReferenceEquals(currentOwedWorld,world)&&currentOwedDirectory==GameIO.GetSaveGameDir())return;currentOwedNative.Clear();currentOwedWorld=world;currentOwedDirectory=GameIO.GetSaveGameDir();}
    private static void TrackOwedNativeChild(EntityPlayer actor,OwedChild c){if(!OwedIdentity(actor,c))return;CurrentOwedScope((World)actor.world);currentOwedNative[c.Owner]=DecodeChild(ChildBody(c));}
    internal static void ObserveCurrentOwedSaved(EntityPlayer actor)
    {try{if(actor?.world?.IsRemote()!=false)return;CurrentOwedScope((World)actor.world);if(RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&currentOwedNative.TryGetValue(owner.StorageKey,out var c)&&OwedIdentity(actor,c))CompleteOwedChild(actor,c,out _);}catch{}}
    internal static void ObserveCurrentOwedFatal(EntityPlayer actor)
    {try{if(actor?.world?.IsRemote()!=false)return;CurrentOwedScope((World)actor.world);if(RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&currentOwedNative.TryGetValue(owner.StorageKey,out var c)&&OwedIdentity(actor,c))ObserveOwedFatal(actor,c.Parent);}catch{}}
    private static string OwedNativePath(Guid child)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-native-checkpoints",child.ToString("N")+".xml");
    private static string OwedNativeImage(OwedPhysicalTrace t,string native)
    {var x=new XElement("placedOwedNative",new XAttribute("version",1),new XAttribute("child",ChildBinding(t.Child)),new XAttribute("trace",t.Digest),new XAttribute("native",native));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool ReadOwedNative(string path,OwedPhysicalTrace t,out string proof)
    {proof=null;try{if(!File.Exists(path)||new FileInfo(path).Length>4096)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));var x=OwedXml(image);proof=(string)x.Attribute("native");return FullHash(proof)&&image==OwedNativeImage(t,proof);}catch{return false;}}
    private static bool CheckpointOwedPhysical(EntityPlayer actor,OwedPhysicalTrace t,out string proof)
    {
        proof=null;try
        {
            string path=OwedNativePath(t.Child.Operation),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak"))return ReadOwedNative(path,t,out proof);Directory.CreateDirectory(folder);var pending=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||!ReadOwedNative(pending[0],t,out proof))return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadOwedNative(path,t,out proof);}
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var file)||file.id!=t.Observation.NativeOwnerEntity||file.deaths!=t.Child.Deaths+1||!SavedOwedMarkerDigest(file,t.Digest,5)||!actual.Matches(RebirthPlacedOriginal.ReadActor(t.Actor))||!SavedEquipment(file).SequenceEqual(Convert.FromBase64String(t.Gear)))return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(t.Image);writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.equipmentData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            string image=OwedNativeImage(t,proof),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var data=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(data,0,data.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadOwedNative(path,t,out proof);
        }catch{return false;}
    }
    private static bool OwnNestedOwedPartition(OwedPartition p,OwedPhysicalTrace t)
    {
        var c=t.Child;if(!ReadChild(ChildPath(c,true),out var accepted)||!SameChild(accepted,c)||p.Operation!=c.Operation||p.World!=c.World||p.Owner!=c.Owner||p.Creation!=c.Creation||c.Deaths==int.MaxValue||p.Deaths!=c.Deaths+1||p.Trace!=t.Digest||!ReadOwedNative(OwedNativePath(c.Operation),t,out var proof)||p.Native!=proof)return false;var rows=t.Inputs.Where(i=>t.Overflow||i.Failed).Select(i=>i.Item).ToArray();return rows.Length>0&&Rows(rows).SequenceEqual(Convert.FromBase64String(p.Owed))&&(t.Creation.Length==0||RebirthPlacedOriginalBodyPublication.TryPublished(c.Operation,t.Creation,out _));
    }
    private static bool ValidOwedPartitionSource(OwedPartition p)
    {
        string initialPath=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","physical-server",p.Operation.ToString("N")+".xml");if(File.Exists(initialPath)||File.Exists(initialPath+".bak"))return ReadPhysical(initialPath,out var initial)&&OwnOwedPartition(p,initial);string nestedPath=OwedPhysicalPath(p.Operation,true);if(File.Exists(nestedPath)||File.Exists(nestedPath+".bak"))return ReadOwedPhysical(p.Operation,true,out var nested)&&OwnNestedOwedPartition(p,nested);return ReadTerminalOperation(p.Operation,out var terminal)&&OwnTerminalParcel(p,terminal);
    }
    private static bool RetainNestedOwed(OwedPhysicalTrace t,string native,out OwedPartition p)
    {
        p=null;try{var c=t.Child;var rows=t.Inputs.Where(i=>t.Overflow||i.Failed).Select(i=>i.Item.Clone()).ToArray();if(rows.Length==0||!FullHash(native)||c.Deaths==int.MaxValue)return false;p=DecodeOwed(OwedBody(new OwedPartition{Operation=c.Operation,World=c.World,Owner=c.Owner,Creation=c.Creation,Deaths=c.Deaths+1,Trace=t.Digest,Body=string.Empty,Native=native,Owed=Convert.ToBase64String(Rows(rows))}));return SaveOwed(p);}catch{return false;}
    }
    private static bool NestedOwedTerminal(OwedPartition p,out string proof)
    {
        proof=null;if(!ValidOwedPartitionSource(p)||!OwedHeadFor(p,out var h))return false;var rows=ReadRows(Convert.FromBase64String(p.Owed));for(int i=0;i<rows.Length;i++)if(rows[i].count!=h.Delivered[i])return false;proof=Hash(p.Image+"|"+HeadBody(h));return true;
    }
}