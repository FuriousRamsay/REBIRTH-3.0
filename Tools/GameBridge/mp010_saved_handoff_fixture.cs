// AUTHORED_NOT_RUN. Actual complete saved handoff partial; native saved file and journal are adapters.
using System;using System.Linq;using System.Xml.Linq;
class World{internal bool Remote;internal EntityPlayer Player;internal State worldState=new State();internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Player;}
class State{internal string Guid="native-world";}
class EntityPlayer{internal World world;internal int entityId=1,Died=2;internal bool Dead;internal string Owner="owner",Creation="creation";internal bool IsDead()=>Dead;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class RebirthOriginalActionScope{internal static bool Matches(EntityPlayer actor,string owner,string creation)=>actor.Owner==owner&&actor.Creation==creation;}
class RebirthRemoteResourceNetworkScope{internal bool Current=true;internal static RebirthRemoteResourceNetworkScope Capture(EntityPlayer actor)=>new RebirthRemoteResourceNetworkScope();}
static class RemoteResourceWorldIdentity{internal static bool TryGet(World world,out string id){id="source-world";return world!=null;}}
class Identity{internal string CanonicalId="owner";}
static class RebirthWorldCharacterService{internal static bool TryGetIdentity(EntityPlayer actor,out Identity identity){identity=new Identity{CanonicalId=actor.Owner};return true;}}
enum RemoteResourceClientOperation{CookingPull=7}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int limit){if(text.Length>limit)throw new Exception();return XElement.Parse(text);}}
class PlayerDataFile{internal int deaths=2;}
static class RebirthCamperEnergySavedOwner{internal static bool Readable=true;internal static PlayerDataFile File=new PlayerDataFile();internal static bool TryRead(Identity identity,out object image,out PlayerDataFile file){image=null;file=File;return Readable;}}
static class RebirthCookingRemoteAdmissionBackend{internal static bool Present=true,Readable=true;internal static float Phase=2;internal static string ExpectedReceipt;internal static bool NativePhase(PlayerDataFile file,string receipt,out float phase,out bool present){phase=Phase;present=Present;return Readable&&receipt==ExpectedReceipt;}}
static class RebirthRemoteResourceDurableJournal
{
 internal static Guid OperationId;internal static bool Fail,ChangeDuringPublish;internal static int Advances;
 internal sealed class Record{internal int Phase=2;internal Guid Operation;internal string Owner="owner",Creation="creation",World="source-world";internal XElement Root;internal XElement Copy()=>new XElement(Root);}
 internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)=>request==5&&world=="source-world"&&owner=="owner"&&creation=="creation"?OperationId:Guid.Empty;
 internal static string Digest(string text)=>text;
 internal static bool TryAdvance(Record original,Action<XElement> write,Func<bool> current,out Record next){Advances++;next=null;if(ChangeDuringPublish)RebirthCamperEnergySavedOwner.File.deaths++;if(Fail||!current())return false;write(original.Root);original.Phase=(int)original.Root.Attribute("phase");next=original;return true;}
}
internal static partial class RebirthRemoteResourceDurableOutcome
{
 // Rich totals/codec are explicit adapters, shared production implementation has its own cohorts.
 static string ReadSlots(XElement node)=>node.Value;static string Totals(string value)=>value;static bool SameTotals(string left,string right)=>left==right;
}
// ACTUAL_SAVED_HANDOFF
class Program
{
 static void Need(bool value,string text){if(!value)throw new Exception(text);}
 static void Main(){foreach(string action in new[]{"fetched","fetched-return"})for(int variant=0;variant<15;variant++)
 {
  var world=new World();var actor=new EntityPlayer{world=world};world.Player=actor;GameManager.Instance.World=world;ThreadManager.Main=true;
  RebirthCamperEnergySavedOwner.Readable=true;RebirthCamperEnergySavedOwner.File=new PlayerDataFile();RebirthCookingRemoteAdmissionBackend.Present=RebirthCookingRemoteAdmissionBackend.Readable=true;RebirthCookingRemoteAdmissionBackend.Phase=2;
  RebirthRemoteResourceDurableJournal.OperationId=Guid.NewGuid();RebirthRemoteResourceDurableJournal.Advances=0;RebirthRemoteResourceDurableJournal.Fail=RebirthRemoteResourceDurableJournal.ChangeDuringPublish=false;
  var child=Guid.NewGuid();RebirthCookingRemoteAdmissionBackend.ExpectedReceipt="rbCookingGrid_"+child.ToString("N");
  var image=new XElement("cookingGridOriginal",new XAttribute("version",1),new XAttribute("fetchId",5),new XAttribute("deaths",1),new XAttribute("world","native-world"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("action",action),new XElement("fetched","rich-items"),new XElement("afterActor","immutable-after"));
  var original=new RebirthRemoteResourceDurableJournal.Record{Operation=RebirthRemoteResourceDurableJournal.OperationId,Root=new XElement("source",new XAttribute("phase",2),new XAttribute("deaths",1),new XAttribute("kind",7),new XElement("removed","rich-items"),new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",child.ToString("N")),new XElement("original",image.ToString(SaveOptions.DisableFormatting))))};
  switch(variant){case 1:RebirthCamperEnergySavedOwner.Readable=false;break;case 2:RebirthCookingRemoteAdmissionBackend.Present=false;break;case 3:RebirthCookingRemoteAdmissionBackend.Phase=1;break;case 4:RebirthCamperEnergySavedOwner.File.deaths=1;break;case 5:actor.Died=0;break;case 6:actor.Owner="other";break;case 7:actor.Creation="other";break;case 8:world.Player=new EntityPlayer();break;case 9:world.Remote=true;break;case 10:original.Root.Element("removed").Value="other-rich";break;case 11:original.Root.Element("ownerAction").Remove();break;case 12:RebirthRemoteResourceDurableJournal.Fail=true;break;case 13:RebirthRemoteResourceDurableJournal.ChangeDuringPublish=true;break;case 14:original.Phase=1;break;}
  string before=original.Root.ToString();bool accepted=RebirthRemoteResourceDurableOutcome.TryCompleteSavedCookingHandoff(actor,original,out var terminal);Need(accepted==(variant==0),"saved historical handoff qualification "+action+"/"+variant);
  if(accepted)Need(terminal.Phase==4&&(string)terminal.Root.Attribute("disposition")== (action=="fetched"?"grid-owned":"refunded")&&terminal.Root.Element("ownerAction").Element("original").Value==image.ToString(SaveOptions.DisableFormatting),"only original terminal metadata advances");else Need(original.Root.ToString()==before,"failed qualification preserves full original");
 }
 Console.WriteLine("MP010 saved handoff: 60 assertions; no owner setter, source mutation or old actor installation supplied.");}
}
