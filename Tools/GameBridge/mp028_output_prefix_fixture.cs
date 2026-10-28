// AUTHORED_NOT_RUN. Actual methods injected; all native/publication boundaries are doubles.
static class RebirthNativeItemCodec
{internal static string Encode(ItemValue value)=>value.type+":"+value.Metadata;}
class Recipe{internal int count=2;internal bool IsScrap;}
class UiScope{internal IReadOnlyList<ItemStack> Outputs;internal XUiC_RecipeStack Card;internal UiScope Previous;internal object Grid;internal bool Inserted,Cached,CachedContinuation,CachedPrimaryConsumed;internal Prefix Original;}
class XUiC_RecipeStack{internal PrefixUi xui=new PrefixUi();internal bool isInventoryFull;internal int CallbackCalls;internal ItemValue OriginalItem;internal Func<bool> NativeContinuation;internal bool AddItemToInventory()=>NativeContinuation();internal XUiC_WorkstationWindowGroup Window;internal T GetParentByType<T>() where T:class=>Window as T;}
class XUiC_WorkstationWindowGroup{internal XUiC_WorkstationOutputGrid Grid;internal T GetChildByType<T>() where T:class=>Grid as T;}
class XUiC_WorkstationOutputGrid{}
class PrefixUi{internal PrefixInventory PlayerInventory=new PrefixInventory();}
class PrefixInventory{internal Func<ItemStack,bool> Insert;internal bool AddItemNoPartial(ItemStack item,bool ignored)=>Insert(item);}
class Prefix
{
 internal PrefixWorld World=new PrefixWorld();
 class AdditionalOutputPrefix{internal static object Capture(object source,int index,byte[] accepted,XElement image)=>null;}
 internal bool Entered=true,Returned=true,Live=true,OutputEntered,OutputEquipmentCallbacks,RemoteCallbackFence;internal int OutputPrefix,OutputAttempt;
 internal Guid Operation=Guid.NewGuid();internal string Path="original";internal byte[] Original={1};
 internal EntityPlayerLocal Actor;internal Recipe Recipe=new Recipe();internal ItemValue Primary;internal UiScope Scope;
 internal ItemStack[] ExpectedBag,ExpectedBelt;internal ItemValue[] ExpectedEquipment;
 internal byte[] AcceptedBag,AcceptedBelt,AcceptedEquipment;
 internal bool CallbackPhysicalComplete=>OutputPrefix==Scope.Outputs.Count+1;
 internal bool CallbackOwnerCurrent()=>AcceptedOwnerCurrent();
 internal byte[] OwnerBeforeBag,OwnerBeforeBelt,OwnerBeforeEquipment;
 internal Dictionary<string,byte[]> Publications=new Dictionary<string,byte[]>();
 internal XUiC_WorkstationWindowGroup NativeWindow;
 internal bool Current()=>Live&&ReferenceEquals(Scope?.Card?.Window,NativeWindow);
 // Cross-domain owner reservations are a separate source boundary.
 static bool CanInsertOutputs(EntityPlayerLocal actor)=>true;
 static string UiOriginalHash(byte[] bytes)=>Convert.ToBase64String(bytes);
 static byte[] UiOriginalBytes(XElement image)=>Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));
 bool PublishUiOriginal(string path,byte[] bytes){if(Publications.ContainsKey(path))return false;Publications.Add(path,bytes);return true;}
 // Native owner saved-file registration is outside this insertion-only fixture.
 static void RegisterUiOutputOwnerPrefix(Prefix source,int index,byte[] bag,byte[] belt,byte[] equipment,string acceptedHash){}
 // Cached outer delivery/queue publication is outside this insertion cohort.
 static void ObserveUiCachedDelivery(Prefix source){}
 // Native per-prefix player save is covered by the separate save-purpose cohort.
 static void SaveUiOutputPersonalPrefix(Prefix source,int index){}
 static byte[] OwningGrid(ItemStack[] cells)=>Encoding.UTF8.GetBytes(string.Join("|",cells.Select(value=>value.count+":"+RebirthNativeItemCodec.Encode(value.itemValue))));
 static byte[] OwningEquipmentNative(EntityPlayerLocal actor)=>OwningGrid(actor.equipment.Cells.Select(value=>new ItemStack(value,value.IsEmpty()?0:1)).ToArray());
 static bool SameOutputGrid(ItemStack[] actual,ItemStack[] expected)=>actual!=null&&expected!=null&&OwningGrid(actual).SequenceEqual(OwningGrid(expected));
 // ACTUAL_ENTER
 // ACTUAL_ACCEPT
 // OWNER_CURRENT_SOURCE
 internal void NativeSetterDouble()
 {
  Actor.bag.ItemGrid.items=ItemStack.Clone(ExpectedBag);Actor.inventory.ItemGrid.items=ItemStack.Clone(ExpectedBelt);
  Actor.equipment.Cells=ExpectedEquipment.Select(value=>value.Clone()).ToArray();
 }
}
class PrefixWorld{internal bool IsRemote()=>false;}
static class RebirthWorkstationCompletionPipeline{
 internal static bool CanResumeAdditionalOutputPrefix(XUiC_RecipeStack card,int prefix)=>true;
 internal static void ObserveAdditionalOutputPrefix(XUiC_RecipeStack card,object prefix){}
}
class PrefixCases
{
 static UiScope ui;
 static bool CanInsertOutputs(EntityPlayerLocal actor)=>true;
 class Pending{internal bool Returned,NativeContinuationEntered,NativeCallbackEntered;internal Prefix Source;}
 // This cohort owns generic remaining insertion; the personal cold cohort
 // composes the actual callback fence and scrap dispatch separately.
 static bool IsUiOutputPersonalCard(Prefix source)=>false;
 static Dictionary<XUiC_RecipeStack,Pending> pendingUiOutputs=new Dictionary<XUiC_RecipeStack,Pending>();
 // ACTUAL_RESUME
 // ACTUAL_FINISH_RESUME
 // ACTUAL_CACHED_RESUME
 // ACTUAL_CACHED_PRIMARY
 static byte[] OwningGrid(ItemStack[] cells)=>Encoding.UTF8.GetBytes(string.Join("|",cells.Select(value=>value.count+":"+RebirthNativeItemCodec.Encode(value.itemValue))));
 static ItemStack Stack(int type,int count)=>new ItemStack(new ItemValue{type=type,Metadata="rich-"+type,ItemClass=new ItemClass()},count);
 static byte[] OwningEquipmentNative(EntityPlayerLocal actor)=>OwningGrid(actor.equipment.Cells.Select(value=>new ItemStack(value,value.IsEmpty()?0:1)).ToArray());
 static string UiOriginalHash(byte[] bytes)=>Convert.ToBase64String(bytes);
 static byte[] UiOriginalBytes(XElement image)=>Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));
 static bool PublishUiOriginal(string path,byte[] bytes){var values=ui.Original.Publications;if(values.ContainsKey(path))return false;values.Add(path,bytes);return true;}
 static bool PublishPersonalColdBinding(string path,byte[] bytes)=>PublishUiOriginal(path,bytes)||ui.Original.Publications[path].SequenceEqual(bytes);
 static Prefix Create()
 {
  var actor=new EntityPlayerLocal{bag=new Bag{ItemGrid=new Grid{items=new[]{Stack(0,0),Stack(0,0)}}},
   inventory=new Inventory{ItemGrid=new Grid{items=new[]{Stack(0,0)}},Length=1,preferredItemTypes=new int[1]},
   equipment=new Equipment{Cells=Enumerable.Range(0,13).Select(_=>new ItemValue()).ToArray(),preferredItemSlots=new int[13]}};
  var source=new Prefix{Actor=actor,Primary=Stack(1,2).itemValue,Scope=new UiScope{Outputs=new[]{Stack(2,1)},Card=new XUiC_RecipeStack()}};
  source.Scope.Original=source;
  source.OwnerBeforeBag=OwningGrid(actor.bag.ItemGrid.items);source.OwnerBeforeBelt=OwningGrid(actor.inventory.ItemGrid.items);
  source.OwnerBeforeEquipment=OwningGrid(actor.equipment.Cells.Select(value=>new ItemStack(value,value.IsEmpty()?0:1)).ToArray());
  source.Scope.Card.xui.PlayerInventory.Insert=item=>{source.NativeSetterDouble();return true;};
  // Explicit native AddItemToInventory dispatch adapter. It traverses the
  // actual existing primary-acknowledgement seam before its callback double.
  source.Scope.Card.NativeContinuation=()=>{bool accepted=false;ConsumeUiOutputCachedPrimary(source.Scope.Card.xui.PlayerInventory,new ItemStack(source.Primary,2),ref accepted);if(accepted)source.Scope.Card.CallbackCalls++;return accepted;};
  return source;
 }
 static int cases;
 static void Check(bool value,string name){if(!value)throw new Exception(name);cases++;}
 static bool Refused(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
 static void Main()
 {
  var p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));
  Check(p.Publications.ContainsKey("original.output-0.entered")&&p.OutputPrefix==0&&p.Actor.bag.ItemGrid.items[0].IsEmpty(),"entry records full plan before any native setter");
  p.NativeSetterDouble();p.ObserveOutput(0,true);
  Check(p.OutputPrefix==1&&p.Publications.ContainsKey("original.output-0.accepted"),"actual expected primary result advances one prefix");
  p.EnterOutput(1,p.Scope.Outputs[0].Clone());p.NativeSetterDouble();p.ObserveOutput(1,true);
  Check(p.OutputPrefix==2&&p.Actor.bag.ItemGrid.items[1].itemValue.type==2,"ordered additional output continues from accepted primary");
  p=Create();Check(Refused(()=>p.EnterOutput(0,Stack(3,2)))&&p.Publications.Count==0,"different primary cannot borrow original");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));
  Check(Refused(()=>p.ObserveOutput(0,true))&&p.OutputPrefix==0,"native true without expected effect cannot advance");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);p.Actor.bag.ItemGrid.items[0].count++;
  Check(Refused(()=>p.EnterOutput(1,p.Scope.Outputs[0].Clone()))&&!p.Publications.ContainsKey("original.output-1.entered"),"different current owner prefix cannot grant the next output");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));
  Check(Refused(()=>p.ObserveOutput(0,false))&&p.OutputPrefix==0,"native refusal cannot be recorded accepted");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.Live=false;
  Check(Refused(()=>p.ObserveOutput(0,true))&&p.OutputPrefix==0,"changed actual scope cannot publish acceptance");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);
  var pending=new Pending{Source=p};pendingUiOutputs.Add(p.Scope.Card,pending);
  Check(ResumeUiOutput(p.Scope.Card)&&pending.Returned&&p.OutputPrefix==2&&p.Actor.bag.ItemGrid.items[0].count==2&&ui==null,"same actual source resumes only extra after accepted primary and restores scope");
  Check(!ResumeUiOutput(p.Scope.Card),"returned application cannot reinsert a prefix before native queue observation");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);p.EnterOutput(1,p.Scope.Outputs[0].Clone());
  pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});
  Check(!ResumeUiOutput(p.Scope.Card)&&p.OutputPrefix==1&&ui==null,"unobserved entered native call never resumes");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);p.Actor.bag.ItemGrid.items[0].count++;
  pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});
  Check(!ResumeUiOutput(p.Scope.Card)&&p.OutputPrefix==1&&ui==null,"changed accepted owner state cannot resume remaining output");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);
  pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});p.Scope.Card.xui.PlayerInventory.Insert=item=>false;
  Check(Refused(()=>ResumeUiOutput(p.Scope.Card))&&!p.OutputEntered&&p.OutputAttempt==1&&p.OutputPrefix==1&&ui==null&&p.Publications.ContainsKey("original.output-1.entered.refused"),"native false with unchanged full owner saves refusal without advancing original prefix");
  p.Scope.Card.xui.PlayerInventory.Insert=item=>{p.NativeSetterDouble();return true;};
  Check(ResumeUiOutput(p.Scope.Card)&&p.OutputPrefix==2&&p.OutputAttempt==0&&p.Publications.ContainsKey("original.output-1.retry-1.entered")&&p.Actor.bag.ItemGrid.items[0].count==2,"saved refusal permits a new remaining-extra attempt without replaying primary");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.Actor.inventory.ItemGrid.items[0]=Stack(6,1);
  Check(Refused(()=>p.ObserveOutput(0,false))&&p.OutputEntered&&p.OutputAttempt==0&&!p.Publications.ContainsKey("original.output-0.entered.refused"),"native false with changed belt remains uncertain and latched");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.Publications.Add("original.output-0.entered.refused",new byte[]{99});
  Check(Refused(()=>p.ObserveOutput(0,false))&&p.OutputEntered&&p.OutputAttempt==0,"refusal publication failure cannot unlock an attempted native insertion");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));Refused(()=>p.ObserveOutput(0,false));p.EnterOutput(0,new ItemStack(p.Primary,2));Refused(()=>p.ObserveOutput(0,false));
  Check(!p.OutputEntered&&p.OutputAttempt==2&&p.OutputPrefix==0&&p.Publications.ContainsKey("original.output-0.retry-1.entered.refused"),"repeated known refusal preserves distinct create-only attempt lineage");
  pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});
  Check(ResumeUiOutput(p.Scope.Card)&&p.OutputPrefix==2&&p.Actor.bag.ItemGrid.items[0].count==2&&p.Publications.ContainsKey("original.output-0.retry-2.entered"),"positively refused primary retries original generated value then extras without repeating native effects");
  p=Create();pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});
  Check(!ResumeUiOutput(p.Scope.Card)&&p.Publications.Count==0,"no accepted prefix and no positive refusal cannot authorize first insertion recovery");
  p=Create();p.Primary.ItemClass=new ItemClassArmor{AutoEquip=false,MaxCount=1};p.EnterOutput(0,new ItemStack(p.Primary,2));
  Check(Refused(()=>p.ObserveOutput(0,false))&&p.OutputEntered&&p.OutputAttempt==0,"armor refusal cannot infer absence of native equipment callbacks from equal final owner images");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);p.Scope.Card.isInventoryFull=true;
  pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});object identity;
  Check(BeginUiOutputCachedResume(p.Scope.Card,out identity)&&ReferenceEquals(identity,p.Scope)&&ReferenceEquals(ui,p.Scope),"actual cached Update can resume retained unfinished inventory prefix");
  var cached=new ItemStack(p.Primary.Clone(),2);bool result=false;
  Check(!ConsumeUiOutputCachedPrimary(p.Scope.Card.xui.PlayerInventory,cached,ref result)&&result&&cached.count==0&&p.Actor.bag.ItemGrid.items[0].count==2&&p.Actor.bag.ItemGrid.items[1].count==1,"cached native primary is acknowledged without a second native debit or grant");
  Check(Refused(()=>ConsumeUiOutputCachedPrimary(p.Scope.Card.xui.PlayerInventory,new ItemStack(p.Primary.Clone(),2),ref result)),"cached primary acknowledgement is one-use");ui=null;
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);p.Scope.Card.isInventoryFull=true;
  p.Scope.Card.Window=new XUiC_WorkstationWindowGroup{Grid=new XUiC_WorkstationOutputGrid()};pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});
  Check(!BeginUiOutputCachedResume(p.Scope.Card,out identity)&&p.OutputPrefix==1&&ui==null,"cached native grid route cannot borrow inventory-only continuation");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);p.EnterOutput(1,p.Scope.Outputs[0].Clone());p.NativeSetterDouble();p.ObserveOutput(1,true);pendingUiOutputs.Add(p.Scope.Card,new Pending{Source=p});
  Check(ResumeUiOutput(p.Scope.Card)&&FinishUiOutputInventoryResume(p.Scope.Card)&&p.Scope.Card.CallbackCalls==1,"complete physical prefix invokes its unentered native callback once without repeating physical insertion");
  p=Create();p.Actor.bag.ItemGrid.items[0]=Stack(7,1);
  Check(Refused(()=>p.EnterOutput(0,new ItemStack(p.Primary,2)))&&p.Publications.Count==0,"first insertion cannot adopt an owner image changed after original capture");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);pending=new Pending{Source=p};pendingUiOutputs.Add(p.Scope.Card,pending);
  Check(ResumeUiOutput(p.Scope.Card)&&FinishUiOutputInventoryResume(p.Scope.Card)&&pending.Returned&&p.Scope.Card.CallbackCalls==1&&p.Actor.bag.ItemGrid.items[0].count==2&&ui==null,
   "warm remaining prefix did not finish actual native helper callback without repeating primary");
  Check(!FinishUiOutputInventoryResume(p.Scope.Card)&&pending.Returned&&p.Scope.Card.CallbackCalls==1,"returned native continuation was invoked again or lost its positive return");
  p=Create();p.EnterOutput(0,new ItemStack(p.Primary,2));p.NativeSetterDouble();p.ObserveOutput(0,true);pending=new Pending{Source=p};pendingUiOutputs.Add(p.Scope.Card,pending);ResumeUiOutput(p.Scope.Card);
  p.Scope.Card.NativeContinuation=()=>throw new InvalidOperationException("native callback failure");
  Check(Refused(()=>FinishUiOutputInventoryResume(p.Scope.Card))&&pending.NativeContinuationEntered&&!pending.Returned&&ui==null&&!p.Scope.CachedContinuation,
   "unknown native callback did not retain entered original and restore scope");
  Check(!FinishUiOutputInventoryResume(p.Scope.Card)&&!ResumeUiOutput(p.Scope.Card),"unknown callback allowed native continuation or output replay");
  Console.WriteLine("MP028 actual output-prefix methods: "+cases+" scenarios passed; native/publication adapters remain doubles.");
 }
}
