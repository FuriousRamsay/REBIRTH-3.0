using System;using System.Linq;using System.IO;using System.Collections.Generic;using System.Xml.Linq;
// AUTHORED_NOT_RUN. Actual Prefix + ApplyParcelOwnerPrefix composed below.
// Explicit adapters: native frame/PDS read/save, native grid setter, durable
// storage, child ID and source-authenticated host plan. No native proof claim.
class ItemStack{internal int count;internal ItemStack Clone()=>new ItemStack{count=count};}
class ItemStackGrid{internal ItemStack[] items=new[]{new ItemStack(),new ItemStack()};}
class Buffs{internal float Phase=1;internal float GetCustomVar(string key)=>Phase;internal void SetCustomVar(string key,float phase,bool sync){Phase=phase;}}
class Bag{internal ItemStackGrid ItemGrid=new ItemStackGrid();internal int Attempts;internal bool Suppress;internal void SetSlot(int slot,ItemStack value){Attempts++;if(!Suppress&&RebirthCamperStationContentsDelivery.Consume(ItemGrid,slot,value))ItemGrid.items[slot]=value;}}
class EntityPlayerLocal{internal Bag bag=new Bag();internal Buffs Buffs=new Buffs();}
class RebirthCamperEnergyActorImage
{internal ItemStack[] Bag;internal RebirthCamperEnergyActorImage Copy()=>new RebirthCamperEnergyActorImage{Bag=Bag.Select(x=>x.Clone()).ToArray()};internal bool Matches(RebirthCamperEnergyActorImage other)=>other!=null&&Bag.Length==other.Bag.Length&&Bag.Where((x,i)=>x.count!=other.Bag[i].count).Count()==0;internal static RebirthCamperEnergyActorImage Capture(EntityPlayerLocal player)=>new RebirthCamperEnergyActorImage{Bag=player.bag.ItemGrid.items.Select(x=>x.Clone()).ToArray()};}
class RebirthCamperRemovalParcelPlan{internal string World="world",Creation="creation",Binding="binding",Receipt="receipt";internal Guid Operation=Guid.NewGuid(),Vehicle=Guid.NewGuid();internal RebirthCamperEnergyActorImage Before,Credited,Retired;internal static byte[] Bytes(XElement node)=>System.Text.Encoding.UTF8.GetBytes(node.ToString());}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count;}
static class RebirthRemoteResourceClientOriginal{internal static string ActorImage(RebirthCamperEnergyActorImage actor)=>string.Join(",",actor.Bag.Select(x=>x.count));}
static class RebirthCookingRemoteAdmissionBackend{internal static Guid StationKey(string world,string text)=>new Guid(System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)));}
class RebirthCamperTransferJournal
{
 internal enum Phase{Prepared,Uncertain,Applied}internal class Record{internal Phase State;internal byte[] Bytes;}
 internal static Dictionary<Guid,Record> Records=new Dictionary<Guid,Record>();internal RebirthCamperTransferJournal(string path){}
 internal Record Prepare(Guid child,Guid vehicle,string creation,int revision,byte[] before,byte[] after){if(Records.TryGetValue(child,out var r)){if(!r.Bytes.SequenceEqual(before))throw new InvalidDataException();return r;}return Records[child]=new Record{Bytes=before};}
 internal Record Advance(Guid op,Phase expected,Phase next){var r=Records[op];if(r.State!=expected)throw new InvalidDataException();r.State=next;return r;}
}
static class RebirthCamperStationContentsDelivery
{
 internal class OwnerDelivery{internal EntityPlayerLocal Actor=new EntityPlayerLocal();internal bool Current=true;internal string Save="save";internal RebirthCamperRemovalParcelPlan Plan;}
 class ParcelPermit{internal OwnerDelivery Owner;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;}
 static ParcelPermit parcelPermit;internal static RebirthCamperEnergyActorImage Saved;internal static float SavedPhase;internal static bool SaveWorks=true;internal static int Saves;
 static bool SavedParcelOwner(OwnerDelivery owner,out RebirthCamperEnergyActorImage actual,out float phase){actual=Saved;phase=SavedPhase;return owner.Current;}
 static void PublishParcelOwner(OwnerDelivery owner){Saves++;if(SaveWorks){Saved=RebirthCamperEnergyActorImage.Capture(owner.Actor);SavedPhase=owner.Actor.Buffs.Phase;}}
 internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value){var p=parcelPermit;if(p==null||p.Used||!p.Owner.Current||!ReferenceEquals(p.Grid,grid)||p.Slot!=slot||!ReferenceEquals(p.Value,value))return false;p.Used=true;return true;}
 // ACTUAL_CONTENTS_DELIVERY
 internal static bool Step(OwnerDelivery owner)=>ApplyParcelOwnerPrefix(owner);
 internal static bool SavedFrame(OwnerDelivery owner,RebirthCamperEnergyActorImage frame,float phase)=>SavedParcelFrame(owner.Plan,frame,phase);
}
static class Program
{
 static int checks;static RebirthCamperStationContentsDelivery.OwnerDelivery owner;
 static void Reset(bool empty=false)
 {owner=new RebirthCamperStationContentsDelivery.OwnerDelivery();var before=RebirthCamperEnergyActorImage.Capture(owner.Actor);var after=before.Copy();if(!empty){after.Bag[0].count=3;after.Bag[1].count=4;}owner.Plan=new RebirthCamperRemovalParcelPlan{Before=before,Credited=after,Retired=after.Copy()};RebirthCamperStationContentsDelivery.Saved=before.Copy();RebirthCamperStationContentsDelivery.SavedPhase=1;RebirthCamperStationContentsDelivery.SaveWorks=true;RebirthCamperStationContentsDelivery.Saves=0;RebirthCamperTransferJournal.Records.Clear();}
 static bool Step()=>RebirthCamperStationContentsDelivery.Step(owner);
 static void Check(bool condition){checks++;if(!condition)throw new Exception("Actual parcel prefix assertion "+checks);}
 static void Main()
 {
  Reset();Check(!Step()&&owner.Actor.bag.Attempts==1&&RebirthCamperStationContentsDelivery.SavedPhase==11);
  Check(!Step()&&owner.Actor.bag.Attempts==2&&RebirthCamperStationContentsDelivery.SavedPhase==12);
  Check(!Step()&&owner.Actor.bag.Attempts==2&&RebirthCamperStationContentsDelivery.SavedPhase==500);
  Check(Step()&&owner.Actor.bag.Attempts==2);
  Reset();RebirthCamperStationContentsDelivery.SaveWorks=false;Step();Check(owner.Actor.bag.Attempts==1&&RebirthCamperStationContentsDelivery.SavedPhase==1);
  RebirthCamperStationContentsDelivery.SaveWorks=true;Check(!Step()&&owner.Actor.bag.Attempts==1&&RebirthCamperStationContentsDelivery.SavedPhase==11);
  Reset();owner.Actor.bag.Suppress=true;Check(!Step()&&owner.Actor.bag.Attempts==1);
  owner.Actor.bag.Suppress=false;Check(!Step()&&owner.Actor.bag.Attempts==1);
  Reset();owner.Actor.bag.ItemGrid.items[0].count=99;Check(!Step()&&owner.Actor.bag.Attempts==0);
  Reset();RebirthCamperStationContentsDelivery.SavedPhase=11.5f;Check(!Step()&&owner.Actor.bag.Attempts==0);
  Reset();RebirthCamperStationContentsDelivery.SavedPhase=11;Check(!Step()&&owner.Actor.bag.Attempts==0);
  Reset();owner.Current=false;Check(!Step()&&owner.Actor.bag.Attempts==0);
  Reset(true);Check(!Step()&&owner.Actor.bag.Attempts==0&&RebirthCamperStationContentsDelivery.SavedPhase==500);
  Check(Step()&&owner.Actor.bag.Attempts==0);
  Reset();Check(RebirthCamperStationContentsDelivery.SavedFrame(owner,owner.Plan.Before,1));
  Check(RebirthCamperStationContentsDelivery.SavedFrame(owner,owner.Plan.Credited,500));
  Check(RebirthCamperStationContentsDelivery.SavedFrame(owner,owner.Plan.Retired,1000));
  var prefix=owner.Plan.Before.Copy();prefix.Bag[0]=owner.Plan.Credited.Bag[0].Clone();Check(RebirthCamperStationContentsDelivery.SavedFrame(owner,prefix,11));
  Check(!RebirthCamperStationContentsDelivery.SavedFrame(owner,owner.Plan.Before,500));
  Check(!RebirthCamperStationContentsDelivery.SavedFrame(owner,prefix,11.5f));
  var retired=owner.Plan.Credited.Copy();retired.Bag[0].count=9;owner.Plan.Retired=retired;
  Check(RebirthCamperStationContentsDelivery.SavedFrame(owner,retired,500));
  RebirthCamperStationContentsDelivery.Saved=retired;RebirthCamperStationContentsDelivery.SavedPhase=500;
  Check(!Step()&&owner.Actor.bag.Attempts==0);
  Console.WriteLine(checks+" actual removal owner-prefix assertions passed");
 }
}
