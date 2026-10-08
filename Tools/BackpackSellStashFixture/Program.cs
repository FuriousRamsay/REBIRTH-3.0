using System;using System.Collections.Generic;using System.Xml.Linq;using System.IO;using System.Linq;
public class Profile{public string Kind,GearSlotId;public int GearBagSlotBonus;}
public class ItemClass{public string Name="any";public int MaxCount=10;public string GetItemName()=>Name;}
public class ItemValue{public int Type=1;public ItemClass ItemClass=new();public Dictionary<string,string> Metadata=new();public bool IsEmpty()=>Type==0;public ItemValue Clone()=>new(){Type=Type,ItemClass=new(){Name=ItemClass.Name},Metadata=new(Metadata)};public bool TryGetMetadata(string k,out string v)=>Metadata.TryGetValue(k,out v);public void SetMetadata(string k,string v){Metadata[k]=v;}public static void Write(ItemValue v,BinaryWriter w){new ItemStack{itemValue=v,count=1}.Write(w);}}
public static class RebirthSurvivorGearService{public static bool TryGetEquippedBackpackItem(EntityPlayer p,out ItemValue v){v=null;return false;}public const string BackpackSlotId="backpack";}
public static class RebirthSurvivorDefinitionRegistry{public static Dictionary<string,Profile> Profiles=new();public static bool TryGetSupportByGearItem(string id,out Profile p)=>Profiles.TryGetValue(id,out p);}
public class EntityPlayer {} public static class RebirthBackpackLibraryPolicy{public static int CapacityForBackpack(string id)=>RebirthBackpackSellStashPolicy.CapacityForBackpack(id)/10*6;public static bool IsLearningMaterial(ItemValue v)=>v!=null&&!v.IsEmpty()&&v.ItemClass!=null;}
public class ItemStack{
 public ItemValue itemValue=new();public int count;
 public ItemStack Clone()=>new(){itemValue=itemValue.Clone(),count=count};
 public bool IsEmpty()=>count==0||itemValue==null||itemValue.IsEmpty();
 public static ItemStack[] CreateArray(int n)=>Enumerable.Range(0,n).Select(_=>new ItemStack()).ToArray();
 public void Read(BinaryReader r){count=r.ReadUInt16();itemValue=new(){Type=r.ReadInt32(),ItemClass=new(){Name=r.ReadString()}};int n=r.ReadByte();for(int i=0;i<n;i++)itemValue.Metadata.Add(r.ReadString(),r.ReadString());}
 public void Write(BinaryWriter w){w.Write((ushort)count);w.Write(itemValue.Type);w.Write(itemValue.ItemClass.Name);w.Write((byte)itemValue.Metadata.Count);foreach(var p in itemValue.Metadata){w.Write(p.Key);w.Write(p.Value);}}
}
public class Switch:Stream{
 public Stream Target;public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>true;public override long Length=>Target.Length;public override long Position{get=>Target.Position;set=>Target.Position=value;}
 public override void Flush(){Target?.Flush();}public override int Read(byte[] b,int o,int n)=>Target.Read(b,o,n);public override void Write(byte[] b,int o,int n)=>Target.Write(b,o,n);public override long Seek(long n,SeekOrigin o)=>Target.Seek(n,o);public override void SetLength(long n)=>Target.SetLength(n);
}
public class Reader:BinaryReader{public Reader():base(new Switch()){}public void SetBaseStream(Stream s){((Switch)BaseStream).Target=s;}}
public class Writer:BinaryWriter{public Writer():base(new Switch()){}public void SetBaseStream(Stream s){((Switch)BaseStream).Target=s;}}
public class RP{public Reader AllocSync(bool b)=>new();}
public class WP{public Writer AllocSync(bool b)=>new();}
public static class MemoryPools{public static RP poolBinaryReader=new();public static WP poolBinaryWriter=new();}
class Program{static int checks;static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;}static void Main(){
 var xml=XDocument.Load(Path.Combine(AppContext.BaseDirectory,"profiles.xml"));foreach(var e in xml.Descendants("support_profile")){var id=(string)e.Attribute("gear_item_id");if(id==null)continue;RebirthSurvivorDefinitionRegistry.Profiles[id]=new(){Kind=(string)e.Attribute("kind"),GearSlotId=(string)e.Attribute("gear_slot_id"),GearBagSlotBonus=(int?)e.Attribute("bag_slot_bonus")??0};}
 var packs=RebirthSurvivorDefinitionRegistry.Profiles.Where(p=>p.Value.GearSlotId=="backpack").OrderBy(p=>p.Value.GearBagSlotBonus).ToArray();
 Check(packs.Length==8,"authored backpack tier count changed");for(int i=0;i<packs.Length;i++)Check(RebirthBackpackSellStashPolicy.CapacityForBackpack(packs[i].Key)==(i+1)*10,"wrong authored tier: "+packs[i].Key);
 Check(RebirthBackpackSellStashPolicy.CapacityForBackpack(null)==0&&RebirthBackpackSellStashPolicy.CapacityForBackpack("missing")==0,"no backpack has stash");
 foreach(var p in RebirthSurvivorDefinitionRegistry.Profiles.Where(p=>p.Value.GearSlotId!="backpack"))Check(RebirthBackpackSellStashPolicy.CapacityForBackpack(p.Key)==0,"non-backpack grants stash");
 Check(RebirthBackpackSellStashPolicy.IsStorableItem(new ItemValue()),"valid arbitrary item refused");
 Check(!RebirthBackpackSellStashPolicy.IsStorableItem(null)&&!RebirthBackpackSellStashPolicy.IsStorableItem(new(){Type=0})&&!RebirthBackpackSellStashPolicy.IsStorableItem(new(){ItemClass=null}),"invalid item admitted");
 var pack=new ItemValue{ItemClass=new(){Name=packs[0].Key}};pack.Metadata["rebirth.backpack.library.v1"]="theory-preserved";
 Check(RebirthBackpackSellStashContents.TryRead(pack,out var cells)&&cells.Length==10,"fresh stash wrong");
 cells[0].count=3;cells[0].itemValue.Metadata["custom"]="exact item metadata";
 Check(RebirthBackpackSellStashContents.TryWrite(pack,cells,out var stored)&&!pack.Metadata.ContainsKey(RebirthBackpackSellStashContents.MetadataKey),"write not detached");
 Check(RebirthBackpackSellStashContents.TryRead(stored,out var loaded)&&loaded[0].count==3&&loaded[0].itemValue.Metadata["custom"]=="exact item metadata","native image double roundtrip lost metadata");
 Check(stored.Metadata["rebirth.backpack.library.v1"]=="theory-preserved","theory lost");
 loaded[0].count=11;Check(!RebirthBackpackSellStashContents.TryWrite(stored,loaded,out _),"oversized stack stored");
 var bad=stored.Clone();bad.Metadata[RebirthBackpackSellStashContents.MetadataKey]+=" ";Check(!RebirthBackpackSellStashContents.TryRead(bad,out _),"noncanonical encoding accepted");
 bad=stored.Clone();bad.ItemClass.Name=packs[1].Key;Check(!RebirthBackpackSellStashContents.TryRead(bad,out _),"capacity mismatch silently truncated");
 bad=stored.Clone();var bytes=Convert.FromBase64String(bad.Metadata[RebirthBackpackSellStashContents.MetadataKey]);bad.Metadata[RebirthBackpackSellStashContents.MetadataKey]=Convert.ToBase64String(bytes.Take(bytes.Length-1).ToArray());Check(!RebirthBackpackSellStashContents.TryRead(bad,out _),"truncated stored blob accepted");
 Check(!RebirthBackpackSellStashContents.TryRead(new ItemValue(),out _),"no backpack storage accepted");
 var huge=stored.Clone();huge.Metadata["unrelated-large-data"]=new string('x',RebirthBackpackStoragePayload.MaxItemBytes);
 Check(!RebirthBackpackStoragePayload.Fits(huge),"oversized complete backpack admitted");
 Check(!RebirthBackpackSellStashContents.TryWrite(huge,cells,out var refused)&&refused==null,"stash write publishes oversized complete backpack");
 Check(huge.Metadata["unrelated-large-data"].Length==RebirthBackpackStoragePayload.MaxItemBytes,"payload refusal mutated original metadata");
 var combined=stored.Clone();combined.Metadata["rebirth.backpack.library.v1"]=new string('x',65536);combined.Metadata["other"]=new string('y',100000);
 Check(RebirthBackpackSellStashContents.TryWrite(combined,cells,out var fits)&&RebirthBackpackStoragePayload.Fits(fits),"valid combined metadata refused");
 Check(fits.Metadata["rebirth.backpack.library.v1"].Length==65536,"combined write truncated Theory metadata");
 var libraryPack=new ItemValue{ItemClass=new(){Name=packs[0].Key}};libraryPack.Metadata[RebirthBackpackSellStashContents.MetadataKey]="sale-preserved";
 Check(RebirthBackpackLibraryContents.TryRead(libraryPack,out var libraryCells),"fresh library read failed");
 libraryCells[0].count=1;libraryCells[0].itemValue.Metadata["book"]= "exact";
 Check(RebirthBackpackLibraryContents.TryWrite(libraryPack,libraryCells,out var libraryStored)&&libraryStored.Metadata[RebirthBackpackSellStashContents.MetadataKey]=="sale-preserved","library write loses Sell Stash metadata");
 libraryPack.Metadata["large"]=new string('z',RebirthBackpackStoragePayload.MaxItemBytes);
 Check(!RebirthBackpackLibraryContents.TryWrite(libraryPack,libraryCells,out var libraryRefused)&&libraryRefused==null,"library writer publishes oversized complete backpack");
 Check(!libraryPack.Metadata.ContainsKey(RebirthBackpackLibraryContents.MetadataKey),"library refusal mutates original pack");
 var creation=Guid.NewGuid();
 Check(RebirthBackpackSellStashView.TryCreate(creation,7,stored,true,out var view)&&view.Capacity==10&&view.OccupiedSlots==1&&view.TransferPending&&view.CreationId==creation.ToString("N")&&view.GearRevision==7,"sale view scope/capacity/occupied/pending fields wrong");
 Check(view.TryGetSlot(0,out var projected)&&projected.count==3&&projected.itemValue.Metadata["custom"]=="exact item metadata","sale view loses full item identity");
 projected.count=1;projected.itemValue.Metadata["custom"]="mutated";
 Check(view.TryGetSlot(0,out var freshProjected)&&freshProjected.count==3&&freshProjected.itemValue.Metadata["custom"]=="exact item metadata","returned sale view item aliases retained image");
 Check(view.TryGetDisplaySlot(0,out var shownItem,out var shownCount)&&shownItem=="any"&&shownCount==3,"sale scalar display wrong");
 Check(!view.TryGetSlot(-1,out _)&&!view.TryGetSlot(10,out _)&&!view.TryGetDisplaySlot(10,out _,out _),"sale display accepts invalid slot");
 Check(!RebirthBackpackSellStashView.TryCreate(Guid.Empty,7,stored,false,out _)&&!RebirthBackpackSellStashView.TryCreate(creation,-1,stored,false,out _),"sale display invalid scope/revision admitted");
 Check(!RebirthBackpackSellStashView.TryCreateDisplay(creation,7,packs[1].Key,false,cells,out _),"sale display capacity mismatch admitted");
 for(int i=0;i<packs.Length;i++){var tierPack=new ItemValue{ItemClass=new(){Name=packs[i].Key}};Check(RebirthBackpackSellStashView.TryCreate(creation,7,tierPack,false,out var tierView)&&tierView.Capacity==(i+1)*10,"sale view wrong authored tier "+i);}
 Check(!RebirthBackpackSellStashView.TryCreate(creation,7,new ItemValue(),false,out _),"sale view no-pack admitted");
 Console.WriteLine("PASS "+checks+" actual sell-stash policy checks against authored profiles; native item/registry are doubles. Actual contents codec also exercised with native serialization doubles; no native save/transfer/UI validation.");
}}