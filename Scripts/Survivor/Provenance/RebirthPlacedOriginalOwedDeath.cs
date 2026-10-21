using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    // Child-specific fatal receipt. This never creates a new placement obligation
    // and never obtains authority from a completed placement or absent output.
    private static string OwedFatalPath(OwedChild c)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-fatal",c.Operation.ToString("N")+".xml");
    private static string OwedFatalImage(OwedChild c,int entity,string policy)
    {var x=new XElement("placedOwedFatal",new XAttribute("version",1),new XAttribute("child",ChildBinding(c)),new XAttribute("entity",entity),new XAttribute("policy",policy));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static bool ReadOwedFatal(OwedChild c,out int entity,out string policy)
    {
        entity=0;policy=null;try{string path=OwedFatalPath(c);if(!File.Exists(path)||new FileInfo(path).Length>4096)return false;string image=File.ReadAllText(path,new UTF8Encoding(false,true));var x=OwedXml(image);entity=(int)x.Attribute("entity");policy=(string)x.Attribute("policy");return entity>0&&ValidPolicy(policy)&&image==OwedFatalImage(c,entity,policy);}catch{return false;}
    }
    internal static void ObserveOwedFatal(EntityPlayer actor,Guid parent)
    {
        try
        {
            if(actor?.world?.IsRemote()!=false||!actor.IsDead()||actor.Health>0||!ReadOwed(parent,out var p)||!OwedOwner(actor,p)||!OwedHeadFor(p,out var h))return;
            var key=new OwedChild{Parent=parent,Sequence=h.Sequence+1};if(!ReadChild(ChildPath(key,true),out var c)||c.Phase!=1||!OwedIdentity(actor,c)||c.Deaths==int.MaxValue||actor.Died<c.Deaths||actor.Died>c.Deaths+1)return;
            string path=OwedFatalPath(c),policy=ServerPolicy(),image=OwedFatalImage(c,actor.entityId,policy);if(File.Exists(path)||File.Exists(path+".bak"))return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length==1&&File.ReadAllText(pending[0],new UTF8Encoding(false,true))==image)RebirthDurableFileCommit.TryPublish(pending[0],path,out _);return;}
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.ReadAllText(temp,new UTF8Encoding(false,true))==image)RebirthDurableFileCommit.TryPublish(temp,path,out _);
        }catch{/* Original child remains retained. */}
    }
    internal static bool TryClassifyOwedEnded(RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,RebirthCamperEnergyActorImage actual,int mode,int loss,int degrade,float amount,out bool delivered)
    {
        delivered=false;if(mode==6||!TryProject(before,mode,loss,degrade,amount,out var a)||!TryProject(after,mode,loss,degrade,amount,out var b)||a.Matches(b))return false;bool used=actual!=null&&actual.Matches(b),unused=actual!=null&&actual.Matches(a);if(used==unused)return false;delivered=used;return true;
    }
    private static bool ProjectOwedEnded(OwedChild c,string policy,out RebirthCamperEnergyActorImage before,out RebirthCamperEnergyActorImage after)
    {
        before=null;after=null;try
        {
            if(!ValidPolicy(policy))return false;using(var stream=new MemoryStream(Convert.FromBase64String(policy),false))using(var reader=new BinaryReader(stream)){reader.ReadByte();int mode=reader.ReadInt32(),loss=reader.ReadInt32(),degrade=reader.ReadInt32();float amount=reader.ReadSingle();
                // Non-injective delete/random/drop transforms need a full typed
                // preclear/body partition. They cannot enter this proof lane.
                if(mode==6||!TryProject(RebirthPlacedOriginal.ReadActor(c.Before),mode,loss,degrade,amount,out before)||!TryProject(RebirthPlacedOriginal.ReadActor(c.After),mode,loss,degrade,amount,out after)||before.Matches(after))return false;return true;}
        }catch{return false;}
    }
    private static void ObserveOwedBeforeRespawn(EntityPlayerLocal player)
    {
        try
        {
            if(player==null||!player.IsDead()||!ReferenceEquals(GameManager.Instance?.World?.GetPrimaryPlayer(),player))return;if(physical?.Context?.Owed!=null&&PhysicalCurrent(physical))return;OwedScope((World)player.world);
            foreach(var a in owedAttempts.Values.ToArray())
            {
                var c=a.Child;if(!ReferenceEquals(a.Player,player)||!OwedIdentity(player,c)||c.Phase!=2||c.Deaths==int.MaxValue||player.Died!=c.Deaths+1)continue;
                string policy=Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax);
                if(!ProjectOwedEnded(c,policy,out var before,out var after))continue;var actual=RebirthCamperEnergyActorImage.Capture(player);bool used=actual.Matches(after),unused=actual.Matches(before);if(!used||unused)continue;
                // Existing actual pre-Respawn hook runs after native equipment
                // clear. Save/upload occurs only here, never preclear.
                string binding=ChildBinding(c);for(int i=0;i<16;i++)player.Buffs.SetCustomVar(OwedMarkerPrefix+i,Convert.ToUInt16(binding.Substring(i*4,4),16),true);player.Buffs.SetCustomVar(OwedMarkerPrefix+"Phase",3,true);UploadOwed(player);
            }
        }catch{/* No native slot replay on an unknown outcome. */}
    }
    private static bool TryCompleteEndedOwed(EntityPlayer actor,OwedChild saved,out OwedChild terminal)
    {
        terminal=null;try
        {
            if(!OwedIdentity(actor,saved)||saved.Deaths==int.MaxValue||actor.Died<saved.Deaths+1||!ReadOwedFatal(saved,out int entity,out string policy)||policy!=ServerPolicy()||!ProjectOwedEnded(saved,policy,out var before,out var after)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var file)||file.id!=entity||file.deaths!=saved.Deaths+1||!SavedOwedMarker(file,saved,3))return false;
            if(!TryClassifyOwedEnded(before,after,actual,0,0,0,0,out bool delivered)||!delivered)return false;
            string previous=ChildBody(saved);var next=DecodeChild(previous);next.Phase=delivered?3:4;next.Applied=delivered;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(ChildBinding(saved));writer.Write(OwedFatalImage(saved,entity,policy));writer.Write(delivered);writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())next.Proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
            if(!SaveChild(next,true,previous)||!AdvanceOwedHead(next))return false;terminal=next;return true;
        }catch{return false;}
    }
}