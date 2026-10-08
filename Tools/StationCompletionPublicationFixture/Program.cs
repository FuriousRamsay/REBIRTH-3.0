using System;using System.IO;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;using Noemax.GZip;
// Explicit engine/authority boundary adapters. Region extraction/inflate and production semantic codecs are linked unchanged.
public class ItemClass{public bool HasQuality=true;public string GetItemName()=>"glue";}
public class ItemValue{
 public int type;public int Quality=2;public ItemClass ItemClass=new ItemClass();public Dictionary<string,object> Metadata=new Dictionary<string,object>();
 public ItemValue(int t=0){type=t;}public bool IsEmpty()=>type==0;public bool HasMetadata(string k)=>Metadata.ContainsKey(k);
 public bool TryGetMetadata<T>(string k,out T v){if(Metadata.TryGetValue(k,out var x)&&x is T){v=(T)x;return true;}v=default(T);return false;}
 public void SetMetadata(string k,object x){Metadata[k]=x;}public ItemValue Clone(){var x=new ItemValue(type){Quality=Quality};foreach(var p in Metadata)x.Metadata[p.Key]=p.Value;return x;}
 public void Write(PooledWriter w){w.Write(type);w.Write(Quality);w.Write((byte)Metadata.Count);foreach(var p in Metadata.OrderBy(x=>x.Key)){w.Write(p.Key);w.Write(p.Value is int);if(p.Value is int n)w.Write(n);else w.Write((string)p.Value);}}
 public static ItemValue Read(PooledReader r){var v=new ItemValue(r.ReadInt32()){Quality=r.ReadInt32()};int n=r.ReadByte();for(int i=0;i<n;i++){var k=r.ReadString();v.Metadata[k]=r.ReadBoolean()?(object)r.ReadInt32():r.ReadString();}return v;}
}
public class ItemStack{public ItemValue itemValue;public int count;public ItemStack(){itemValue=new ItemValue();}public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);public void Write(PooledWriter w){itemValue.Write(w);w.Write(count);}public ItemStack Read(PooledReader r){itemValue=ItemValue.Read(r);count=r.ReadInt32();return this;}}
public class Recipe{public int itemValueType=7,count=2,craftExpGain=3,craftingToolType,craftingTier,unlockExpGain;public bool UseIngredientModifier,wildcardForgeCategory,wildcardCampfireCategory,materialBasedRecipe,IsLearnable,IsTrackable,isQuest,isChallenge,IsTracked;public string tooltip="",tags="",Job;public bool IsScrap;public string GetName()=>"glue";}
public class RecipeQueueItem{public Recipe Recipe;public int Multiplier=1,StartingEntityId=42,AmountToRepair;public byte Quality=2;public float CraftingTimeLeft,OneItemCraftTime;public void Write(PooledWriter w){w.Write(Recipe!=null);if(Recipe!=null){w.Write(Recipe.itemValueType);w.Write(Recipe.Job??"");}w.Write(Multiplier);}}
public class CraftCompleteData{public int CrafterEntityID,CraftExpGain,RecipeUsedCount;public ItemStack CraftedItemStack;public string RecipeName,ItemScrapped;public CraftCompleteData(){}public CraftCompleteData(int actor,ItemStack s,string name,string scrap,int xp,int used){CrafterEntityID=actor;CraftedItemStack=s;RecipeName=name;ItemScrapped=scrap;CraftExpGain=xp;RecipeUsedCount=used;}public void Write(PooledWriter w){w.Write((ushort)1);w.Write(CrafterEntityID);CraftedItemStack.Write(w);w.Write(RecipeName);w.Write(ItemScrapped);w.Write(CraftExpGain);w.Write(RecipeUsedCount);}public void Read(PooledReader r){if(r.ReadUInt16()!=1)throw new Exception();CrafterEntityID=r.ReadInt32();CraftedItemStack=new ItemStack().Read(r);RecipeName=r.ReadString();ItemScrapped=r.ReadString();CraftExpGain=r.ReadInt32();RecipeUsedCount=r.ReadInt32();}}
public class TileEntityWorkstation{public ItemStack[] Input,Output;public RecipeQueueItem[] Queue;public List<CraftCompleteData> CraftCompleteList;}
public class PooledWriter:BinaryWriter{public PooledWriter():base(new MemoryStream()){}public void SetBaseStream(Stream s){OutStream=s;}}
public class PooledReader:IDisposable{private BinaryReader r;public void SetBaseStream(Stream s){r=new BinaryReader(s);}public byte ReadByte()=>r.ReadByte();public bool ReadBoolean()=>r.ReadBoolean();public string ReadString()=>r.ReadString();public int ReadInt32()=>r.ReadInt32();public short ReadInt16()=>r.ReadInt16();public ushort ReadUInt16()=>r.ReadUInt16();public void Dispose(){r?.Dispose();}}
public class WriterPool{public PooledWriter AllocSync(bool x)=>new PooledWriter();}public class ReaderPool{public PooledReader AllocSync(bool x)=>new PooledReader();}
public static class MemoryPools{public static WriterPool poolBinaryWriter=new WriterPool();public static ReaderPool poolBinaryReader=new ReaderPool();}
public static class RebirthStationGridQueue{public static bool IsMarked(Recipe r)=>r?.Job!=null;public static bool TryGetJobId(Recipe r,out string job){job=r?.Job;return job!=null&&job!="malformed";}}
public static class RebirthSurvivorRequestScope{public static bool Matches(string a,string b)=>a==b;public static bool TryNormalize(string x,out string y){y=x;return !string.IsNullOrEmpty(x);}}
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b){if(a==null||b==null||a.count!=b.count||a.itemValue.type!=b.itemValue.type||a.itemValue.Quality!=b.itemValue.Quality||a.itemValue.Metadata.Count!=b.itemValue.Metadata.Count)return false;return a.itemValue.Metadata.All(p=>b.itemValue.Metadata.TryGetValue(p.Key,out var v)&&Equals(p.Value,v));}}
public sealed class RebirthStationGridAdmission{
 public bool IsPublicationAttempted=true;public string JobId=Guid.NewGuid().ToString("N"),CreationId=Guid.NewGuid().ToString("N"),DefinitionId=new string('A',64);public int Revision;public Recipe Bound=new Recipe();
 public XElement Write()=>new XElement("stationAdmission",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("definition",DefinitionId),new XAttribute("revision",Revision),new XAttribute("x",0),new XAttribute("y",45),new XAttribute("z",0),new XAttribute("block","workbench"));
 public RebirthStationGridAdmission Clone()=>new RebirthStationGridAdmission{JobId=JobId,CreationId=CreationId,DefinitionId=DefinitionId,Revision=Revision,Bound=Bound,IsPublicationAttempted=IsPublicationAttempted};
 public bool TryMaterialize(IList<Recipe> d,out Recipe r,out int batches,out int tier){r=Bound;batches=1;tier=2;return d!=null&&d.Contains(Bound);}
 public bool MatchesQueuedQuality(Recipe r,int m,int q)=>ReferenceEquals(r,Bound)&&m==1&&q==2;
}
public static class RebirthStationSnapshotEvidence{public const int MaximumSnapshotBytes=8*1024*1024;public sealed class Publication{public string Path,PayloadDigest;public int ChunkX,ChunkZ,InputStart,InputLength,QueueStart,QueueLength,OutputStart,OutputLength,CompletionStart,CompletionLength;}}
public static class SdFile{public static Stream Open(string path,FileMode m,FileAccess a,FileShare share)=>File.Open(path,m,a,share);}
public static class RebirthStationSerializedContents{public static bool Matches(byte[] i,byte[] q,RebirthStationGridAdmission a,string c,int x,int y,int z,string block,int owner)=>false;}
class Program{
 static int count;static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
 static byte[] Bytes(Action<PooledWriter> write){using(var s=new MemoryStream())using(var w=new PooledWriter()){w.SetBaseStream(s);write(w);w.Flush();return s.ToArray();}}
 static byte[] Pack(byte[] decoded){using(var s=new MemoryStream()){s.Write(new byte[]{116,116,99,0,47,0,0,0},0,8);var z=new DeflateOutputStream(s,3,true);z.Write(decoded,0,decoded.Length);z.Restart();return s.ToArray();}}
 static byte[] Region(byte[] payload){int sectors=(payload.Length+16+4095)/4096;var file=new byte[(3+sectors)*4096];file[0]=55;file[1]=114;file[2]=103;file[3]=1;file[4096]=3;file[4099]=(byte)sectors;Buffer.BlockCopy(BitConverter.GetBytes(payload.Length),0,file,12288,4);Buffer.BlockCopy(payload,0,file,12304,payload.Length);return file;}
 static RebirthStationSnapshotEvidence.Publication Save(string path,TileEntityWorkstation t){
 var parts=new[]{Bytes(w=>{w.Write((byte)t.Input.Length);foreach(var v in t.Input)v.Write(w);}),Bytes(w=>{w.Write((byte)t.Queue.Length);foreach(var v in t.Queue)v.Write(w);}),Bytes(w=>{w.Write((byte)t.Output.Length);foreach(var v in t.Output)v.Write(w);}),Bytes(w=>{w.Write((short)t.CraftCompleteList.Count);foreach(var v in t.CraftCompleteList)v.Write(w);})};
 var raw=parts.SelectMany(x=>x).ToArray();var payload=Pack(raw);File.WriteAllBytes(path,Region(payload));return new RebirthStationSnapshotEvidence.Publication{Path=path,PayloadDigest=RebirthStationPublicationRecord.Digest(payload),InputStart=8,InputLength=parts[0].Length,QueueStart=8+parts[0].Length,QueueLength=parts[1].Length,OutputStart=8+parts[0].Length+parts[1].Length,OutputLength=parts[2].Length,CompletionStart=8+parts[0].Length+parts[1].Length+parts[2].Length,CompletionLength=parts[3].Length};}
 static void Main(){
 string root=Path.Combine(Path.GetTempPath(),"rebirth-completion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Region"));string path=Path.Combine(root,"Region","r.0.0.7rg");
 try{
 var a=new RebirthStationGridAdmission();var definitions=new List<Recipe>{a.Bound};
 Check(RebirthStationTerminalIntent.TryCreate(a,true,42,out var intent),"original completion intent");
 var t=new TileEntityWorkstation{Input=new[]{new ItemStack(new ItemValue(2),5)},Output=new[]{new ItemStack(new ItemValue(7),2)},Queue=new[]{new RecipeQueueItem()},CraftCompleteList=new List<CraftCompleteData>()};
 var pre=Save(path,t);Check(RebirthStationPublicationRecord.TryCreate(root,a,42,pre,out var queued),"bound prior queued record");
 var original=new RecipeQueueItem{Recipe=a.Bound};Check(RebirthStationCompletionReceipt.TryCreate(a,original,new ItemValue(7),2,out var receipt),"marked original completion receipt");t.CraftCompleteList.Add(receipt);
 Check(RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out var expected),"typed post completion expectation");
 Check(expected.MatchesLive(t),"live exact frozen postimage");
 var final=Save(path,t);Check(RebirthStationCompletionPublication.TryCreate(root,a,intent,queued,expected,final,out var result),"actual temporary final region create");
 Check(result.Revalidate(root,a,intent,queued,expected),"actual compressed final readback semantic match");
 Check(RebirthStationCompletionPublication.TryRead(result.Write(),a,intent,queued,out var parsed),"strict roundtrip");
 var clone=result.Clone();var external=result.Write();external.SetAttributeValue("job","changed");Check(XNode.DeepEquals(result.Write(),clone.Write()),"detached clone/write");
 foreach(var field in new[]{"admission","intent","queued","definition","payload","inputDigest","queueDigest","outputDigest","completionDigest","creation","job"}){var n=result.Write();n.SetAttributeValue(field,"bad");Check(!RebirthStationCompletionPublication.TryRead(n,a,intent,queued,out _),"bad "+field);}
 foreach(var pair in new[]{("inputStart","0"),("queueStart",final.InputStart.ToString()),("outputLength","0"),("completionLength","262145"),("chunkX","1"),("region","../outside"),("phase","nativeCancelledPublished"),("version","2")}){var n=result.Write();n.SetAttributeValue(pair.Item1,pair.Item2);Check(!RebirthStationCompletionPublication.TryRead(n,a,intent,queued,out _),"refuse "+pair.Item1);}
 var extra=result.Write();extra.Add(new XAttribute("extra",1));Check(!RebirthStationCompletionPublication.TryRead(extra,a,intent,queued,out _),"extra schema field");
 RebirthStationTerminalIntent.TryCreate(a,false,42,out var cancel);Check(!RebirthStationCompletionPublication.TryRead(result.Write(),a,cancel,queued,out _),"cancellation cannot completion");
 t.Queue[0].Recipe=new Recipe{Job=a.JobId};Check(!RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out _),"original job still queued refused");t.Queue[0].Recipe=new Recipe{Job="malformed"};Check(!RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out _),"malformed queue marker refused");t.Queue[0].Recipe=null;
 t.CraftCompleteList.Add(receipt);Check(!RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out _),"duplicate original receipt refused");t.CraftCompleteList.RemoveAt(1);
 receipt.CrafterEntityID=43;Check(!RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out _),"foreign receipt actor refused");receipt.CrafterEntityID=42;
 t.Output[0].count++;Check(!expected.MatchesLive(t),"live output changes refused");var changed=Save(path,t);Check(!result.Revalidate(root,a,intent,queued,expected),"later changed payload refuses saved publication");Check(!RebirthStationCompletionPublication.TryCreate(root,a,intent,queued,expected,changed,out _),"new digest cannot replace frozen output semantics");t.Output[0].count--;
 Save(path,t);Check(result.Revalidate(root,a,intent,queued,expected),"exact original final file restored");
  t.Input[0].count++;var changedInput=Save(path,t);Check(!RebirthStationCompletionPublication.TryCreate(root,a,intent,queued,expected,changedInput,out _),"fresh payload hash cannot replace exact input");t.Input[0].count--;
 t.Queue[0].Multiplier++;var changedQueue=Save(path,t);Check(!RebirthStationCompletionPublication.TryCreate(root,a,intent,queued,expected,changedQueue,out _),"fresh payload hash cannot replace exact queue");t.Queue[0].Multiplier--;
 receipt.CraftExpGain++;var changedReceipt=Save(path,t);Check(!RebirthStationCompletionPublication.TryCreate(root,a,intent,queued,expected,changedReceipt,out _),"fresh payload hash cannot replace marked terminal receipt");Check(!RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out _),"receipt no longer matches admission refused");receipt.CraftExpGain--;
 t.CraftCompleteList.Clear();Check(!RebirthStationCompletionExpectation.TryCreate(t,a,intent,queued,definitions,out _),"missing completed receipt refused");t.CraftCompleteList.Add(receipt);
 Save(path,t);Check(result.Revalidate(root,a,intent,queued,expected),"all four originals restored after mutations");var published=File.ReadAllBytes(path);File.WriteAllBytes(path,published.Take(4096).ToArray());Check(!result.Revalidate(root,a,intent,queued,expected),"truncated actual region refused");File.WriteAllBytes(path,published);
 final.Path=Path.Combine(root+"-foreign","r.7rg");Check(!RebirthStationCompletionPublication.TryCreate(root,a,intent,queued,expected,final,out _),"outside save root refused");final.Path=path;
 var ads=new Dictionary<string,RebirthStationGridAdmission>{{a.JobId,a}};var ints=new Dictionary<string,RebirthStationTerminalIntent>{{a.JobId,intent}};var qs=new Dictionary<string,RebirthStationPublicationRecord>{{a.JobId,queued}};var rs=new Dictionary<string,RebirthStationCompletionPublication>{{a.JobId,result}};
 var section=RebirthStationCompletionPublication.WriteAll(rs,ads,ints,qs);
 Check(RebirthStationCompletionPublication.ReadAll(new XElement("progression",section),ads,ints,qs,out var loaded)&&loaded.Count==1,"strict collection roundtrip");
 Check(RebirthStationCompletionPublication.ReadAll(new XElement("progression"),ads,ints,qs,out loaded)&&loaded.Count==0,"old save absent section");
 Check(!RebirthStationCompletionPublication.ReadAll(new XElement("progression",section,section),ads,ints,qs,out loaded),"duplicate section refused");
 var duplicate=new XElement(section);duplicate.Add(result.Write());Check(!RebirthStationCompletionPublication.ReadAll(new XElement("progression",duplicate),ads,ints,qs,out loaded),"duplicate job refused");
 qs.Clear();Check(!RebirthStationCompletionPublication.ReadAll(new XElement("progression",section),ads,ints,qs,out loaded),"missing prior queued record refused");
 ExtractedRepository.Run(root,a,intent,queued,result);a.Revision++;Check(!result.Revalidate(root,a,intent,queued,expected),"changed admission refuses original");
 Console.WriteLine(count+" PASS actual completion/terminal/snapshot codecs and real temp region native Noemax reader; authority/engine serialization adapters explicit");
 }finally{foreach(var f in Directory.GetFiles(Path.Combine(root,"Region")))File.Delete(f);Directory.Delete(Path.Combine(root,"Region"));Directory.Delete(root);}
 }
}
