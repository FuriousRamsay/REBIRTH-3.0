using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Xml.Linq;
public struct Vector3i{public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
public class WorldState{public string Guid;}
public class World{public WorldState worldState;public bool Remote;public bool IsRemote()=>Remote;}
public class EntityPlayer{public World world;public bool Dead;public bool IsDead()=>Dead;}
public class ItemValue{public int type;} public class ItemStack{public ItemValue itemValue=new();public int count=1;public string Metadata="original";public ItemStack Clone()=>new(){count=count,Metadata=Metadata,itemValue=new(){type=itemValue.type}};public bool IsEmpty()=>false;}
public class Recipe{public bool Known=true;public string craftingArea="campfire";public int craftingToolType;public string GetName()=>"recipe.test";public bool IsUnlocked(EntityPlayer p)=>Known;}
class PropertiesShim{public bool Contains(string a,string b)=>false;public string GetString(string a,string b)=>"campfire";} class Block{public PropertiesShim Properties=new();public string Name="campfire";public string GetBlockName()=>Name;}
class TileEntityWorkstation{public Block block=new();public ItemStack[] Tools=Array.Empty<ItemStack>();public ItemStack[] Input=new[]{new ItemStack(),new ItemStack()};public int InputSlotCount=1;public string[] MaterialNames=new[]{"iron"};public int[] Queue=new[]{0};public bool bDisableModifiedCheck;}
class Origin{public string CreationId;}
class Progression{public Dictionary<string,RebirthStationGridAdmission> StationPreparations=new();public Dictionary<string,RebirthStationDiscoveryAdmissionBinding> StationDiscoveryAdmissions=new();} class RebirthWorldCharacterRecord{public Progression Progression=new();public string StablePlayerKey,StablePlayerId;public Origin Origin;}
class Identity{public string StorageKey,CanonicalId;}
class GameManager{public static GameManager Instance=new();public World World;}
static class GameIO{public static string Root;public static Action Callback;public static string GetSaveGameDir(){Callback?.Invoke();return Root;}}
partial class RebirthWorldCharacterRepository{public static bool Current=true;public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>Current&&ReferenceEquals(r,RebirthStationLiveAccess.Owner);}
static class RebirthWorldCharacterService{public static Identity Identity;public static bool TryGetIdentity(EntityPlayer p,out Identity i){i=Identity;return i!=null;}}
static class RebirthStationObservationDispatcher{public static bool Authority=true;public static bool IsCurrentAuthorityThread(World w)=>Authority&&ReferenceEquals(w,GameManager.Instance.World)&&!w.Remote;}
static class RebirthStationLiveAccess{public static TileEntityWorkstation Station;public static RebirthWorldCharacterRecord Owner;public static bool Access=true;public static bool TryResolve(EntityPlayer p,Vector3i v,string creation,out TileEntityWorkstation s,out RebirthWorldCharacterRecord o){s=Station;o=Owner;return Access&&p!=null&&!p.Dead&&RebirthStationObservationDispatcher.IsCurrentAuthorityThread(p.world)&&Owner?.Origin.CreationId==creation&&RebirthWorldCharacterRepository.Current;}}
static class RebirthCraftingProgressionRegistry{public static string Hash=new string('c',64);public static string SemanticHash=>Hash;}
static class XUiM_Recipes{public static List<Recipe> Definitions=new(){new()};public static List<Recipe> GetRecipes()=>Definitions;}
public class RebirthStationGridAdmission{
 private XElement image;public string JobId,CreationId,DefinitionId;public bool IsPublicationAttempted;public static Action MaterializeCallback;
 public XElement Write()=>image!=null?new XElement(image):new("stationAdmission",new XAttribute("version",IsPublicationAttempted?2:1),IsPublicationAttempted?new XAttribute("phase","publicationAttempted"):null,new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("definition",DefinitionId),new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("block","campfire"));
 public bool TryMaterialize(IList<Recipe> d,out Recipe q,out ItemStack[] b,out ItemStack[] a){MaterializeCallback?.Invoke();q=d?.FirstOrDefault();b=a=Array.Empty<ItemStack>();return q!=null;}
 public static bool TryReadStored(XElement x,out RebirthStationGridAdmission a){a=null;if(x==null)return false;a=new(){image=new XElement(x),JobId=(string)x.Attribute("job"),CreationId=(string)x.Attribute("creation"),DefinitionId=(string)x.Attribute("definition"),IsPublicationAttempted=(string)x.Attribute("phase")=="publicationAttempted"};return a.JobId!=null;}public RebirthStationGridAdmission Clone(){TryReadStored(Write(),out var clone);return clone;}public bool SharesStation(RebirthStationGridAdmission other)=>(string)Write().Attribute("x")== (string)other.Write().Attribute("x");public static bool TryNormalizeCreation(string s,out string n)=>RebirthSurvivorRequestScope.TryNormalize(s,out n);public bool TryMarkPublicationAttempted(out RebirthStationGridAdmission attempted){attempted=null;if(IsPublicationAttempted)return false;var node=Write();node.SetAttributeValue("version",2);node.SetAttributeValue("phase","publicationAttempted");return TryReadStored(node,out attempted);}}
static class RebirthStationGridQueue{public static bool TryGetAdmittedSource(Recipe q,IList<Recipe> d,out Recipe s){s=d?.FirstOrDefault();return ReferenceEquals(q,s);}}
static class RebirthStationDiscoveryCanonicalRecipe{
 public sealed class Resolution{public string CanonicalRecipe="recipe.test",JobId,DefinitionId,KnowledgeId="knowledge.test";public bool Matches(string a,string b)=>a==CanonicalRecipe&&b==DefinitionId;}
 public static bool Resolve=true;public static string Job,Definition;public static Action Callback;public static bool TryResolve(Recipe q,IList<Recipe> d,out Resolution r){Callback?.Invoke();r=new(){JobId=Job,DefinitionId=Definition};return Resolve;}}
static class RebirthStationDiscoveryScope{public static bool Unlocked=true;public static bool IsUnlockedForDiscovery(Recipe r,EntityPlayer p)=>Unlocked;}
static class RebirthRecipeDiscoveryRules{public static bool Reading=true;public static Action Callback;public static bool Allows(EntityPlayer p,string r){Callback?.Invoke();return Reading;}}
class Evaluation{public bool IsAllowed=true;}
static class RebirthCapabilityService{public static bool Allowed=true;public static Action Callback;public static Evaluation EvaluateRecipeForDiscovery(EntityPlayer p,string s){Callback?.Invoke();return new(){IsAllowed=Allowed};}}
static class RebirthStationPreparationReservation{public static bool Acquired;public static bool TryAcquire(World w,RebirthWorldCharacterRecord o,RebirthStationGridAdmission a){Acquired=true;return true;}}
class Program{
 static int n;static void C(bool v,string name){n++;if(!v)throw new Exception(name);}
 static EntityPlayer player;static Vector3i pos=new(1,2,3);static string creation;static RebirthStationGridAdmission admission;
 static void Reset(){creation=Guid.NewGuid().ToString("N");var world=new World{worldState=new(){Guid=Guid.NewGuid().ToString("N")}};player=new(){world=world};GameManager.Instance=new(){World=world};GameIO.Root=Path.Combine(AppContext.BaseDirectory,"original-save");GameIO.Callback=null;
 RebirthStationLiveAccess.Station=new();RebirthStationLiveAccess.Owner=new(){StablePlayerKey=new string('a',64),StablePlayerId="owner",Origin=new(){CreationId=creation}};RebirthStationLiveAccess.Access=true;
 RebirthWorldCharacterService.Identity=new(){StorageKey=new string('a',64),CanonicalId="owner"};RebirthWorldCharacterRepository.Current=true;RebirthStationObservationDispatcher.Authority=true;RebirthCraftingProgressionRegistry.Hash=new string('c',64);
 admission=new(){JobId=Guid.NewGuid().ToString("N"),CreationId=creation,DefinitionId=new string('D',64)};RebirthStationGridAdmission.MaterializeCallback=null;
 RebirthStationDiscoveryCanonicalRecipe.Job=admission.JobId;RebirthStationDiscoveryCanonicalRecipe.Definition=admission.DefinitionId;RebirthStationDiscoveryCanonicalRecipe.Resolve=true;RebirthStationDiscoveryCanonicalRecipe.Callback=null;
 RebirthStationDiscoveryScope.Unlocked=true;RebirthRecipeDiscoveryRules.Reading=true;RebirthRecipeDiscoveryRules.Callback=null;RebirthCapabilityService.Allowed=true;RebirthCapabilityService.Callback=null;}
 static bool Create(out RebirthStationDiscoveryAdmissionBinding b,out RebirthStationDiscoveryAuthorityScope s)=>RebirthStationDiscoveryFrozenBindingCreator.TryCreate(player,pos,creation,admission,out b,out s);
 static void Main(){Reset();C(Create(out var binding,out var scope)&&scope.IsCurrent(),"original live creator");
 C((string)binding.Write().Element("stationDiscoveryWitness").Attribute("save")==scope.SaveDigest&&scope.SaveDigest.Length==64&&scope.PolicyDigest==RebirthCraftingProgressionRegistry.SemanticHash,"actual typed save/policy witness");
 string digest=scope.SaveDigest;string originalRoot=GameIO.Root;GameIO.Root=Path.Combine(originalRoot,"..",Path.GetFileName(originalRoot))+Path.DirectorySeparatorChar;
 C(scope.IsCurrent(),"equivalent full lexical root normalization");C(RebirthStationDiscoveryAuthorityScope.TryCapture(player,pos,creation,out var same)&&same.SaveDigest==digest,"normalized equivalent scope hash");
 var newWorld=new World{worldState=new(){Guid=player.world.worldState.Guid}};player.world=newWorld;GameManager.Instance.World=newWorld;C(!scope.IsCurrent(),"same saved guid new session refuses old receipt");
 C(RebirthStationDiscoveryAuthorityScope.TryCapture(player,pos,creation,out same)&&same.SaveDigest==digest,"new authority session same durable save hash");
 foreach(var fault in new[]{"identity","worldguid","root","policy","creation","owner","station","cache","dead","authority","invalidguid","emptyroot"}){Reset();C(Create(out binding,out scope),"capture before "+fault);
 switch(fault){case "identity":RebirthWorldCharacterService.Identity.CanonicalId="other";break;case "worldguid":player.world.worldState.Guid=Guid.NewGuid().ToString("N");break;case "root":GameIO.Root+="other";break;case "policy":RebirthCraftingProgressionRegistry.Hash=new string('d',64);break;case "creation":RebirthStationLiveAccess.Owner.Origin.CreationId=Guid.NewGuid().ToString("N");break;case "owner":RebirthStationLiveAccess.Owner=new(){StablePlayerKey=new string('a',64),StablePlayerId="owner",Origin=new(){CreationId=creation}};break;case "station":RebirthStationLiveAccess.Station=new();break;case "cache":RebirthWorldCharacterRepository.Current=false;break;case "dead":player.Dead=true;break;case "authority":RebirthStationObservationDispatcher.Authority=false;break;case "invalidguid":player.world.worldState.Guid="invalid";break;case "emptyroot":GameIO.Root="";break;}
 C(!scope.IsCurrent(),"retained scope refuses "+fault);if(new[]{"worldguid","root","policy","owner","station"}.Contains(fault))C(Create(out binding,out same)&&!ReferenceEquals(same,scope),"fresh capture never reuses retired original "+fault);else C(!Create(out binding,out same)&&binding==null&&same==null,"creator refuses "+fault);}
 foreach(var boundary in new[]{"materialize","canonical","reading","capability","rootgetter"}){Reset();Action swap=()=>RebirthWorldCharacterService.Identity=new(){StorageKey=new string('b',64),CanonicalId="other"};
 switch(boundary){case "materialize":RebirthStationGridAdmission.MaterializeCallback=swap;break;case "canonical":RebirthStationDiscoveryCanonicalRecipe.Callback=swap;break;case "reading":RebirthRecipeDiscoveryRules.Callback=swap;break;case "capability":RebirthCapabilityService.Callback=swap;break;case "rootgetter":GameIO.Callback=swap;break;}
 C(!Create(out binding,out same)&&binding==null&&same==null,"scope substitution around "+boundary);}
 foreach(var fault in new[]{"attempted","definition","job","unlocked","reading","capability"}){Reset();switch(fault){case "attempted":admission.IsPublicationAttempted=true;break;case "definition":RebirthStationDiscoveryCanonicalRecipe.Definition=new string('E',64);break;case "job":RebirthStationDiscoveryCanonicalRecipe.Job=Guid.NewGuid().ToString("N");break;case "unlocked":RebirthStationDiscoveryScope.Unlocked=false;break;case "reading":RebirthRecipeDiscoveryRules.Reading=false;break;case "capability":RebirthCapabilityService.Allowed=false;break;}
 C(!Create(out binding,out same)&&binding==null&&same==null,"original creator refuses "+fault);}
 Reset();creation="legacy-"+new string('a',64);admission.CreationId=creation;RebirthStationLiveAccess.Owner.Origin.CreationId=creation;C(Create(out binding,out scope),"legacy original authority");
 Reset();C(Create(out binding,out scope),"before final root callback");int roots=0;GameIO.Callback=()=>{if(++roots==2)RebirthWorldCharacterService.Identity=new(){StorageKey=new string('b',64),CanonicalId="other"};};
 C(!scope.IsCurrent(),"late path getter identity substitution refuses");
 Reset();GameIO.Callback=()=>{player.world=new World{worldState=new(){Guid=Guid.NewGuid().ToString("N")}};};C(!Create(out binding,out same),"reentrant root world change refuses");
 Reset();RebirthCraftingProgressionRegistry.Hash="invalid";C(!Create(out binding,out same),"invalid policy producer refuses");
 Reset();C(Create(out binding,out scope),"before alternate save creation");var oldSave=scope.SaveDigest;GameIO.Root+="-different";C(Create(out var different,out same)&&same.SaveDigest!=oldSave&&!scope.IsCurrent(),"different save hashes and old receipt cannot authenticate");
 Reset();C(Create(out binding,out scope),"pair source");C(RebirthStationDiscoveryPreparationPair.Matches(binding,admission),"pair exact original");
 C(admission.TryMarkPublicationAttempted(out var attempted)&&RebirthStationDiscoveryPreparationPair.Matches(binding,attempted),"pair original to known attempted phase");
 C(RebirthStationDiscoveryPreparationPair.TryValidateLive(player,attempted,binding,out same),"attempted validates unchanged original witness");
 var changed=attempted.Write();changed.SetAttributeValue("block","foreign");RebirthStationGridAdmission.TryReadStored(changed,out var changedAdmission);C(!RebirthStationDiscoveryPreparationPair.Matches(binding,changedAdmission),"attempted other field refuses");
 var weird=attempted.Write();weird.SetAttributeValue("version",9);RebirthStationGridAdmission.TryReadStored(weird,out changedAdmission);C(!RebirthStationDiscoveryPreparationPair.Matches(binding,changedAdmission),"future attempted refuses");
 GameIO.Root+="foreign";C(!RebirthStationDiscoveryPreparationPair.TryValidateLive(player,attempted,binding,out same),"old saved binding cannot rebind to foreign save");
 Reset();C(Create(out binding,out scope),"saved pair source");string folder=Path.Combine(AppContext.BaseDirectory,"pair-files");Directory.CreateDirectory(folder);string final=Path.Combine(folder,"final.xml");
 void SavePair(RebirthStationGridAdmission current,RebirthStationDiscoveryAdmissionBinding frozen){new XElement("fixture",new XAttribute("owner",RebirthStationLiveAccess.Owner.StablePlayerKey),new XAttribute("creation",creation),new XElement("preparations",current?.Write()),new XElement("bindings",frozen?.Write())).Save(final);RebirthWorldCharacterRepository.FinalPath=final;RebirthWorldCharacterRepository.serverAuthority=true;RebirthWorldCharacterRepository.Migrated=false;RebirthWorldCharacterRepository.ThrowRead=false;RebirthWorldCharacterRepository.OnRead=null;}
 bool Saved(RebirthStationGridAdmission a,RebirthStationDiscoveryAdmissionBinding b)=>RebirthWorldCharacterRepository.HasSavedStationDiscoveryAdmission(new(){StorageKey=RebirthStationLiveAccess.Owner.StablePlayerKey},a,b);
 SavePair(admission,binding);C(Saved(admission,binding),"actual original paired final file witness");admission.TryMarkPublicationAttempted(out attempted);SavePair(attempted,binding);C(Saved(attempted,binding),"actual attempted paired final file witness");
 SavePair(admission,binding);C(!Saved(attempted,binding),"saved phase mismatch refuses");SavePair(attempted,binding);C(!Saved(admission,binding),"saved later phase does not prove original");
 SavePair(attempted,null);C(!Saved(attempted,binding),"partial saved admission without binding");SavePair(null,binding);C(!Saved(attempted,binding),"partial binding without admission");
 SavePair(attempted,binding);RebirthWorldCharacterRepository.Migrated=true;C(!Saved(attempted,binding),"migrated pair refuses");SavePair(attempted,binding);RebirthWorldCharacterRepository.ThrowRead=true;C(!Saved(attempted,binding),"paired readfailure");
 SavePair(attempted,binding);RebirthWorldCharacterRepository.OnRead=()=>RebirthWorldCharacterRepository.serverAuthority=false;C(!Saved(attempted,binding),"authority swap during paired read");
 SavePair(attempted,binding);RebirthWorldCharacterRepository.OnRead=()=>RebirthWorldCharacterRepository.FinalPath=final+"other";C(!Saved(attempted,binding),"path swap during paired read");
 SavePair(attempted,binding);File.Copy(final,final+".bak",true);File.Delete(final);C(!Saved(attempted,binding),"backup only pair refuses");
 SavePair(attempted,binding);File.WriteAllText(final,"<broken>");C(!Saved(attempted,binding),"corrupt pair final refuses");
 Reset();C(Create(out binding,out scope),"integration original candidate");var owner=RebirthStationLiveAccess.Owner;int saves=0;
 bool Register(Func<bool> save)=>RebirthStationDiscoveryPreparationIntegration.RegisterOriginal(player,owner,admission,binding,scope,save);
 C(!Register(()=>{saves++;return false;})&&owner.Progression.StationPreparations.Count==1&&owner.Progression.StationDiscoveryAdmissions.Count==1,"uncertain callback retains BOTH exact originals");
 var aRef=owner.Progression.StationPreparations[admission.JobId];var bRef=owner.Progression.StationDiscoveryAdmissions[admission.JobId];
 C(Register(()=>{saves++;return true;})&&saves==2&&ReferenceEquals(aRef,owner.Progression.StationPreparations[admission.JobId])&&ReferenceEquals(bRef,owner.Progression.StationDiscoveryAdmissions[admission.JobId]),"retry saves same pair references");
 C(!Register(()=>throw new IOException("uncertain"))&&owner.Progression.StationPreparations.Count==1&&owner.Progression.StationDiscoveryAdmissions.Count==1,"save exception preserves pair");
 Reset();C(Create(out binding,out scope),"partial setup");owner=RebirthStationLiveAccess.Owner;owner.Progression.StationPreparations.Add(admission.JobId,admission.Clone());saves=0;
 C(!Register(()=>{saves++;return true;})&&saves==0&&owner.Progression.StationDiscoveryAdmissions.Count==0,"old unbound original cannot promote");
 Reset();C(Create(out binding,out scope),"binding partial setup");owner=RebirthStationLiveAccess.Owner;owner.Progression.StationDiscoveryAdmissions.Add(admission.JobId,binding.Clone());
 C(!Register(()=>true)&&owner.Progression.StationPreparations.Count==0,"binding-only partial refuses repair");
 Reset();C(Create(out binding,out scope),"callback changed root setup");owner=RebirthStationLiveAccess.Owner;
 C(!Register(()=>{GameIO.Root+="foreign";return true;})&&owner.Progression.StationPreparations.Count==1&&owner.Progression.StationDiscoveryAdmissions.Count==1,"save context change no positive retains pair");
 Reset();C(Create(out binding,out scope),"callback replaced setup");owner=RebirthStationLiveAccess.Owner;
 C(!Register(()=>{owner.Progression.StationDiscoveryAdmissions[admission.JobId]=binding.Clone();return true;}),"save entry replacement refuses positive");
 Reset();C(Create(out binding,out scope),"normal gated setup");owner=RebirthStationLiveAccess.Owner;var normal=new Recipe{Known=true};
 C(RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,admission,normal),"ordinary known native unlock preserved");normal.Known=false;
 C(!RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,admission,normal),"ordinary unknown unbound cannot bypass");
 C(Register(()=>true)&&RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,admission,normal),"explicit bound discovery unknown uses discovery scope");
 Reset();C(Create(out binding,out scope),"classification setup");owner=RebirthStationLiveAccess.Owner;
 bool Classify(out RebirthStationSavedPreparationIntent typed)=>RebirthWorldCharacterRepository.TryGetSavedStationPreparationIntent(new(){StorageKey=owner.StablePlayerKey},admission,out typed);
 SavePair(null,null);C(Classify(out var typed)&&typed.Kind==RebirthStationSavedIntentKind.NewOriginal,"new job only valid same owner final absence");
 SavePair(admission,null);C(Classify(out typed)&&typed.Kind==RebirthStationSavedIntentKind.Ordinary&&typed.MatchesCached(owner,admission),"known phase legacy ordinary classification");
 admission.TryMarkPublicationAttempted(out attempted);C(RebirthWorldCharacterRepository.TryGetSavedStationPreparationIntent(new(){StorageKey=owner.StablePlayerKey},attempted,out typed)&&typed.Kind==RebirthStationSavedIntentKind.Ordinary,"ordinary known phase attempt transition classification");
 SavePair(admission,binding);owner.Progression.StationPreparations[admission.JobId]=admission.Clone();owner.Progression.StationDiscoveryAdmissions[admission.JobId]=binding.Clone();
 C(Classify(out typed)&&typed.Kind==RebirthStationSavedIntentKind.Discovery&&typed.MatchesCached(owner,admission),"durable discovery original classification");
 owner.Progression.StationDiscoveryAdmissions.Remove(admission.JobId);C(!typed.MatchesCached(owner,admission),"binding removed between public calls cannot reclassify ordinary");
 C(Classify(out typed)&&typed.Kind==RebirthStationSavedIntentKind.Discovery&&!typed.MatchesCached(owner,admission),"saved discovery remains original despite missing cached binding");
 SavePair(null,binding);C(!Classify(out typed)&&typed==null,"invalid saved binding only classification no output");
 SavePair(admission,null);RebirthWorldCharacterRepository.Migrated=true;C(!Classify(out typed)&&typed==null,"migrated never proves ordinary");
 SavePair(admission,null);RebirthWorldCharacterRepository.ThrowRead=true;C(!Classify(out typed)&&typed==null,"unreadable never proves ordinary");
 SavePair(admission,null);File.Copy(final,final+".bak",true);File.Delete(final);C(!Classify(out typed)&&typed==null,"missing backup only never proves ordinary");
 SavePair(admission,null);RebirthWorldCharacterRepository.OnRead=()=>RebirthWorldCharacterRepository.FinalPath=final+"different";C(!Classify(out typed)&&typed==null,"classification current file switches false");
 Reset();C(Create(out binding,out scope),"preacquire mismatch setup");owner=RebirthStationLiveAccess.Owner;owner.Progression.StationPreparations.Add(admission.JobId,admission.Clone());RebirthStationPreparationReservation.Acquired=false;
 C(!Register(()=>true)&&!RebirthStationPreparationReservation.Acquired,"invalid partial original BEFORE new claim");
 Reset();C(Create(out binding,out scope),"preacquire capacity setup");owner=RebirthStationLiveAccess.Owner;for(int i=0;i<64;i++)owner.Progression.StationDiscoveryAdmissions.Add("invalidkey"+i,binding);RebirthStationPreparationReservation.Acquired=false;
 C(!Register(()=>true)&&!RebirthStationPreparationReservation.Acquired,"count limit BEFORE new claim");
 Reset();C(Create(out binding,out scope),"tail binding setup");owner=RebirthStationLiveAccess.Owner;SavePair(admission,binding);owner.Progression.StationPreparations[admission.JobId]=admission.Clone();owner.Progression.StationDiscoveryAdmissions[admission.JobId]=binding.Clone();C(Classify(out typed),"tail saved discovery classification");
 int calls=0;RebirthRecipeDiscoveryRules.Callback=()=>{if(++calls==2)owner.Progression.StationDiscoveryAdmissions.Remove(admission.JobId);};
 C(ActualPublicationTail.Current(player,owner,admission,new Recipe(),RebirthStationLiveAccess.Station,typed)&&!owner.Progression.StationDiscoveryAdmissions.ContainsKey(admission.JobId),"COUNTEREXAMPLE current exact tail permits binding loss in last reading callback");
 owner.Progression.StationDiscoveryAdmissions[admission.JobId]=binding.Clone();calls=0;
 C(!ActualPublicationTail.Candidate(player,owner,admission,new Recipe(),RebirthStationLiveAccess.Station,typed),"unapplied reordered candidate rejects late binding loss");
 owner.Progression.StationDiscoveryAdmissions[admission.JobId]=binding.Clone();RebirthRecipeDiscoveryRules.Callback=null;
 C(ActualPublicationTail.Candidate(player,owner,admission,new Recipe(),RebirthStationLiveAccess.Station,typed),"candidate normal unchanged discovery permitted");
 foreach(var mutation in new[]{"none","binding","station","world","worldstate","worldguid","owner","block","blockname","layout","materials","lock","physical","storage","metadata","queue","cache"}){
 Reset();C(Create(out binding,out scope),"complete guard setup "+mutation);owner=RebirthStationLiveAccess.Owner;SavePair(admission,binding);owner.Progression.StationPreparations[admission.JobId]=admission.Clone();owner.Progression.StationDiscoveryAdmissions[admission.JobId]=binding.Clone();C(Classify(out typed),"complete guard original classification "+mutation);
 var station=RebirthStationLiveAccess.Station;var frozen=new CompletePublicationTailCandidate.Frozen(player,station,owner);int readingCalls=0;
 RebirthRecipeDiscoveryRules.Callback=()=>{if(++readingCalls!=2)return;switch(mutation){
 case "binding":owner.Progression.StationDiscoveryAdmissions.Remove(admission.JobId);break;
 case "station":RebirthStationLiveAccess.Station=new();break;case "world":player.world=new(){worldState=new(){Guid=Guid.NewGuid().ToString("N")}};break;
 case "worldstate":player.world.worldState=new(){Guid=player.world.worldState.Guid};break;case "worldguid":player.world.worldState.Guid=Guid.NewGuid().ToString("N");break;
 case "owner":RebirthStationLiveAccess.Owner=new(){Origin=new(){CreationId=creation}};break;case "block":station.block=new();break;case "blockname":station.block.Name="forge";break;
 case "layout":station.InputSlotCount=2;break;case "materials":station.MaterialNames[0]="lead";break;case "lock":station.bDisableModifiedCheck=true;break;
 case "physical":station.Input[0].count++;break;case "storage":station.Input[1].count++;break;case "metadata":station.Input[1].Metadata="foreign";break;
 case "queue":station.Queue[0]=1;break;case "cache":RebirthWorldCharacterRepository.Current=false;break;}};
 bool accepted=CompletePublicationTailCandidate.Run(frozen,player,owner,admission,new Recipe(),typed);
 C(accepted==(mutation=="none"),"complete trailing guard "+mutation);C(frozen.Consumed,"consumed permission never restored "+mutation);
 }
 Console.WriteLine($"PASS {n} actual scope/creator/Witness/Binding/Scope syntax; explicit native liveaccess/admission/canonical/capability/identity/path adapters, no save/publication/grant.");
 }
}