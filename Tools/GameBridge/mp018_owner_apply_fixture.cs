// AUTHORED_NOT_RUN. Actual grant, hold, apply sequence and native write permit.
// Native setters below consume the actual permit; PDS/journal remain adapters.
internal static class NativeWrites {
 internal static int Calls;internal static bool Suppress,ThrowAfter;internal static Action Callback;
 internal static void Write(object inventory,bool bag,int index,object payload,int count,Action apply){
  Calls++;if(!RebirthGearNativeWritePermit.TryConsume(bag?RebirthGearNativeWritePermit.Kind.BagSlot:RebirthGearNativeWritePermit.Kind.BeltSlot,inventory,index,payload,count))return;
  if(!Suppress)apply();Callback?.Invoke();if(ThrowAfter)throw new IOException("native setter returned unknown");
 }
}
internal sealed partial class Bag {internal void SetSlot(int index,ItemStack stack)=>NativeWrites.Write(this,true,index,stack,stack.count,()=>ItemGrid.items[index]=stack);}
internal sealed partial class Inventory {internal void SetItem(int index,ItemValue value,int count)=>NativeWrites.Write(this,false,index,value,count,()=>ItemGrid.items[index]=new ItemStack(value,count));}
internal sealed class Buffs {internal readonly Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
internal sealed partial class EntityPlayerLocal {internal Buffs Buffs=new Buffs();}
internal enum RebirthGearOwnerReceipt {Applied=1,Applying=2}
internal static class RebirthHydrationOwnerCheckpoint {
 internal static bool Saved=true,Applying=true,Applied=true;internal static int Saves;
 internal static bool HasSavedBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>Saved;
 internal static bool HasLiveBefore(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>Saved;
 internal static bool TryPersist(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthGearOwnerReceipt stage){
  Saves++;if(!(stage==RebirthGearOwnerReceipt.Applying?Applying:Applied))return false;
  player.Buffs.Values["rbGear_"+original.Operation.ToString("N")]=(float)stage;return true;
 }
}
// PROGRAM_PREFIX
 private static RebirthHydrationTransferGrant Ready(bool belt=false,bool returning=false){
  Fresh();
  if(belt||returning){
   var item=player.bag.ItemGrid.items[3];player.bag.ItemGrid.items[3]=new ItemStack();
   if(belt)player.inventory.ItemGrid.items[1]=item;
   var hydration=returning?new Stack{Count=1,ItemData=item.Data}:new Stack();
   RebirthHydrationTransferOriginal next=null;
   Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&
    RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,null,4,hydration,!belt,belt?1:-1,out var plan)&&
    RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(player.world.Guid),"account",player.Creation,2,8,4,null,plan,out next),"alternate original unavailable");
   original=next;
   if(returning)RebirthMetabolismStateRepository.State.HydrationSlotItem=new ItemStack{count=1,Data=item.Data};
  }
  Need(RebirthHydrationOwnerHold.TryAcquire(player,original),"native original hold unavailable");
  NativeWrites.Calls=0;NativeWrites.Suppress=NativeWrites.ThrowAfter=false;NativeWrites.Callback=null;
  RebirthHydrationOwnerCheckpoint.Applying=RebirthHydrationOwnerCheckpoint.Applied=true;RebirthHydrationOwnerCheckpoint.Saves=0;
  player.Buffs.Values["rbGear_"+original.Operation.ToString("N")]=2;
  Need(RebirthHydrationTransferGrant.TryIssueLocal(player,original,out var grant),"fixture grant unavailable");return grant;
 }
 public static void Main(){
  var grant=Ready();Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&player.bag.ItemGrid.items[3].count==2&&NativeWrites.Calls==1,"exact native bag application failed");
  Need(RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty()&&RebirthHydrationOwnerHold.IsHeld(player),"owner leg committed metabolism or released joint custody");
  Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&NativeWrites.Calls==1,"applied retry repeated native debit");
  grant=Ready(true);Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&player.inventory.ItemGrid.items[1].count==2&&NativeWrites.Calls==1,"owned belt application failed");
  grant=Ready(false,true);Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&player.bag.ItemGrid.items[0].count==1&&RebirthMetabolismStateRepository.State.HydrationSlotItem.count==1,"owner return lost source before joint commit");
  grant=Ready();NativeWrites.Suppress=true;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Pending&&player.bag.ItemGrid.items[3].count==3,"suppressed setter qualified applied");
  NativeWrites.Suppress=false;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&player.bag.ItemGrid.items[3].count==2,"retained suppressed application could not retry");
  grant=Ready();NativeWrites.ThrowAfter=true;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Pending&&player.bag.ItemGrid.items[3].count==2,"unknown native outcome restored or discarded debit");
  NativeWrites.ThrowAfter=false;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&NativeWrites.Calls==1,"unknown-outcome retry repeated native debit");
  grant=Ready();RebirthHydrationOwnerCheckpoint.Applying=false;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Pending&&NativeWrites.Calls==0,"failed applying persistence reached setter");
  grant=Ready();RebirthHydrationOwnerCheckpoint.Applied=false;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Pending&&NativeWrites.Calls==1,"failed applied persistence lost native outcome");
  RebirthHydrationOwnerCheckpoint.Applied=true;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&NativeWrites.Calls==1,"applied save retry repeated mutation");
  grant=Ready();player.Buffs.Values.Clear();Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation&&NativeWrites.Calls==0,"missing receipt replayed native work");
  grant=Ready();player.bag.ItemGrid.items[3].count=99;Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation&&NativeWrites.Calls==0,"foreign affected slot overwritten");
  grant=Ready();var untouched=new ItemStack{count=1,Data=player.bag.ItemGrid.items[3].Data};player.bag.ItemGrid.items[7]=untouched;
  Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied&&ReferenceEquals(player.bag.ItemGrid.items[7],untouched),"unaffected owner slot replaced by transaction snapshot");
  grant=Ready();NativeWrites.Callback=()=>player.world.Primary=null;Need(RebirthHydrationOwnerApply.Apply(grant)!=RebirthGearOwnerApplySequence.Result.Applied&&RebirthHydrationOwnerHold.IsHeld(player),"lost scope released or qualified application");
  Console.WriteLine("PASS actual hydration owner application/grant/hold/sequence/permit; native PDS and remote topology NOT RUN");
 }
}
