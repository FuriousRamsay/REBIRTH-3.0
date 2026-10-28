// AUTHORED_NOT_RUN. Actual cold reader and native region table parser.
// Compression, platform file opening and original scope are explicit adapters.
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Security.Cryptography;
static class GameIO {internal static string Save=Path.Combine(Path.GetTempPath(),"rebirth-cold-drop-adapter");internal static string GetSaveGameDir()=>Save;}
static class SdFile {internal static Stream Open(string path,FileMode mode,FileAccess access,FileShare share){Model.Opens++;Model.DuringOpen?.Invoke();if(Model.Missing)throw new FileNotFoundException();return new MemoryStream(Model.Region,false);}}
static class RebirthStationPublicationRecord {internal static string Digest(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
static class RebirthStationSnapshotEvidence {
    internal const int MaximumSnapshotBytes=8*1024*1024;
    internal sealed class EntityImage {internal readonly int X,Z,Bytes,Length;internal readonly long Offset,Generation;internal readonly string Digest;internal EntityImage(int x,int z,int bytes,long offset,int length,long generation,string digest){X=x;Z=z;Bytes=bytes;Offset=offset;Length=length;Generation=generation;Digest=digest;}}
    internal sealed class PublishedSnapshot {internal readonly string Path,PayloadDigest;internal readonly int ChunkX,ChunkZ;internal readonly long Generation;internal PublishedSnapshot(string path,string digest,int x,int z,long generation){Path=path;PayloadDigest=digest;ChunkX=x;ChunkZ=z;Generation=generation;}}
}
public interface IRebirthStationSnapshotByteBudget {int MaximumSnapshotBytes{get;}bool TryReserveAllocation(long bytes,out IDisposable allocation);}
class Budget:IRebirthStationSnapshotByteBudget {public int MaximumSnapshotBytes=>8*1024*1024;internal bool Available=true;internal long Live;sealed class Hold:IDisposable{internal Budget B;internal long N;public void Dispose(){if(B!=null){B.Live-=N;B=null;}}}public bool TryReserveAllocation(long n,out IDisposable allocation){allocation=null;if(!Available)return false;Live+=n;allocation=new Hold{B=this,N=n};return true;}}
static class RebirthStationChunkInflate {
    internal static bool Truncated;internal static int Limit;
    internal static bool TryDecode(byte[] payload,int maximum,out byte[] decoded){Limit=maximum;decoded=payload.Skip(8).ToArray();if(Truncated)decoded=decoded.Take(decoded.Length-1).ToArray();return decoded.Length<=maximum;}
}
static class Model {internal static byte[] Region;internal static int Opens;internal static bool Missing;internal static Action DuringOpen;}
// ACTUAL_REGION
// ACTUAL_COLD
class Program {
    static int assertions;static void Need(bool value,string label){assertions++;if(!value)throw new Exception(label);}
    static byte[] Header()=>new byte[]{116,116,99,0,47,0,0,0};
    static byte[] Image(){var bytes=new byte[24];Buffer.BlockCopy(Header(),0,bytes,0,8);Buffer.BlockCopy(BitConverter.GetBytes(2),0,bytes,8,4);Buffer.BlockCopy(BitConverter.GetBytes(3),0,bytes,16,4);bytes[20]=1;bytes[21]=2;bytes[22]=3;bytes[23]=4;return bytes;}
    static byte[] Region(byte[] payload){var file=new byte[4*4096];file[0]=55;file[1]=114;file[2]=103;file[3]=1;int location=4096+4*(2+3*32);file[location]=3;file[location+3]=1;Buffer.BlockCopy(BitConverter.GetBytes(payload.Length),0,file,3*4096,4);Buffer.BlockCopy(payload,0,file,3*4096+16,payload.Length);return file;}
    static void Main(){
        for(int variant=0;variant<16;variant++){
            GameIO.Save=Path.Combine(Path.GetTempPath(),"rebirth-cold-drop-adapter");Model.Missing=false;Model.Opens=0;Model.DuringOpen=null;RebirthStationChunkInflate.Truncated=false;
            var snapshot=Image();Model.Region=Region(snapshot);var expected=new byte[]{1,2,3,4};bool current=true;var budget=new Budget();
            string root=GameIO.Save,path=Path.Combine(root,"Region","r.0.0.7rg");
            var descriptor=RebirthCookingDropSnapshot.Describe(root,path,new RebirthStationSnapshotEvidence.EntityImage(2,3,snapshot.Length,20,4,7,RebirthStationPublicationRecord.Digest(snapshot)));
            if(variant==1)Model.Missing=true;
            if(variant==2)descriptor.SetAttributeValue("region","../foreign.7rg");
            if(variant==3)descriptor.SetAttributeValue("digest",new string('0',64));
            if(variant==4)expected[0]=9;
            if(variant==5)descriptor.SetAttributeValue("offset",8);
            if(variant==6)Model.Region[3]=2;
            if(variant==7)Model.Region[3*4096+16+4]=48;
            if(variant==8)Model.Region[3*4096+16+8]=99;
            if(variant==9)RebirthStationChunkInflate.Truncated=true;
            if(variant==10)budget.Available=false;
            if(variant==11)current=false;
            if(variant==12)Model.DuringOpen=()=>current=false;
            if(variant==13)Model.DuringOpen=()=>GameIO.Save=root+"-another-save";
            if(variant==14)descriptor.SetAttributeValue("bytes",9*1024*1024);
            if(variant==15)descriptor.SetAttributeValue("generation",0);
            bool result=RebirthCookingDropSnapshot.TryRead(root,descriptor,expected,()=>current,budget,out var proof);
            Need(result==(variant==0),"cold saved publication shape "+variant);
            Need(budget.Live==0,"cold decode allocation leaked "+variant);
            if(result)Need(proof.Path==path&&proof.ChunkX==2&&proof.ChunkZ==3&&proof.Generation==7&&proof.PayloadDigest==RebirthStationPublicationRecord.Digest(snapshot)&&RebirthStationChunkInflate.Limit==16,"cold proof not bound to exact native image/location/generation");
            if(variant==2||variant==10||variant==11||variant==14||variant==15)Need(Model.Opens==0,"refused descriptor/budget/scope opened a native file "+variant);
        }
        Console.WriteLine("MP042 actual cold native snapshot reader assertions="+assertions+"; compression/native platform IO and genuine topology NOT RUN");
    }
}
