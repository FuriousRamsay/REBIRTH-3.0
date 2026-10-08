// CODEC
namespace HarmonyLib {
[AttributeUsage(AttributeTargets.Class)]class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}
class HarmonyPrefix:Attribute{} class HarmonyFinalizer:Attribute{}
}
class GameManager{public static GameManager Instance=new GameManager();public object World=new object();}
class RegionFile{public static string ConstructFullFilePath(string d,int x,int z,string ext){return d+"/r."+x+"."+z+"."+ext;}}
public class RegionFileV2{public string fullFilePath;public void WriteData(){}}
public class RegionFileChunkSnapshot{public void Write(){}}
static class SdFile{public static byte[] Image;public static Stream Open(string p,FileMode m,FileAccess a,FileShare s){return new MemoryStream(Image);}}
// Compression adapter deliberately doubled; native Noemax codec covered separately.
static class RebirthStationChunkInflate{public static int Calls;public static bool Refuse;public static bool TryDecode(byte[] b,int max,out byte[] d){Calls++;if(Refuse){d=null;return false;}d=new byte[b.Length-8];Buffer.BlockCopy(b,8,d,0,d.Length);return true;}}
public class RebirthStationGridAdmission {
 public string JobId="job",CreationId="creation";
 public System.Xml.Linq.XElement Write(){return new System.Xml.Linq.XElement("admission",new System.Xml.Linq.XAttribute("x",16),new System.Xml.Linq.XAttribute("y",45),new System.Xml.Linq.XAttribute("z",32),new System.Xml.Linq.XAttribute("block","workbench"),new System.Xml.Linq.XAttribute("job",JobId),new System.Xml.Linq.XAttribute("creation",CreationId));}
}
static class RebirthStationSerializedContents {public static int Calls;public static bool Matches(byte[] i,byte[] q,RebirthStationGridAdmission a,string c,int x,int y,int z,string b,int owner){Calls++;return i.Length==1&&i[0]==7&&q.Length==1&&q[0]==9&&c==a.CreationId&&x==16&&y==45&&z==32&&b=="workbench"&&owner==1;}}
class Check{
 static void A(bool ok,string why){if(!ok)throw new Exception(why);}
 static byte[] Payload(){return new byte[]{116,116,99,0,47,0,0,0,7,9};}
 static void Run(bool error,bool tamper){var world=GameManager.Instance.World;var chunk=new object();var snap=new object();
 using(var request=RebirthStationSnapshotEvidence.Watch(world,chunk,1,2)){
 var f=RebirthStationSnapshotEvidence.Begin(snap,chunk,world,1,2,true);var stream=new MemoryStream(Payload());RebirthStationSnapshotEvidence.Serialized(chunk,stream);RebirthStationSnapshotEvidence.Finish(f,stream,null);
 var scope=RebirthStationPublicationEvidence.Begin(snap,"region",1,2);A(scope!=null,"publication scope");var region=new RegionFileV2{fullFilePath=RegionFile.ConstructFullFilePath("region",0,0,"7rg")};
 var payload=Payload();var w=RebirthStationPublicationEvidence.Before(region,1,2,payload.Length,payload,true);A(w!=null&&Monitor.IsEntered(region),"writer lock retained");
 SdFile.Image=new byte[16384];SdFile.Image[0]=55;SdFile.Image[1]=114;SdFile.Image[2]=103;SdFile.Image[3]=1;int loc=4096+4*(1+2*32);SdFile.Image[loc]=3;SdFile.Image[loc+3]=1;SdFile.Image[12288]=10;Buffer.BlockCopy(payload,0,SdFile.Image,12304,10);if(tamper)SdFile.Image[12313]++;
 RebirthStationPublicationEvidence.After(w,error?new Exception():null);A(!Monitor.IsEntered(region),"writer lock released");A(RebirthStationSnapshotEvidence.HasPublished(request)==(!error&&!tamper),"only exact successful readback publishes");
 RebirthStationSnapshotEvidence.Invalidate(snap);A(RebirthStationSnapshotEvidence.HasPublished(request)==(!error&&!tamper),"historical publication survives pool release");RebirthStationPublicationEvidence.End(scope);
 }}
 static void Records(){
 var a=new RebirthStationGridAdmission();var proof=new RebirthStationSnapshotEvidence.Publication(System.IO.Path.GetFullPath("save/region/r.0.0.7rg"),RebirthStationPublicationRecord.Digest(new byte[]{1}),1,2,8,2,10,2,0,0,0,0);
 RebirthStationPublicationRecord r;A(RebirthStationPublicationRecord.TryCreate("save",a,1,proof,out r),"record create");
 var node=r.Write();RebirthStationPublicationRecord copy;A(RebirthStationPublicationRecord.TryRead(node,a,out copy),"roundtrip");var textNode=r.Write();textNode.Add("unexpected");A(!RebirthStationPublicationRecord.TryRead(textNode,a,out copy),"stray record text refusal");
 foreach(var pair in new[]{new[]{"region","../outside"},new[]{"queueStart","9"},new[]{"inputLength","0"},new[]{"queuedOwner","-1"},new[]{"queuedOwner","0"},new[]{"chunkX","2"},new[]{"payload","bad"},new[]{"creation","other"},new[]{"admission","bad"}}){var bad=new System.Xml.Linq.XElement(node);bad.SetAttributeValue(pair[0],pair[1]);A(!RebirthStationPublicationRecord.TryRead(bad,a,out copy),"refuse "+pair[0]);}
 var admissions=new Dictionary<string,RebirthStationGridAdmission>{{a.JobId,a}};var records=new Dictionary<string,RebirthStationPublicationRecord>{{a.JobId,r}};
 var progression=new System.Xml.Linq.XElement("progression",RebirthStationPublicationRecord.WriteAll(records,admissions));Dictionary<string,RebirthStationPublicationRecord> loaded;
 A(RebirthStationPublicationRecord.ReadAll(progression,admissions,out loaded)&&loaded.Count==1,"collection roundtrip");progression.Element("stationPublications").Add(node);A(!RebirthStationPublicationRecord.ReadAll(progression,admissions,out loaded),"duplicate refusal");
 Console.WriteLine("PASS actual durable record create/XML roundtrip/collection and path/span/owner/chunk/digest/identity refusal; admission and native contents doubled");
 }
 static void Revalidation(){
 var a=new RebirthStationGridAdmission();var payload=Payload();
 SdFile.Image=new byte[16384];SdFile.Image[0]=55;SdFile.Image[1]=114;SdFile.Image[2]=103;SdFile.Image[3]=1;int loc=4096+4*(1+2*32);SdFile.Image[loc]=3;SdFile.Image[loc+3]=1;SdFile.Image[12288]=10;Buffer.BlockCopy(payload,0,SdFile.Image,12304,10);
 var proof=new RebirthStationSnapshotEvidence.Publication(Path.GetFullPath("save/region/r.0.0.7rg"),RebirthStationPublicationRecord.Digest(payload),1,2,8,1,9,1,0,0,0,0);RebirthStationPublicationRecord r;
 A(RebirthStationPublicationRecord.TryCreate("save",a,1,proof,out r)&&r.Revalidate("save",a),"exact payload/span revalidation");
 int calls=RebirthStationChunkInflate.Calls;SdFile.Image[12313]++;A(!r.Revalidate("save",a)&&RebirthStationChunkInflate.Calls==calls,"digest refuses before decompression");SdFile.Image[12313]--;
 RebirthStationChunkInflate.Refuse=true;A(!r.Revalidate("save",a),"decode refusal");RebirthStationChunkInflate.Refuse=false;
 var bad=r.Write();bad.SetAttributeValue("queueStart",10);RebirthStationPublicationRecord q;A(RebirthStationPublicationRecord.TryRead(bad,a,out q)&&!q.Revalidate("save",a),"decoded span out of bounds");
 bad=r.Write();bad.SetAttributeValue("queuedOwner",2);A(RebirthStationPublicationRecord.TryRead(bad,a,out q)&&!q.Revalidate("save",a),"native owner content refusal");
 a.CreationId="changed";calls=RebirthStationChunkInflate.Calls;A(!r.Revalidate("save",a)&&RebirthStationChunkInflate.Calls==calls,"admission changed before codec");
 Console.WriteLine("PASS actual record Revalidate/region parser: matching spans, digest-before-codec, decode failure, decoded bounds and native-content refusal; file/compression/admission/content adapters doubled");
 }
 static void Main(){Records();Revalidation();Run(false,false);Run(true,false);Run(false,true);Console.WriteLine("PASS actual publication/readback integration with synthetic region and doubled compression/native adapters: exact match, error/tamper refusal, lock release, pooled reset historical receipt");}
}