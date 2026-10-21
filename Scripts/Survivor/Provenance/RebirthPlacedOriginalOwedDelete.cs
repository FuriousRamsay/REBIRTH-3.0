using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private sealed class OwedDeleteObservation
    {internal OwedChild Child;internal string Policy,Equipment,Image,Digest;internal int Entity;internal bool Delivered;}
    private sealed class OwedDeleteFrame
    {internal OwedDeleteObservation Observation;internal EntityPlayerLocal Player;internal World World;internal string Directory;}
    private static OwedDeleteFrame owedDelete;
    private static string DeleteObservationBody(OwedDeleteObservation o)=>new XElement("placedOwedDelete",new XAttribute("version",1),new XAttribute("entity",o.Entity),new XAttribute("delivered",o.Delivered),new XAttribute("policy",o.Policy),new XElement("child",ChildBody(o.Child)),new XElement("equipment",o.Equipment)).ToString(SaveOptions.DisableFormatting);
    private static OwedDeleteObservation DecodeOwedDelete(string image)
    {
        var x=OwedXml(image);if(x.Name!="placedOwedDelete"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();var o=new OwedDeleteObservation{Child=DecodeChild((string)x.Element("child")),Policy=(string)x.Attribute("policy"),Equipment=(string)x.Element("equipment"),Entity=(int)x.Attribute("entity"),Delivered=(bool)x.Attribute("delivered"),Image=image};
        if(o.Entity<=0||!o.Delivered||o.Child.Phase!=2||!o.Child.Applied||!DeleteProjection(o,out _,out _)||DeleteObservationBody(o)!=image)throw new InvalidDataException();o.Digest=Hash(image);return o;
    }
    private static bool DeleteProjection(OwedDeleteObservation o,out RebirthCamperEnergyActorImage actor,out byte[] equipment)
    {
        actor=null;equipment=null;try
        {
            if(!ValidPolicy(o.Policy))return false;using(var stream=new MemoryStream(Convert.FromBase64String(o.Policy),false))using(var reader=new BinaryReader(stream)){reader.ReadByte();int mode=reader.ReadInt32(),loss=reader.ReadInt32(),degrade=reader.ReadInt32();float amount=reader.ReadSingle();if(mode!=6||loss!=0||!TryProject(RebirthPlacedOriginal.ReadActor(o.Delivered?o.Child.After:o.Child.Before),mode,loss,degrade,amount,out actor))return false;var original=RebirthPlacedOriginal.ReadActor(o.Delivered?o.Child.After:o.Child.Before);if(!RebirthLogisticsPhysicalLossWitness.TryProject(original.Bag,original.Belt,Convert.FromBase64String(o.Equipment),new byte[(original.Bag.Length+original.Belt.Length+13+7)/8],2,0,0,0,degrade,amount,out var projected))return false;equipment=projected.DegradedEquipment;}
            var gear=EquipmentImage(equipment);for(int i=0;i<13;i++){var value=gear.GetSlotItem(i);if(value!=null&&!value.IsEmpty()&&!value.ItemClassOrMissing.KeepOnDeath())gear.ItemGrid[i]=ItemStack.Empty.Clone();}equipment=RebirthLogisticsDeleteWitness.EquipmentBytes(gear);return true;
        }catch{return false;}
    }
    private static string OwedDeletePath(Guid child,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"owed-delete-server":"owed-delete-owner",child.ToString("N")+".xml");
    private static bool ReadOwedDelete(Guid child,bool server,out OwedDeleteObservation o)
    {o=null;try{string path=OwedDeletePath(child,server);if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;o=DecodeOwedDelete(File.ReadAllText(path,new UTF8Encoding(false,true)));return o.Child.Operation==child;}catch{return false;}}
    private static bool SaveOwedDelete(OwedDeleteObservation o,bool server)
    {
        try{string path=OwedDeletePath(o.Child.Operation,server);if(File.Exists(path)||File.Exists(path+".bak"))return ReadOwedDelete(o.Child.Operation,server,out var old)&&old.Image==o.Image;Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>Maximum||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=o.Image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadOwedDelete(o.Child.Operation,server,out var recovered)&&recovered.Image==o.Image;}string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(o.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadOwedDelete(o.Child.Operation,server,out var saved)&&saved.Image==o.Image;}catch{return false;}
    }
    private static bool OwedDeleteCurrent(OwedDeleteFrame frame)=>frame!=null&&ReferenceEquals(owedDelete,frame)&&ReferenceEquals(GameManager.Instance?.World,frame.World)&&ReferenceEquals(frame.World.GetPrimaryPlayer(),frame.Player)&&frame.Directory==GameIO.GetSaveGameDir()&&OwedIdentity(frame.Player,frame.Observation.Child)&&frame.Player.IsDead()&&frame.Observation.Child.Deaths<int.MaxValue&&frame.Player.Died==frame.Observation.Child.Deaths+1&&frame.Observation.Policy==Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax);
    private static void ObserveOwedDeleteStarting(EntityPlayerLocal player)
    {
        try
        {
            if(owedDelete!=null&&!OwedDeleteCurrent(owedDelete))owedDelete=null;if(owedDelete!=null||player==null||!player.IsDead()||(int)EntityPlayerLocal.DropOnDeathOption!=6)return;OwedScope((World)player.world);
            foreach(var a in owedAttempts.Values.ToArray())
            {
                if(RetireLocalOwedTerminal(a))continue;var c=a.Child;if(!ReferenceEquals(a.Player,player)||!OwedIdentity(player,c)||c.Phase!=2||c.Deaths==int.MaxValue||player.Died!=c.Deaths+1)continue;
                var actual=RebirthCamperEnergyActorImage.Capture(player);bool delivered=actual.Matches(RebirthPlacedOriginal.ReadActor(c.After)),unused=actual.Matches(RebirthPlacedOriginal.ReadActor(c.Before));if(!delivered||unused)continue;if(delivered&&!c.Applied){string prior=ChildBody(c);var observed=DecodeChild(prior);observed.Applied=true;if(!SaveChild(observed,false,prior))continue;a.Child=c=observed;}
                var o=new OwedDeleteObservation{Child=DecodeChild(ChildBody(c)),Entity=player.entityId,Delivered=delivered,Equipment=Convert.ToBase64String(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)),Policy=Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax)};o=DecodeOwedDelete(DeleteObservationBody(o));if(!SaveOwedDelete(o,false))return;owedDelete=new OwedDeleteFrame{Observation=o,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir()};SendOwed(player,o.Image);return;
            }
        }catch{/* No preclear save or actor mutation. */}
    }
    private static void ReceiveOwedDelete(EntityPlayer actor,string image)
    {
        try{var o=DecodeOwedDelete(image);if(actor?.world?.IsRemote()!=false)return;if(ReplySavedOwedTerminal(actor,o.Child))return;if(!OwedIdentity(actor,o.Child)||!ReadChild(ChildPath(o.Child,true),out var actual)||!SameChild(actual,o.Child)||actual.Phase!=1||!ReadOwedFatal(actual,out int entity,out string policy)||entity!=o.Entity||policy!=o.Policy||actor.Died<actual.Deaths)return;if(SaveOwedDelete(o,true))CompleteOwedChild(actor,o.Child,out _);}catch{}
    }
    private static void ObserveOwedDeleteBeforeRespawn(EntityPlayerLocal player)
    {
        try{var f=owedDelete;if(!OwedDeleteCurrent(f)||!ReferenceEquals(f.Player,player)||!DeleteProjection(f.Observation,out var actor,out var gear)||!RebirthCamperEnergyActorImage.Capture(player).Matches(actor)||!RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment).SequenceEqual(gear))return;var o=f.Observation;for(int i=0;i<16;i++)player.Buffs.SetCustomVar(OwedMarkerPrefix+i,Convert.ToUInt16(o.Digest.Substring(i*4,4),16),true);player.Buffs.SetCustomVar(OwedMarkerPrefix+"Phase",4,true);UploadOwed(player);SendOwed(player,o.Image);owedDelete=null;}catch{}
    }
    private static void RetryOwedDelete(EntityPlayerLocal player,OwedChild child)
    {try{if(OwedIdentity(player,child)&&player.Died>child.Deaths&&ReadOwedDelete(child.Operation,false,out var o)&&SameChild(child,o.Child))SendOwed(player,o.Image);}catch{}}
    private static bool SavedOwedDeleteMarker(PlayerDataFile file,OwedDeleteObservation o)
    {
        // Use the exact existing native buff decoder with a detached binding
        // selector; no client payload alone supplies a native-file witness.
        return SavedOwedMarkerDigest(file,o.Digest,4);
    }
    private static bool TryCompleteOwedDelete(EntityPlayer actor,OwedChild saved,out OwedChild terminal)
    {
        terminal=null;try
        {
            if(!OwedIdentity(actor,saved)||saved.Deaths==int.MaxValue||actor.Died<saved.Deaths+1||!ReadOwedDelete(saved.Operation,true,out var o)||!SameChild(saved,o.Child)||!ReadOwedFatal(saved,out int entity,out string policy)||policy!=o.Policy||entity!=o.Entity||!DeleteProjection(o,out var expected,out var gear)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var file)||file.id!=entity||file.deaths!=saved.Deaths+1||!SavedOwedDeleteMarker(file,o)||!actual.Matches(expected)||!SavedEquipment(file).SequenceEqual(gear))return false;
            string prior=ChildBody(saved);var next=DecodeChild(prior);next.Phase=o.Delivered?3:4;next.Applied=o.Delivered;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(o.Image);writer.Write(OwedFatalImage(saved,entity,policy));writer.Write(file.id);writer.Write(file.deaths);foreach(var bytes in new[]{file.bagData.ToArray(),file.inventoryData.ToArray(),file.equipmentData.ToArray(),file.buffData.ToArray()}){writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();using(var hash=System.Security.Cryptography.SHA256.Create())next.Proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}if(!SaveChild(next,true,prior)||!AdvanceOwedHead(next))return false;terminal=next;return true;
        }catch{return false;}
    }
}