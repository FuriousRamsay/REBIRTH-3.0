// AUTHORED_NOT_RUN. Actual complete local Load/Read/ToPlayer observer and
// actual native receipt reader. Native method execution, PDS/queue/equipment
// codecs, UI, native disk save and owner identity are explicit adapters.
enum StreamModeRead{Persistency,FromServer}
class PooledBinaryReader:BinaryReader{internal PooledBinaryReader(Stream stream):base(stream,System.Text.Encoding.UTF8,true){}}
class EntityBuffs{internal const int Version=3;}
public static partial class RebirthRecipeOutputIntegration
{
 private static bool installed=true; // Explicit bootstrap readiness adapter.
 // Cold adoption/queued-terminal production is independently composed by
 // mp028_personal_cold_adoption; this Load cohort grants neither permission.
 private static bool TryAdoptPersonalOutputCold(UiOutputLoadedOwner source,Guid operation)=>false;
 private static bool TryCompletePersonalOutputLoadedQueue(UiOutputLoadedOwner source,Guid operation)=>false;
 private static UiOutputLoadedOwner LoadFresh(int variant,out PlayerDataFile data,out EntityPlayerLocal actor)
 {
  var original=Fresh(out data);actor=original.Actor;
  Directory.CreateDirectory(Path.Combine(GameIO.GetSaveGameDir(),"rebirth","recipe-outputs","ui"));
  data.Native=new byte[]{(byte)'t',(byte)'t',(byte)'p',0,PlayerDataFile.cFileVersion,9,8};
  data.buffData=new MemoryStream();using(var writer=new BinaryWriter(data.buffData,Encoding.UTF8,true))
  {writer.Write((byte)3);writer.Write((ushort)0);writer.Write((ushort)1);writer.Write(PersonalOutputReceipt(original.Operation));writer.Write(variant==10?0f:1f);writer.Flush();}
  actor.Buffs.SetCustomVar(PersonalOutputReceipt(original.Operation),1,true);WriteNative(data);
  object state=EnterRecipeOutputOwnerLoad(data,GameIO.Root,variant==8?"foreign":"account",variant!=9);
  var source=state as UiOutputLoadedOwner;if(source==null)return null;
  try
  {
   if(variant==7)data.Native[0]=44;
   using(var stream=new MemoryStream(data.Native,false))using(var reader=new PooledBinaryReader(stream))
   {
    ObserveRecipeOutputOwnerOpen(Path.Combine(GameIO.Root,"account.ttp"),stream,true);stream.Position=5;
    object read;
    if(variant==3)
    {using(var other=new MemoryStream(data.Native,false))using(var foreignReader=new PooledBinaryReader(other)){other.Position=5;read=EnterRecipeOutputOwnerRead(data,foreignReader,PlayerDataFile.cFileVersion,StreamModeRead.Persistency,true);}}
    else read=EnterRecipeOutputOwnerRead(data,reader,PlayerDataFile.cFileVersion,StreamModeRead.Persistency,true);
    stream.Position=variant==2?stream.Length-1:stream.Length;
    ObserveRecipeOutputOwnerRead(reader,true,read);
    if(variant==1)File.WriteAllBytes(Path.Combine(GameIO.Root,"account.ttp"),new byte[]{99});
    ObserveRecipeOutputOwnerLoad(data,true,state);
   }
  }
  finally{EndRecipeOutputOwnerLoad(state);}
  return source;
 }
 private static void Hydrate(UiOutputLoadedOwner source,PlayerDataFile data,EntityPlayerLocal actor,StreamModeRead mode=StreamModeRead.Persistency)
 {
  object hydration=EnterRecipeOutputOwnerHydration(data,actor,mode,true);
  try{ObserveRecipeOutputOwnerHydration(data,actor,true,hydration);}finally{EndRecipeOutputOwnerHydration(hydration);}
 }
 private static void WriteColdTerminal(UiOutputLoadedOwner source,int variant)
 {
  // Explicit immutable-sidecar input adapter. Actual cold reader, native-file
  // equality, receipt parser, queue comparison and retirement execute below.
  var operation=source.Operations[0];string path=Path.Combine(source.Save,"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");
  var original=new XElement("recipeOutputOriginal");
  foreach(var pair in new[]{"version=1","operation="+operation.ToString("N"),"world="+source.World.Guid,"owner="+source.Owner,"creation="+source.Creation,
   "deaths="+source.Deaths,"entity=1","beforeCount=1","startingOwner=1","cached=false","nativeVersion=0","sink=Inventory","personalQueueIndex=0"})
  {int separator=pair.IndexOf('=');original.Add(new XAttribute(pair.Substring(0,separator),pair.Substring(separator+1)));}
  foreach(var name in new[]{"recipe","primaryBeforeEffects","outputs","bagBefore","beltBefore","equipmentBefore","personalQueueBefore"})original.Add(new XElement(name,"fixture"));
  byte[] bytes=UiOriginalBytes(original);File.WriteAllBytes(path,bytes);
  var terminal=new XElement("recipeOutputPersonalQueueSaved",new XAttribute("operation",operation.ToString("N")),new XAttribute("original",UiOriginalHash(bytes)),
   new XAttribute("owner",source.Owner),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths),new XAttribute("beforeCount",1),
   new XAttribute("afterCount",0),new XAttribute("outputPrefix",1),new XAttribute("playerFile",source.Path),new XAttribute("nativeDigest",UiOriginalHash(source.Native)),
   new XAttribute("nativeLength",source.Native.Length),new XAttribute("queueDigest",UiOriginalHash(Encoding.UTF8.GetBytes(source.Queue))),new XAttribute("personalPrefix",1));
  if(variant==1)terminal.SetAttributeValue("nativeDigest","foreign");
  if(variant==2)terminal.SetAttributeValue("original","foreign");
  if(variant==3)terminal.SetAttributeValue("queueDigest","foreign");
  if(variant==4)terminal.SetAttributeValue("personalPrefix",2);
  if(variant==5)terminal.Add(new XAttribute("grant",true));
  File.WriteAllBytes(path+".queue-owner-saved",UiOriginalBytes(terminal));
  if(variant==6)File.WriteAllText(path+".tmp","unknown");
 }
 public static void RunOwnerLoadCases()
 {
  var source=LoadFresh(0,out var data,out var actor);Hydrate(source,data,actor);
  Need(source.Loaded&&source.Hydrated&&source.Current()&&source.Operations.Length==1,"actual complete native load lineage did not qualify owner observation");
  var card=actor.PlayerUI.xui.Group.Queue.Cards[0];var operation=source.Operations[0];
  Need(!CanAdvancePersonalOutputRecovery(card),"restored active original regenerated output before reattachment");
  var neighbour=new XUiC_RecipeStack{xui=card.xui};actor.PlayerUI.xui.Group.Queue.Cards=new[]{card,neighbour};
  Need(BindPersonalOutputRecoverySlot(actor,operation,0)&&CanAdvancePersonalOutputRecovery(neighbour),"qualified slot binding blocked unrelated native queue card");
  Need(!BindPersonalOutputRecoverySlot(actor,Guid.NewGuid(),1),"foreign operation gained recovered slot binding");
  var retained=new UiOutputPending{Operation=Guid.NewGuid(),Source=new UiOutputOriginal{World=actor.world,Actor=actor}};pendingUiOutputs.Add(card,retained);
  Need(!CanAdvancePersonalOutputRecovery(card),"another retained application borrowed native original gate");
  retained.Operation=operation;Need(CanAdvancePersonalOutputRecovery(card),"exact retained original could not reach its existing continuation");
  pendingUiOutputs.Remove(card);source.Dispose();Need(!CanAdvancePersonalOutputRecovery(card),"disposed observation turned an active native marker into fresh output permission");
  source=LoadFresh(1,out data,out actor);Need(!source.Loaded&&source.Closed,"contradictory final native file qualified load");
  Hydrate(source,data,actor);Need(!CanAdvancePersonalOutputRecovery(actor.PlayerUI.xui.Group.Queue.Cards[0]),"unqualified Load lineage permitted a restored marked original to run fresh");
  source=LoadFresh(2,out data,out actor);Need(!source.Loaded&&!source.ReadReturned&&source.Closed,"incomplete native Read qualified owner load");
  source=LoadFresh(3,out data,out actor);Need(!source.ReadEntered&&!source.Loaded&&source.Closed,"unrelated reader stream qualified native load");
  source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor,StreamModeRead.FromServer);
  Need(!source.Hydrated,"nonlocal hydration mode consumed local persistency source");source.Dispose();
  source=LoadFresh(0,out data,out actor);actor.bag.ItemGrid.items[0].Value=44;Hydrate(source,data,actor);
  Need(!source.Hydrated,"different actual hydrated bag qualified original owner image");source.Dispose();
  source=LoadFresh(0,out data,out actor);foreach(var savedOperation in source.Operations)actor.Buffs.RemoveCustomVar(PersonalOutputReceipt(savedOperation));Hydrate(source,data,actor);
  Need(!source.Hydrated,"missing actual hydrated original marker qualified load");source.Dispose();
  source=LoadFresh(0,out data,out actor);data.Native[5]++;Hydrate(source,data,actor);
  Need(!source.Hydrated,"changed PDS before native ToPlayer qualified hydration");source.Dispose();
  source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor);GameManager.Instance.World=new World();SweepUiOutputLoadedOwners(GameManager.Instance.World);
  Need(source.Closed&&source.Memory==null,"replaced world retained native load residency");
  Need(LoadFresh(8,out data,out actor)==null,"foreign native account admitted local owner load");
  Need(LoadFresh(9,out data,out actor)==null,"suppressed native Load created source lineage");
  source=LoadFresh(7,out data,out actor);Need(!source.ReadEntered&&!source.Loaded&&source.Closed,"unsupported native header qualified load");
  source=LoadFresh(10,out data,out actor);Need(!source.Loaded&&source.Closed,"cleanup-only marker created active financial native load");
  for(int variant=1;variant<=6;variant++)
  {
   source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor);WriteColdTerminal(source,variant);
   Need(!TryRetirePersonalOutputColdTerminal(source,source.Operations[0])&&actor.Buffs.GetCustomVar(PersonalOutputReceipt(source.Operations[0]))==1,
    "contradictory or staged cold terminal retired original: "+variant);source.Dispose();
  }
  source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor);WriteColdTerminal(source,0);
  Need(TryRetirePersonalOutputColdTerminal(source,source.Operations[0])&&actor.Buffs.GetCustomVar(PersonalOutputReceipt(source.Operations[0]))==0,
   "exact native owner+queue saved terminal did not retire original marker");
  Need(CanAdvancePersonalOutputRecovery(actor.PlayerUI.xui.Group.Queue.Cards[0]),"positively saved cold terminal kept settled native queue blocked");source.Dispose();
  source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor);
  Need(!TryRetirePersonalOutputColdTerminal(source,source.Operations[0]),"absent complete terminal retired uncertain original");source.Dispose();
  source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor);WriteColdTerminal(source,0);actor.PlayerUI.xui.Crafting.Queue="not-restored";
  Need(!TryRetirePersonalOutputColdTerminal(source,source.Operations[0]),"ToPlayer before actual matching UI queue restoration retired original");source.Dispose();
  source=LoadFresh(0,out data,out actor);Hydrate(source,data,actor);WriteColdTerminal(source,0);actor.NativeEquipment=new byte[]{99};
  Need(!TryRetirePersonalOutputColdTerminal(source,source.Operations[0]),"different current equipment retired exact loaded original");source.Dispose();
  Console.WriteLine("MP028 actual owner Load/Read/ToPlayer, recovery gate and cold terminal: 31 assertions authored, native execution/codec/UI and sidecar input adapters remain explicit.");
 }
}
class PersonalOwnerLoadCases{static void Main()=>RebirthRecipeOutputIntegration.RunOwnerLoadCases();}
