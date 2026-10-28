// AUTHORED_NOT_RUN. Actual source association methods; rich codec/source and grid journals are adapters.
using System;using System.IO;using System.Text;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
enum RemoteResourceSourceKind{StaticContainer=1,NpcStorage=5}enum RemoteResourceClientOperation{CookingPull}
class World{internal State worldState=new State();internal bool IsRemote()=>false;internal class State{internal string Guid="native-world";}}
class EntityPlayerLocal{internal World world=new World();}
class ItemValue{internal string Rich="A";internal ItemValue Clone()=>new ItemValue{Rich=Rich};}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue item,int count){itemValue=item;this.count=count;}internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);internal static ItemStack[] Clone(ItemStack[] items)=>items.Select(value=>value.Clone()).ToArray();internal static ItemStack Empty=>new ItemStack(new ItemValue(),0);internal static ItemStack[] CreateArray(int count)=>Enumerable.Range(0,count).Select(_=>Empty).ToArray();}
class XUiC_RebirthCookingWorkspace{internal object CustodyStation=new object();internal ItemStack[] Grid;internal ItemStack[] ReadCustodyGrid()=>ItemStack.Clone(Grid);internal void CustodyStatus(string text){}}
class RebirthCamperEnergyActorImage{internal int Count=2;internal static RebirthCamperEnergyActorImage Capture(EntityPlayerLocal player)=>new RebirthCamperEnergyActorImage();internal RebirthCamperEnergyActorImage Copy()=>new RebirthCamperEnergyActorImage{Count=Count};}
static class RebirthCookingStationLocator{internal static string Encode(object station)=>"station";}
class RemoteNpcResourceSource{internal RebirthNpcStableId NativeIdentity=new RebirthNpcStableId();}class RebirthNpcStableId{public override string ToString()=>"npc";}
static class RemoteResourceSourceDiscovery{internal static IEnumerable<object> ResolveNearby(World world,EntityPlayerLocal player,int radius)=>new[]{new RemoteNpcResourceSource()};}
static class RemoteResourcesRuntimePolicy{internal static int Radius=10;}
static class RebirthCookingNpcReturn{internal static int Capacity=3;internal static int PlanBound(RemoteNpcResourceSource source,EntityPlayerLocal player,ItemStack item,int slot,int requested,out uint revision){revision=8;return Math.Min(requested,Capacity);}}
static class RebirthCookingNativeReturn{internal static int Capacity=2;internal static string Source;internal static int Plan(EntityPlayerLocal player,string id,RemoteResourceSourceKind kind,string incarnation,int slot,ItemStack item,out XElement body){Source=id;body=new XElement("native",new XElement("before","fresh-native-before"));return Math.Min(Capacity,item.count);}}
static class RebirthCookingRemoteAdmission{internal static bool IsHeld(XUiC_RebirthCookingWorkspace workspace)=>false;}
static class RebirthNativeItemCodec{internal static string Encode(ItemValue value)=>value.Rich;}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join(";",items.Select(value=>value.count+":"+value.itemValue.Rich)));internal static ItemStack[] Restore(byte[] bytes){string text=Encoding.UTF8.GetString(bytes);return text.Length==0?new ItemStack[0]:text.Split(';').Select(value=>{var pair=value.Split(':');return new ItemStack(new ItemValue{Rich=pair[1]},int.Parse(pair[0]));}).ToArray();}}
class RebirthStablePlayerIdentity{internal string CanonicalId="owner",StorageKey="storage-owner";internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
static class RemoteResourceWorldIdentity{internal static bool TryGet(World world,out string key){key="source-world";return true;}}
class RebirthCamperTransferJournal{internal enum Phase{Prepared,Uncertain,Applied,Rejected}internal class Record{internal Guid Operation=Guid.NewGuid(),Vehicle;internal long Revision;internal string OwnerCreation="creation";internal byte[] Before,After;internal Phase State=Phase.Applied;}}
static class RebirthRemoteResourceDurableJournal
{
 internal class Record{internal string World="source-world",Owner="storage-owner",Creation="creation";internal int Phase=4;internal XElement Root;internal XElement Copy()=>new XElement(Root);}
 internal static Dictionary<Guid,Record> Rows=new Dictionary<Guid,Record>();internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>new Guid((int)request,0,0,new byte[8]);
 internal static bool TryRead(string owner,Guid operation,out Record root,out bool unknown){unknown=false;return Rows.TryGetValue(operation,out root);}
}
static class RebirthCookingGridCustody
{
 static List<RebirthCamperTransferJournal.Record> Journal=new List<RebirthCamperTransferJournal.Record>();static IEnumerable<RebirthCamperTransferJournal.Record> Records()=>Journal;
 static RebirthCamperTransferJournal.Record SourceHistoryRecord(Guid operation)=>Journal.SingleOrDefault(row=>row.Operation==operation);
 static bool Same(ItemStack a,ItemStack b)=>a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.Rich==b.itemValue.Rich);static bool Same(ItemStack[] a,ItemStack[] b)=>a.Length==b.Length&&a.Select((value,index)=>Same(value,b[index])).All(value=>value);
 // ACTUAL_ORIGIN_TYPE
 // ACTUAL_ITEMS
 // ACTUAL_ORIGINS
 // ACTUAL_SOURCE_CALLER
 // ACTUAL_RETURN
 // ACTUAL_DETACHED
 static bool CurrentOwner(EntityPlayerLocal player)=>true;static bool IsHeld(EntityPlayerLocal player)=>false;
 static Guid Key(EntityPlayerLocal player,string owner,string location)=>key;
 static int OwnerCapacity=20;static string BeganAction,BeganLocation;static XElement BeganSource;static int BeganBefore,BeganAfter,BeganGridCount,BeganOverflow;
 static int PlanPreferred(EntityPlayerLocal player,RebirthCamperEnergyActorImage actor,ItemStack item){int count=Math.Min(OwnerCapacity,item.count);actor.Count+=count;OwnerCapacity-=count;return item.count-count;}
 static bool Begin(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,string action,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,ItemStack[] gridBefore,ItemStack[] gridAfter,long fetchId=0,IList<ItemStack> fetched=null,int pickupType=-1,int sourceDeaths=-1,XElement sourceReturn=null,string detachedLocation=null,IList<ItemStack> overflow=null){BeganAction=action;BeganLocation=detachedLocation;BeganSource=sourceReturn;BeganBefore=before.Count;BeganAfter=after.Count;BeganGridCount=gridAfter.Sum(item=>item.count);BeganOverflow=overflow?.Sum(item=>item.count)??0;return true;}
 static string Grid(ItemStack[] items)=>Frame(items);
 static string Frame(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));static ItemStack[] Grid(int count,string rich="A")=>new[]{new ItemStack(new ItemValue{Rich=rich},count)};
 static Guid key=Guid.NewGuid();static long sequence;static int checks;
 static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static RebirthCamperTransferJournal.Record Add(string action,int before,int after,long fetch=0,XElement extra=null)
 {
  var body=new XElement("cookingGridOriginal",new XAttribute("action",action),new XAttribute("owner","owner"),new XAttribute("world","native-world"),new XAttribute("creation","creation"),new XElement("beforeGrid",Frame(Grid(before))),new XElement("afterGrid",Frame(Grid(after))),extra);
  if(fetch>0)body.SetAttributeValue("fetchId",fetch);byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));var row=new RebirthCamperTransferJournal.Record{Vehicle=key,Revision=++sequence,Before=bytes,After=bytes};Journal.Add(row);return row;
 }
 static void Fetch(long request,int before,int count,bool npc=true)
 {
  var row=Add("fetched",before,before+count,request);var source=new XElement("source",new XAttribute("id",npc?"N:npc":"S:1,2,3"),new XAttribute("kind",npc?5:1),new XAttribute("published",1),new XAttribute("incarnation","original-incarnation"),new XElement("before",Frame(Grid(count+2))),new XElement("after",Frame(Grid(2))),npc?new XElement("npc",new XAttribute("stable","npc")):null);
  RebirthRemoteResourceDurableJournal.Rows.Add(RebirthRemoteResourceDurableJournal.Operation("source-world","storage-owner","creation",1,(ulong)request,RemoteResourceClientOperation.CookingPull),new RebirthRemoteResourceDurableJournal.Record{Root=new XElement("root",new XAttribute("disposition","grid-owned"),new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",row.Operation.ToString("N")),new XElement("original",Encoding.UTF8.GetString(row.Before))),new XElement("sources",source))});
 }
 static void Reset(){Journal.Clear();RebirthRemoteResourceDurableJournal.Rows.Clear();sequence=0;BeganAction=null;BeganSource=null;OwnerCapacity=20;RebirthCookingNpcReturn.Capacity=3;RebirthCookingNativeReturn.Capacity=2;}
 public static void Main()
 {
  var actor=new EntityPlayerLocal();Reset();Fetch(1,0,5);var origins=SourceOrigins(actor,key);Need(origins.Count==1&&origins[0].Npc=="npc"&&origins[0].Item.count==5&&origins[0].Slot==0,"actual fetched NPC association");
  Add("external",5,2);Need(SourceOrigins(actor,key).Single().Item.count==2,"consumption reduced source entitlement");Add("pull",2,7);Need(SourceOrigins(actor,key).Single().Item.count==2,"later local same-rich input revived consumed source entitlement");
  for(int revision=0;revision<5000;revision++)Add("external",7,7);Need(SourceOrigins(actor,key).Single().Item.count==2,"long-lived station history lost original source association after4096 revisions");
  var longWorkspace=new XUiC_RebirthCookingWorkspace{Grid=Grid(7)};Need(Return(longWorkspace,actor)&&BeganAction=="source-return"&&BeganGridCount==5,"long-lived station history prevented actual source-associated return");
  Reset();Fetch(1,0,3);Fetch(2,3,4,false);origins=SourceOrigins(actor,key);Need(origins.Count==2&&origins[1].Kind==RemoteResourceSourceKind.StaticContainer&&origins[1].Incarnation=="original-incarnation"&&origins[1].SourceId=="S:1,2,3","native container source identity/incarnation lost");
  var returned=new XElement("sourceReturn",new XAttribute("origin",origins[1].Key),new XElement("items",Frame(Grid(2))));Add("source-return",7,5,extra:returned);origins=SourceOrigins(actor,key);Need(origins[0].Item.count==3&&origins[1].Item.count==2,"specific native source return retired another original source");
  Add("return",5,3);origins=SourceOrigins(actor,key);Need(origins[0].Item.count==1&&origins[1].Item.count==2,"owner fallback did not retire receipt order");
  Add("external",3,0);Need(SourceOrigins(actor,key).Count==0,"fully consumed origins retained claim");Add("pull",0,8);Need(SourceOrigins(actor,key).Count==0,"new local grid revived retired origins");
  Reset();Fetch(1,0,3);Journal.Last().State=RebirthCamperTransferJournal.Phase.Rejected;Need(SourceOrigins(actor,key).Count==0,"rejected fetched child granted source association");
  Reset();Fetch(1,0,3);Journal.Last().OwnerCreation="prior-character";Need(SourceOrigins(actor,key).Count==0,"prior-character station history blocked or granted a new-character source association");
  Reset();Fetch(1,0,3);RebirthRemoteResourceDurableJournal.Rows.Values.Single().Phase=2;bool refused=false;try{SourceOrigins(actor,key);}catch(InvalidDataException){refused=true;}Need(refused,"unsettled source root allowed return association");
  Reset();Fetch(1,0,3);RebirthRemoteResourceDurableJournal.Rows.Values.Single().Root.Element("ownerAction").SetAttributeValue("child",Guid.NewGuid().ToString("N"));refused=false;try{SourceOrigins(actor,key);}catch(InvalidDataException){refused=true;}Need(refused,"foreign source child allowed association");
  Reset();Fetch(1,0,3);Add("pull",2,5);refused=false;try{SourceOrigins(actor,key);}catch(InvalidDataException){refused=true;}Need(refused,"broken grid lineage inherited source association");
  Reset();Fetch(1,0,3);var workspace=new XUiC_RebirthCookingWorkspace{Grid=Grid(3)};Need(Return(workspace,actor)&&BeganAction=="source-return"&&(string)BeganSource.Attribute("npc")=="npc"&&BeganBefore==BeganAfter&&BeganGridCount==0,"normal local return bypassed original NPC destination");
  Reset();Fetch(1,0,3,false);workspace.Grid=Grid(3);Need(Return(workspace,actor)&&BeganAction=="source-return"&&BeganSource.Element("native")!=null&&RebirthCookingNativeReturn.Source=="S:1,2,3"&&BeganBefore==BeganAfter&&BeganGridCount==1,"normal local native source return lost partial remainder");
  Reset();Fetch(1,0,3);RebirthCookingNpcReturn.Capacity=0;workspace.Grid=Grid(3);Need(Return(workspace,actor)&&BeganAction=="return"&&BeganAfter-BeganBefore==3&&BeganGridCount==0,"unavailable original source did not use safe owner fallback");
  Reset();Fetch(1,0,3);RebirthCookingNpcReturn.Capacity=0;OwnerCapacity=0;workspace.Grid=Grid(3);Need(Return(workspace,actor)&&BeganAction=="return"&&BeganOverflow==3&&workspace.Grid[0].count==3,"full owner bypassed immutable native overflow original or debited grid before publication");
  Reset();Fetch(1,0,3);origins=SourceOrigins(actor,key);var rejected=Add("source-return",3,0,extra:new XElement("sourceReturn",new XAttribute("origin",origins[0].Key),new XAttribute("revision",8)));rejected.State=RebirthCamperTransferJournal.Phase.Rejected;workspace.Grid=Grid(3);Need(Return(workspace,actor)&&BeganAction=="return","known same-revision source refusal retried original destination instead of owner fallback");
  Reset();Need(BeginDetachedFetchedReturn(actor,9,Enumerable.Range(0,13).Select(_=>new ItemStack(new ItemValue(),1)).ToArray())&&BeganAction=="fetched"&&BeganLocation=="fetch-return:9"&&BeganBefore==BeganAfter&&BeganGridCount==13,"unplaced late fetched return bypassed detached source-associated custody");
  Need(ValidDetachedLocation("fetch-return:9")&&!ValidDetachedLocation("fetch-return:09")&&!ValidDetachedLocation("fetch-return:-1"),"detached original location identity not canonical");
  Console.WriteLine("PASS actual source association assertions="+checks+"; grid/source journals and rich native codec are adapters");
 }
}
