using System.Text;
public class ItemValue{public int type,Quality,Use;public string Texture="";public SortedDictionary<string,string> Metadata=new(StringComparer.Ordinal);public static void Write(ItemValue v,PooledBinaryWriter w){w.Write(v.type);w.Write(v.Quality);w.Write(v.Use);w.Write(v.Texture);w.Write(v.Metadata.Count);foreach(var p in v.Metadata){w.Write(p.Key);w.Write(p.Value);}}public void Write(PooledBinaryWriter w)=>Write(this,w);}
class ItemStack{public int count;public ItemValue itemValue;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}}
public class PooledBinaryWriter:IDisposable{BinaryWriter w;public Encoding Encoding=Encoding.UTF8;public void SetBaseStream(Stream s){w=new(s,Encoding,true);}public void Write(int n)=>w.Write(n);public void Write(string s)=>w.Write(s);public void Flush()=>w.Flush();public void Dispose()=>w.Dispose();}
static class MemoryPools{public static Pool poolBinaryWriter=new();public class Pool{public PooledBinaryWriter AllocSync(bool b)=>new();}}
static class RebirthNativeItemConformanceReader{public static bool TryDecodeCanonicalV9(string s,out ItemValue v){v=null;return false;}} // Not invoked; no fake native decode qualification.

