using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private sealed class OwedPhysicalTrace
    {internal OwedChild Child;internal Observation Observation;internal bool Delivered,Overflow;internal byte[] Selected;internal string Actor,Gear,Creation,Image,Digest;internal BuilderInput[] Inputs;}
    private static bool CurrentOwedPhysical(Context c)=>c!=null&&ReferenceEquals(current,c)&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,c.World)&&ReferenceEquals(c.World.GetPrimaryPlayer(),c.Player)&&c.Directory==GameIO.GetSaveGameDir()&&OwedIdentity(c.Player,c.Owed)&&c.Player.IsDead()&&c.Owed.Deaths<int.MaxValue&&c.Player.Died==c.Owed.Deaths+1&&c.Observation.Policy==Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax);
    private static void TryBeginOwedPhysical(EntityPlayerLocal player)
    {
        try
        {
            if(current!=null&&!Current(current))current=null;if(current!=null||player==null||!player.IsDead()||(int)EntityPlayerLocal.DropOnDeathOption==6)return;OwedScope((World)player.world);
            foreach(var a in owedAttempts.Values.ToArray())
            {
                if(RetireLocalOwedTerminal(a))continue;var c=a.Child;if(!ReferenceEquals(a.Player,player)||!OwedIdentity(player,c)||c.Phase!=2||c.Deaths==int.MaxValue||player.Died!=c.Deaths+1)continue;var actual=RebirthCamperEnergyActorImage.Capture(player);bool delivered=actual.Matches(RebirthPlacedOriginal.ReadActor(c.After)),unused=actual.Matches(RebirthPlacedOriginal.ReadActor(c.Before));if(!delivered||unused)continue;
                if(delivered&&!c.Applied){string old=ChildBody(c);var observed=DecodeChild(old);observed.Applied=true;if(!SaveChild(observed,false,old))continue;a.Child=c=observed;}
                string policy=Policy((int)EntityPlayerLocal.DropOnDeathOption,SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),(int)EntityPlayerLocal.DegradeOnDeathType,EntityPlayerLocal.DegradeAmountOnDeath,EntityPlayerLocal.LostItemOnDeathMin,EntityPlayerLocal.LostItemOnDeathMax);var o=OwedPhysicalObservation(c,delivered,policy,player.entityId,Convert.ToBase64String(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)));
                if(!SaveOwedPreclear(c,delivered,o))return;
                current=new Context{Owed=DecodeChild(ChildBody(c)),OwedDelivered=delivered,Observation=o,Player=player,World=(World)player.world,Directory=GameIO.GetSaveGameDir()};BeginPhysical(current);return;
            }
        }catch{/* Existing child remains retained. */}
    }
    private static bool CanRepresentOwedRecovery(OwedChild c,byte[] equipment)
    {
        try
        {
            // Pure codec admission before a new slot effect. Bound the largest
            // own trace (all native rows failed, maximum native ECD) rather than
            // discovering an oversized retained death image after delivery.
            var actor=RebirthPlacedOriginal.ReadActor(c.After);var gear=EquipmentImage(equipment);var rows=actor.Bag.Concat(actor.Belt).Where(row=>!row.IsEmpty()).Select(row=>row.Clone()).ToList();for(int i=0;i<13;i++){var value=gear.GetSlotItem(i);if(value!=null&&!value.IsEmpty())rows.Add(new ItemStack(value.Clone(),1));}
            long bound=checked((long)ChildBody(c).Length+c.After.Length+2L*Convert.ToBase64String(equipment).Length+262144L+8192L);foreach(var row in rows)bound=checked(bound+Convert.ToBase64String(Rows(new[]{row})).Length+128L);return bound<=Maximum;
        }catch{return false;}
    }
    private static bool SaveOwedPreclear(OwedChild c,bool delivered,Observation o)
    {
        try{var x=new XElement("placedOwedPreclear",new XAttribute("version",1),new XAttribute("child",ChildBinding(c)),new XAttribute("delivered",delivered),new XAttribute("entity",o.NativeOwnerEntity),new XAttribute("policy",o.Policy),new XElement("original",ChildBody(c)),new XElement("actor",o.Preclear),new XElement("equipment",o.Equipment));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));string image=x.ToString(SaveOptions.DisableFormatting),path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-preclear-owner",c.Operation.ToString("N")+".xml");if(File.Exists(path)||File.Exists(path+".bak"))return new FileInfo(path).Length<=Maximum&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>Maximum||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _);}string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==image;}catch{return false;}
    }
    private static Observation OwedPhysicalObservation(OwedChild child,bool delivered,string policy,int entity,string equipment)
    {
        if(!ValidPolicy(policy)||entity<=0||!delivered||child.Phase!=2||!child.Applied)throw new InvalidDataException();var o=new Observation{NativeOwnerEntity=entity,Equipment=equipment,Policy=policy,Preclear=delivered?child.After:child.Before};EquipmentImage(Convert.FromBase64String(equipment));using(var stream=new MemoryStream(Convert.FromBase64String(policy),false))using(var reader=new BinaryReader(stream)){reader.ReadByte();o.Mode=reader.ReadInt32();o.Loss=reader.ReadInt32();o.Degrade=reader.ReadInt32();o.Amount=reader.ReadSingle();o.Minimum=reader.ReadInt32();o.Maximum=reader.ReadInt32();}if(o.Mode==6)throw new InvalidDataException();o.Digest=Hash(ChildBinding(child)+"|"+delivered+"|"+entity+"|"+policy+"|"+equipment);return o;
    }
    private static string OwedPhysicalBody(OwedPhysicalTrace t)=>new XElement("placedOwedPhysical",new XAttribute("version",1),new XAttribute("entity",t.Observation.NativeOwnerEntity),new XAttribute("delivered",t.Delivered),new XAttribute("policy",t.Observation.Policy),new XAttribute("overflow",t.Overflow),new XElement("child",ChildBody(t.Child)),new XElement("equipment",t.Observation.Equipment),new XElement("selected",Convert.ToBase64String(t.Selected)),new XElement("actor",t.Actor),new XElement("gear",t.Gear),new XElement("creation",t.Creation),new XElement("inputs",t.Inputs.Select(i=>new XElement("input",new XAttribute("failed",i.Failed),Convert.ToBase64String(Rows(new[]{i.Item})))))).ToString(SaveOptions.DisableFormatting);
    private static OwedPhysicalTrace DecodeOwedPhysical(string image)
    {
        var x=OwedXml(image);if(x.Name!="placedOwedPhysical"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();var c=DecodeChild((string)x.Element("child"));bool delivered=(bool)x.Attribute("delivered");var t=new OwedPhysicalTrace{Child=c,Delivered=delivered,Observation=OwedPhysicalObservation(c,delivered,(string)x.Attribute("policy"),(int)x.Attribute("entity"),(string)x.Element("equipment")),Overflow=(bool)x.Attribute("overflow"),Selected=Convert.FromBase64String((string)x.Element("selected")),Actor=(string)x.Element("actor"),Gear=(string)x.Element("gear"),Creation=(string)x.Element("creation"),Inputs=x.Element("inputs").Elements("input").Select(i=>{var rows=ReadRows(Convert.FromBase64String(i.Value));if(rows.Length!=1||rows[0].IsEmpty())throw new InvalidDataException();return new BuilderInput{Item=rows[0],Failed=(bool)i.Attribute("failed")};}).ToArray(),Image=image};
        if(t.Inputs.Length>525||t.Overflow&&t.Creation.Length!=0||!Project(t.Observation,t.Selected,out var projection)||!Convert.FromBase64String(t.Gear).SequenceEqual(projection.FinalEquipment)||OwedPhysicalBody(t)!=image)throw new InvalidDataException();var actor=RebirthPlacedOriginal.ReadActor(t.Actor);if(!Conserved(projection.DegradedBag.Concat(projection.DegradedBelt).Concat(projection.DroppedEquipment),actor.Bag.Concat(actor.Belt).Concat(t.Inputs.Select(i=>i.Item))))throw new InvalidDataException();
        if(t.Observation.Mode==0&&(t.Creation.Length!=0||t.Overflow||t.Inputs.Length!=0||!SameRows(projection.DegradedBag,actor.Bag)||!SameRows(projection.DegradedBelt,actor.Belt)))throw new InvalidDataException();var successful=t.Inputs.Where(i=>!i.Failed).Select(i=>i.Item).ToArray();if(t.Creation.Length!=0){var e=ReadEcd(t.Creation);var bag=NativeBag(e);var body=e.entityData.ToArray();bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);if(e.id!=-1||e.entityClass!="Backpack".GetHashCode()||!Finite(e.pos.x)||!Finite(e.pos.y)||!Finite(e.pos.z)||!Finite(e.rot.x)||!Finite(e.rot.y)||!Finite(e.rot.z)||body.Length<bag.Length+4||!body.Skip(body.Length-bag.Length).SequenceEqual(bag)||BitConverter.ToInt32(body,body.Length-bag.Length-4)!=t.Observation.NativeOwnerEntity||!Conserved(successful,ReadBodyBag(e)))throw new InvalidDataException();}else if(!t.Overflow&&successful.Length!=0)throw new InvalidDataException();t.Digest=Hash(image);return t;
    }
    private static OwedPhysicalTrace FreezeOwedPhysical(Physical p)
    {
        var c=p.Context;if(!PhysicalCurrent(p)||c.Owed==null||p.Projection==null||p.Adding)throw new InvalidDataException();return DecodeOwedPhysical(OwedPhysicalBody(new OwedPhysicalTrace{Child=c.Owed,Delivered=c.OwedDelivered,Observation=c.Observation,Overflow=p.Overflow,Selected=(byte[])p.Selected.Clone(),Actor=p.FinalActor??RebirthPlacedOriginal.EncodeActor(RebirthCamperEnergyActorImage.Capture(c.Player)),Gear=p.FinalEquipment??Convert.ToBase64String(p.Projection.FinalEquipment),Creation=p.Creation,Inputs=p.Inputs.Select(i=>new BuilderInput{Item=i.Item.Clone(),Failed=i.Failed}).ToArray()}));
    }
    private static string OwedPhysicalPath(Guid child,bool server)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",server?"owed-physical-server":"owed-physical-owner",child.ToString("N")+".xml");
    private static bool ReadOwedPhysical(Guid child,bool server,out OwedPhysicalTrace t)
    {t=null;try{string path=OwedPhysicalPath(child,server);if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;t=DecodeOwedPhysical(File.ReadAllText(path,new UTF8Encoding(false,true)));return t.Child.Operation==child;}catch{return false;}}
    private static bool SaveOwedPhysical(OwedPhysicalTrace t,bool server)
    {
        try{string path=OwedPhysicalPath(t.Child.Operation,server);if(File.Exists(path)||File.Exists(path+".bak"))return ReadOwedPhysical(t.Child.Operation,server,out var old)&&old.Image==t.Image;Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>Maximum||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=t.Image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&ReadOwedPhysical(t.Child.Operation,server,out var recovered)&&recovered.Image==t.Image;}string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(t.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadOwedPhysical(t.Child.Operation,server,out var saved)&&saved.Image==t.Image;}catch{return false;}
    }
    private static void PublishOwedPhysicalIntent(Physical p)
    {var t=FreezeOwedPhysical(p);if(!SaveOwedPhysical(t,false))throw new InvalidDataException("Owed child native body intent retained.");QueueDeferredBody(p.Context.Player,true,t.Image);}    private static void FinishOwedPhysicalBeforeRespawn(Context c,Physical p,EntityPlayerLocal player)
    {
        var t=FreezeOwedPhysical(p);if(!SaveOwedPhysical(t,false))return;for(int i=0;i<16;i++)player.Buffs.SetCustomVar(OwedMarkerPrefix+i,Convert.ToUInt16(t.Digest.Substring(i*4,4),16),true);player.Buffs.SetCustomVar(OwedMarkerPrefix+"Phase",5,true);UploadOwed(player);QueueNativeFinalTrace(player,true,t.Image);current=null;physical=null;
    }
    private static bool ValidOwedPhysicalOwner(EntityPlayer actor,OwedPhysicalTrace t,bool pending)
    {
        return OwedIdentity(actor,t.Child)&&ReadChild(ChildPath(t.Child,true),out var actual)&&SameChild(actual,t.Child)&&(!pending||actual.Phase==1)&&t.Child.Deaths<int.MaxValue&&actor.Died>=t.Child.Deaths&&ReadOwedFatal(t.Child,out int entity,out string policy)&&entity==actor.entityId&&entity==t.Observation.NativeOwnerEntity&&policy==t.Observation.Policy;
    }
    private static bool ValidHistoricalOwedPhysicalOwner(EntityPlayer actor,OwedPhysicalTrace t,bool pending)
    {return OwedIdentity(actor,t.Child)&&ReadChild(ChildPath(t.Child,true),out var actual)&&SameChild(actual,t.Child)&&(!pending||actual.Phase==1)&&actual.Phase>=1&&actor.Died>=t.Child.Deaths&&ReadOwedFatal(t.Child,out int entity,out string policy)&&entity==t.Observation.NativeOwnerEntity&&policy==t.Observation.Policy;}
    private static void ReceiveOwedPhysical(EntityPlayer actor,string image)
    {
        try{var t=DecodeOwedPhysical(image);if(actor?.world?.IsRemote()!=false)return;if(ReplySavedOwedTerminal(actor,t.Child))return;if(!ValidOwedPhysicalOwner(actor,t,true)||t.Observation.Policy!=ServerPolicy()){if(ValidHistoricalOwedPhysicalOwner(actor,t,true)&&CheckpointOwedPhysical(actor,t,out _)&&SaveOwedPhysical(t,true))CompleteOwedChild(actor,t.Child,out _);return;}RememberOwedBody(t);if(SaveOwedPhysical(t,true))CompleteOwedChild(actor,t.Child,out _);}catch{}
    }
    private static bool TryCompleteOwedPhysical(EntityPlayer actor,OwedChild saved,out OwedChild terminal)
    {
        terminal=null;try
        {
            if(!ReadOwedPhysical(saved.Operation,true,out var t)||!SameChild(saved,t.Child)||!ValidHistoricalOwedPhysicalOwner(actor,t,true)||!CheckpointOwedPhysical(actor,t,out var nativeProof))return false;
            // Failed/byte-refused rows remain full immutable custody until the
            // exact successor partition publishes; no empty-body inference.
            string nestedProof=string.Empty;if(t.Overflow||t.Inputs.Any(i=>i.Failed)){if(!RetainNestedOwed(t,nativeProof,out var nested)||!NestedOwedTerminal(nested,out nestedProof))return false;}string bodyProof=string.Empty;if(t.Creation.Length!=0&&(!RebirthPlacedOriginalBodyPublication.TryPublished(saved.Operation,t.Creation,out bodyProof)||!RecordOwedBodyInvocation(t)))return false;
            string previous=ChildBody(saved);var next=DecodeChild(previous);next.Phase=t.Delivered?3:4;next.Applied=t.Delivered;next.Proof=Hash(t.Image+"|"+bodyProof+"|"+OwedFatalImage(saved,t.Observation.NativeOwnerEntity,t.Observation.Policy)+"|"+nativeProof+"|"+nestedProof);if(!SaveChild(next,true,previous)||!AdvanceOwedHead(next))return false;terminal=next;return true;
        }catch{return false;}
    }
}