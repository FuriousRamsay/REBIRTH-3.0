// Source-only live MP044 scenarios; NOT_RUN. Each case requires a fresh seeded
// disposable CodexTest world, actual authenticated owner, unowned dog, and native meat.
using System;
using System.IO;
using System.Reflection;

public static class MpCDogHireSettlementScenario
{
    static void Need(bool value,string label){if(!value)throw new InvalidOperationException(label);}
    static void Require(EntityPlayer player,EntityRebirthDogCompanion dog)
    {
        Need(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest","disposable CodexTest required");
        Need(ThreadManager.IsMainThread(),"actual native main thread required");
        Need(player!=null&&dog!=null&&ReferenceEquals(player.world,GameManager.Instance.World)&&ReferenceEquals(dog.world,player.world),"actual original native participants required");
        Need(ReferenceEquals(player.world.GetPrimaryPlayer(),player),"synchronous host-only fixture; remote/cold flows use mpC_dog_hire_live.py");
        Need(dog.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.None,"fresh unowned dog required");
        Need(Meat(player)>=RebirthDogLifecycleService.HireItemCount,"actual native hire payment required");
    }
    static int Meat(EntityPlayer player)
    {
        var item=ItemClass.GetItem(RebirthDogLifecycleService.HireItemName);
        return (player.inventory?.GetItemCount(item,false,-1,-1)??0)+(player.bag?.GetItemCount(item,-1,-1,false)??0);
    }
    static FieldInfo Hook(Type type,string name)
    {var field=type.GetField(name,BindingFlags.Static|BindingFlags.NonPublic);Need(field!=null,"actual DEBUG boundary missing "+name);return field;}
    public static void CommittedNotificationFailure(EntityPlayer player,EntityRebirthDogCompanion dog)
    {
        Require(player,dog);int meat=Meat(player);var hook=Hook(typeof(RebirthDogLifecycleService),"HireScenarioBoundary");
        hook.SetValue(null,(Action<string>)(stage=>{if(stage=="notify")throw new IOException("scenario notification failure");}));
        try{Need(RebirthDogLifecycleService.TryHire(player.world,player,dog,out var reason),"actual saved hire must survive notification exception");}
        finally{hook.SetValue(null,null);}
        Need(dog.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player&&RebirthNpcPersistenceCoordinator.LastSaveSucceeded,"ownership must be positively published");
        Need(Meat(player)==meat-RebirthDogLifecycleService.HireItemCount,"committed hire charged exactly once");
        Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out var duplicate),"duplicate normal hire refused");
        Need(Meat(player)==meat-RebirthDogLifecycleService.HireItemCount,"duplicate hire changed payment");
    }
    public static void OwnershipPublicationFailureThenRetry(EntityPlayer player,EntityRebirthDogCompanion dog,string failedDomain)
    {
        Require(player,dog);int meat=Meat(player);var hook=Hook(typeof(RebirthNpcPersistenceCoordinator),"BeforeCommitScenario");int hits=0;bool ownershipSave=false;
        var native=Hook(typeof(RebirthDogLifecycleService),"HireScenarioBoundary");native.SetValue(null,(Action<string>)(stage=>{if(stage=="before-ownership-save")ownershipSave=true;}));
        hook.SetValue(null,(Action<string>)(stage=>{if(ownershipSave&&stage==failedDomain){hits++;throw new IOException("scenario actual domain publication failure");}}));
        try
        {
            Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out var first),"actual ownership save failure must retain hire");
            Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out var second),"same failed original retry must remain pending");
            Need(hits>=2,"actual domain writer must be reached on both retries");
            Need(Meat(player)==meat-RebirthDogLifecycleService.HireItemCount,"pending retry charged twice or refunded an unresolved owner");
            Need(dog.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player,"failed save discarded paid original owner");
            Need(!RebirthDogLifecycleService.TryDismiss(player.world,player,dog,out var dismiss),"unsettled hire owner escaped custody");
        }
        finally{hook.SetValue(null,null);native.SetValue(null,null);}
        Need(RebirthDogLifecycleService.TryHire(player.world,player,dog,out var final),"same actual original must finish after real writer retry");
        Need(RebirthNpcPersistenceCoordinator.LastSaveSucceeded&&Meat(player)==meat-RebirthDogLifecycleService.HireItemCount,"positive retry changed original price");
    }
    public static void ReversalSaveFailureThenExactRefund(EntityPlayer player,EntityRebirthDogCompanion dog)
    {
        Require(player,dog);int meat=Meat(player);
        var belt=player.inventory.ItemGrid.items;var bag=player.bag.ItemGrid.items;
        var beforeBelt=Array.ConvertAll(belt,slot=>slot?.Clone());var beforeBag=Array.ConvertAll(bag,slot=>slot?.Clone());
        var native=Hook(typeof(RebirthDogLifecycleService),"HireScenarioBoundary");
        native.SetValue(null,(Action<string>)(stage=>{if(stage=="before-order-set")throw new IOException("scenario original order call refused before invocation");}));
        try{Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out var first),"order failure must retain paid original");}
        finally{native.SetValue(null,null);}
        var save=Hook(typeof(RebirthNpcPersistenceCoordinator),"BeforeCommitScenario");
        save.SetValue(null,(Action<string>)(stage=>{if(stage=="stable-identity")throw new IOException("scenario durable reversal publication failure");}));
        try
        {
            Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out var second),"reversal publication failure must retain original");
            Need(Meat(player)==meat-RebirthDogLifecycleService.HireItemCount,"runtime-only owner reversal refunded meat");
        }
        finally{save.SetValue(null,null);}
        Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out var refunded),"refunded original is a failed hire outcome");
        Need(RebirthNpcPersistenceCoordinator.LastSaveSucceeded&&dog.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.None,"refund requires actual saved reversal");
        Need(Meat(player)==meat,"exact original native payment not restored");
        for(int i=0;i<belt.Length;i++)Need(RebirthStationGridIngredients.IsSameStackSnapshot(belt[i],beforeBelt[i]),"toolbelt native metadata differs after refund "+i);
        for(int i=0;i<bag.Length;i++)Need(RebirthStationGridIngredients.IsSameStackSnapshot(bag[i],beforeBag[i]),"backpack native metadata differs after refund "+i);
    }
    // Invoke inside the actual native death notification, before the next Died
    // episode changes. originalDeaths is captured while arming the paid case.
    public static void VerifyActualDeathBeforeCounterRetains(EntityPlayer player,EntityRebirthDogCompanion dog,int originalDeaths)
    {
        Need(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ThreadManager.IsMainThread(),"disposable CodexTest native main thread required");
        Need(player!=null&&dog!=null&&ReferenceEquals(GameManager.Instance.World,player.world)&&ReferenceEquals(dog.world,player.world),"actual original native participants required");
        Need(player.IsDead()&&player.Died==originalDeaths,"actual native death before counter advance required; do not substitute a flag");
        var belt=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.inventory.ItemGrid.items));var bag=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.bag.ItemGrid.items));
        var kind=dog.RebirthRuntimeState.OwnershipKind;var owner=dog.RebirthRuntimeState.OwnerId;var order=dog.RebirthRuntimeState.Order;
        var boundary=Hook(typeof(RebirthDogLifecycleService),"HireScenarioBoundary");var prior=boundary.GetValue(null);int entries=0;boundary.SetValue(null,(Action<string>)(_=>entries++));
        try{Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out _),"dead original advanced a financial/owner permit");}
        finally{boundary.SetValue(null,prior);}
        Need(entries==0&&player.Died==originalDeaths,"dead-before-counter window entered original native settlement");
        Need(belt==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.inventory.ItemGrid.items))&&bag==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(player.bag.ItemGrid.items)),"native payment/refund changed during death gap");
        Need(kind==dog.RebirthRuntimeState.OwnershipKind&&owner==dog.RebirthRuntimeState.OwnerId&&order==dog.RebirthRuntimeState.Order,"native owner/order changed during death gap");
    }
    public static void PendingLocalHireRefusesUnrelatedNativeWriter(EntityPlayer player,EntityRebirthDogCompanion dog)
    {
        Require(player,dog);Need(player is EntityPlayerLocal,"actual local host required");
        var hook=Hook(typeof(RebirthDogLifecycleService),"HireScenarioBoundary");var prior=hook.GetValue(null);
        hook.SetValue(null,(Action<string>)(stage=>{if(stage=="before-ownership-save")throw new IOException("scenario retain paid local hire before durable ownership");}));
        try{Need(!RebirthDogLifecycleService.TryHire(player.world,player,dog,out _),"paid original must be retained");}
        finally{hook.SetValue(null,prior);}
        var belt=player.inventory.ItemGrid.items;var bag=player.bag.ItemGrid.items;
        Need(belt.Length>0&&bag.Length>0,"actual native grids required");
        var beltRef=belt[0];var bagRef=bag[0];var beltCopy=beltRef?.Clone()??ItemStack.Empty.Clone();var bagCopy=bagRef?.Clone()??ItemStack.Empty.Clone();
        string beforeBelt=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(belt)),beforeBag=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(bag));
        player.inventory.SetItem(0,beltCopy.itemValue,beltCopy.count);player.bag.SetSlot(0,bagCopy);
        Need(ReferenceEquals(belt[0],beltRef)&&ReferenceEquals(bag[0],bagRef),"unrelated native leaf writer escaped actual local hire hold");
        Need(beforeBelt==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(belt))&&beforeBag==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(bag)),"held payment image changed");
        Need(RebirthDogLifecycleService.TryHire(player.world,player,dog,out _),"same original must finish after actual hold refusal");
    }
}
