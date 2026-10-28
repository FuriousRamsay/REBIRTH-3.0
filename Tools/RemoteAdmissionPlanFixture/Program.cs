using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
class ItemValue { public int type;public bool HasMetadata(string s)=>false;public void SetMetadata(string k,string v){} }
class ItemStack
{
    public int count;public ItemValue itemValue=new ItemValue();
    public bool IsEmpty()=>count==0;
    public static ItemStack Empty=>new ItemStack();
    public ItemStack Clone()=>new ItemStack{count=count,itemValue=new ItemValue{type=itemValue.type}};
    public static ItemStack[] Clone(ItemStack[] stacks)=>stacks.Select(s=>s.Clone()).ToArray();
}
struct Position {public float sqrMagnitude;public static Position operator -(Position a,Position b)=>new Position{sqrMagnitude=0};}
class Actor{public Position position;}
interface IRemoteResourceSource {ItemStack[] Slots{get;}bool[] SlotLocks{get;}Position Position{get;}}
class Source:IRemoteResourceSource
{
    public Action Read;
    public ItemStack[] Slots{get{Read?.Invoke();return new[]{new ItemStack{count=1,itemValue=new ItemValue{type=1}}};}}
    public bool[] SlotLocks=>null;public Position Position=>new Position();
}
class RemoteNpcResourceSource:Source{public object NativeActor;public RemoteNpcResourceSource(object actor){NativeActor=actor;}}
static class RemoteResourcesRuntimePolicy{public static bool Enabled=true;public static float Radius=10;}
static class RebirthRemoteResourceSourceCustody{public static bool BlocksSource(IRemoteResourceSource source)=>false;}
static class RemoteResourceEligibility{public static int Checks;public static bool CanUse(IRemoteResourceSource source,Actor actor,bool a,bool b,out string reason){Checks++;reason=null;return true;}}
static class RemoteResourceTransactions{public static bool MatchesConsumable(ItemStack stack,ItemValue value)=>stack.itemValue.type==value.type;}
static class RebirthCamperInventorySnapshot{public static byte[] Capture(ItemStack[] items)=>new byte[items.Length];}
static class RemoteResourceWorldIdentity
{
    public static string Key="world";public static int Reads;
    public static bool TryGet(object world,out string key){Reads++;key=Key;return key!=null;}
}
partial class AdmissionFixture
{
    const int MaximumOutcomeBytes=6*1024*1024;
    class Frame {public IRemoteResourceSource Source;public ItemStack[] Before,After,Paid;public bool[] Locks;}
    readonly object world=new object();readonly string worldId="world";
    readonly Actor actor=new Actor();readonly ItemStack[] requirements=new[]{new ItemStack{count=4,itemValue=new ItemValue{type=1}}};
    public bool ActorCurrent=true;
    private bool CurrentActor()=>ActorCurrent;
    public int Run(IEnumerable<IRemoteResourceSource> sources)=>Plan(sources).Count;
}
class Program
{
    static int checks;
    static void Check(bool c,string s){if(!c)throw new Exception(s);checks++;Console.WriteLine("PASS "+s);}
    static void Main()
    {
        var sources=Enumerable.Range(0,4).Select(i=>new Source()).ToArray();var f=new AdmissionFixture();
        Check(f.Run(sources)==4,"four-source plan retained all required payments");
        Check(RemoteResourceWorldIdentity.Reads==2,"four candidates perform two boundary identity reads");
        Check(RemoteResourceEligibility.Checks==4,"each candidate retains source eligibility check");
        RemoteResourceWorldIdentity.Reads=0;sources[2].Read=()=>RemoteResourceWorldIdentity.Key="changed";
        bool refused=false;try{f.Run(sources);}catch(InvalidDataException){refused=true;}
        Check(refused&&RemoteResourceWorldIdentity.Reads==2,"identity changed during enumeration rejects completed plan");
        RemoteResourceWorldIdentity.Key="world";sources[2].Read=()=>f.ActorCurrent=false;
        refused=false;try{f.Run(sources);}catch(InvalidDataException){refused=true;}
        Check(refused,"actor invalidation during enumeration rejects plan");
        f.ActorCurrent=true;RemoteResourceWorldIdentity.Key=null;RemoteResourceEligibility.Checks=0;
        refused=false;try{f.Run(sources);}catch(InvalidDataException){refused=true;}
        Check(refused&&RemoteResourceEligibility.Checks==0,"missing identity rejects before touching sources");
        Console.WriteLine("PASS total="+checks);
    }
}
