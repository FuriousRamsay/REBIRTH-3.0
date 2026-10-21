using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    // Read-only death observation for a positively pre-attempt child0/1. No
    // original fragment exists in its native body, so that body stays vanilla.
    private sealed class OwedUnusedObservation
    {internal OwedChild Child;internal string Policy,Equipment,Actor=string.Empty,Gear=string.Empty,Image,Digest;internal int Entity;}
    private sealed class OwedUnusedFrame{internal OwedUnusedObservation Observation;internal EntityPlayerLocal Player;internal World World;internal string Directory;}
    private static OwedUnusedFrame owedUnused;
    private static string OwedUnusedBody(OwedUnusedObservation o)=>new XElement("placedOwedUnused",new XAttribute("version",1),new XAttribute("entity",o.Entity),new XAttribute("policy",o.Policy),new XElement("child",ChildBody(o.Child)),new XElement("equipment",o.Equipment),new XElement("actor",o.Actor),new XElement("gear",o.Gear)).ToString(SaveOptions.DisableFormatting);
    private static OwedUnusedObservation DecodeOwedUnused(string image)
    {
        var x=OwedXml(image);if(x.Name!="placedOwedUnused"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();var o=new OwedUnusedObservation{Child=DecodeChild((string)x.Element("child")),Policy=(string)x.Attribute("policy"),Equipment=(string)x.Element("equipment"),Actor=(string)x.Element("actor"),Gear=(string)x.Element("gear"),Entity=(int)x.Attribute("entity"),Image=image};if(o.Child.Phase>1||o.Child.Applied||o.Entity<=0||!ValidPolicy(o.Policy)||OwedUnusedBody(o)!=image||(o.Actor.Length==0)!=(o.Gear.Length==0))throw new InvalidDataException();EquipmentImage(Convert.FromBase64String(o.Equipment));if(o.Actor.Length!=0){RebirthPlacedOriginal.ReadActor(o.Actor);EquipmentImage(Convert.FromBase64String(o.Gear));}o.Digest=Hash(image);return o;
    }
    private static string OwedUnusedPath(Guid op,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"owed-unused-server":"owed-unused-owner",op.ToString("N")+".xml");
    private static bool ReadOwedUnused(Guid op,bool server,out OwedUnusedObservation o)
    {o=null;try{string path=OwedUnusedPath(op,server);if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;o=DecodeOwedUnused(File.ReadAllText(path,new UTF8Encoding(false,true)));return o.Child.Operation==op;}catch{return false;}}
    private static bool SameUnusedPreclear(OwedUnusedObservation a,OwedUnusedObservation b)=>SameChild(a.Child,b.Child)&&a.Policy==b.Policy&&a.Equipment==b.Equipment&&a.Entity==b.Entity;
    private static bool SaveOwedUnused(OwedUnusedObservation o,bool server)
    {
        try{string path=OwedUnusedPath(o.Child.Operation,server);if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadOwedUnused(o.Child.Operation,server,out var old)||!SameUnusedPreclear(old,o)||old.Actor.Length!=0&&old.Image!=o.Image)return false;if(old.Image==o.Image)return true;if(server||o.Actor.Length==0)return false;}Directory.CreateDirectory(Path.GetDirectoryName(path));var files=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(files.Length!=0){if(files.Length!=1||new FileInfo(files[0]).Length>Maximum||File.ReadAllText(files[0],new UTF8Encoding(false,true))!=o.Image)return false;return RebirthDurableFileCommit.TryPublish(files[0],path,out _)&&ReadOwedUnused(o.Child.Operation,server,out var recovered)&&recovered.Image==o.Image;}string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(o.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadOwedUnused(o.Child.Operation,server,out var saved)&&saved.Image==o.Image;}catch{return false;}
    }
    private static bool OwedUnusedCurrent(OwedUnusedFrame f)=>f!=null&&ReferenceEquals(owedUnused,f)&&ReferenceEquals(GameManager.Instance?.World,f.World)&&ReferenceEquals(f.World.GetPrimaryPlayer(),f.Player)&&f.Directory==GameIO.GetSaveGameDir()&&OwedIdentity(f.Player,f.Observation.Child)&&f.Player.IsDead()&&f.Observation.Child.Deaths<int.MaxValue&&f.Player.Died==f.Observation.Child.Deaths+1&&f.Observation.Policy==Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax);
    private static void ObserveOwedUnusedStarting(EntityPlayerLocal player)
    {
        try{if(owedUnused!=null&&!OwedUnusedCurrent(owedUnused))owedUnused=null;if(owedUnused!=null||player==null||!player.IsDead())return;OwedScope((World)player.world);foreach(var a in owedAttempts.Values.ToArray()){if(RetireLocalOwedTerminal(a))continue;var c=a.Child;if(!ReferenceEquals(a.Player,player)||c.Phase>1||c.Applied||!OwedIdentity(player,c)||c.Deaths==int.MaxValue||player.Died!=c.Deaths+1||!RebirthCamperEnergyActorImage.Capture(player).Matches(RebirthPlacedOriginal.ReadActor(c.Before)))continue;var o=new OwedUnusedObservation{Child=DecodeChild(ChildBody(c)),Entity=player.entityId,Equipment=Convert.ToBase64String(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)),Policy=Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax)};o=DecodeOwedUnused(OwedUnusedBody(o));if(!SaveOwedUnused(o,false))return;owedUnused=new OwedUnusedFrame{Observation=o,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir()};return;}}catch{}
    }
    private static void ObserveOwedUnusedBeforeRespawn(EntityPlayerLocal player)
    {
        try{var f=owedUnused;if(!OwedUnusedCurrent(f)||!ReferenceEquals(f.Player,player))return;var o=f.Observation;var complete=DecodeOwedUnused(OwedUnusedBody(new OwedUnusedObservation{Child=o.Child,Entity=o.Entity,Policy=o.Policy,Equipment=o.Equipment,Actor=RebirthPlacedOriginal.EncodeActor(RebirthCamperEnergyActorImage.Capture(player)),Gear=Convert.ToBase64String(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment))}));if(!SaveOwedUnused(complete,false))return;for(int i=0;i<16;i++)player.Buffs.SetCustomVar(OwedMarkerPrefix+i,Convert.ToUInt16(complete.Digest.Substring(i*4,4),16),true);player.Buffs.SetCustomVar(OwedMarkerPrefix+"Phase",6,true);UploadOwed(player);SendOwed(player,complete.Image);owedUnused=null;}catch{}
    }
    private static void ReceiveOwedUnused(EntityPlayer actor,string image)
    {
        try{var o=DecodeOwedUnused(image);if(actor?.world?.IsRemote()!=false)return;if(ReplySavedOwedTerminal(actor,o.Child))return;if(o.Actor.Length==0||!OwedIdentity(actor,o.Child)||!ReadChild(ChildPath(o.Child,true),out var actual)||actual.Phase!=1||!SameChild(actual,o.Child)||!ReadOwedFatal(actual,out int entity,out string policy)||entity!=o.Entity||policy!=o.Policy)return;if(SaveOwedUnused(o,true))CompleteOwedChild(actor,o.Child,out _);}catch{}
    }
    private static bool TryCompleteOwedUnused(EntityPlayer actor,OwedChild saved,out OwedChild terminal)
    {
        terminal=null;try{if(!ReadOwedUnused(saved.Operation,true,out var o)||o.Actor.Length==0||!SameChild(saved,o.Child)||!OwedIdentity(actor,saved)||saved.Deaths==int.MaxValue||actor.Died<saved.Deaths+1||!ReadOwedFatal(saved,out int entity,out string policy)||entity!=o.Entity||policy!=o.Policy||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var file)||file.id!=entity||file.deaths!=saved.Deaths+1||!SavedOwedMarkerDigest(file,o.Digest,6)||!actual.Matches(RebirthPlacedOriginal.ReadActor(o.Actor))||!SavedEquipment(file).SequenceEqual(Convert.FromBase64String(o.Gear)))return false;string old=ChildBody(saved);var next=DecodeChild(old);next.Phase=4;next.Applied=false;next.Proof=Hash("positive-native-pre-attempt-unused|"+o.Image+"|"+OwedFatalImage(saved,entity,policy)+"|"+Convert.ToBase64String(file.buffData.ToArray()));if(!SaveChild(next,true,old)||!AdvanceOwedHead(next))return false;terminal=next;return true;}catch{return false;}
    }
    private static void RetryOwedUnused(EntityPlayerLocal player,OwedChild c)
    {try{if(OwedIdentity(player,c)&&player.Died>c.Deaths&&ReadOwedUnused(c.Operation,false,out var o)&&o.Actor.Length!=0&&SameChild(c,o.Child))SendOwed(player,o.Image);}catch{}}
}