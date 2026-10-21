using System;
using System.Reflection;

public static partial class RemoteResourceTransactions
{
    [ThreadStatic]private static ActionRemoteWrite actionRemoteWrite;
    private sealed class ActionRemoteWrite:IDisposable
    {
        internal readonly ActionRemoteClick Click;internal readonly ItemStackGrid Grid;internal readonly int Slot;internal readonly ItemStack Desired;internal readonly ActionFrozenDesired Frozen;internal ItemStack LeafArgument;
        internal bool Consumed,Observed,Ran;
        internal ActionRemoteWrite(ActionRemoteClick click,bool belt,int slot,ActionFrozenDesired desired)
        {if(actionRemoteWrite!=null)throw new InvalidOperationException("Nested electrical owner setter");Click=click;Grid=belt?click.Belt:click.Bag;Slot=slot;Frozen=desired;Desired=desired.Argument;actionRemoteWrite=this;}
        public void Dispose(){if(ReferenceEquals(actionRemoteWrite,this))actionRemoteWrite=null;}
    }
    private static bool RemoteActionDesired(ActionRemoteWrite scope,ItemStack desired)=>(ReferenceEquals(scope.Desired,desired)||
        desired!=null&&(ReferenceEquals(scope.Desired.itemValue,desired.itemValue)||scope.Frozen.Expected.IsEmpty()&&desired.IsEmpty()&&ReferenceEquals(desired.itemValue,ItemValue.None)))&&scope.Frozen.Matches(desired);
    private static bool AllowRemoteActionGrid(ItemStackGrid grid,int slot,ItemStack desired)
    {
        var scope=actionRemoteWrite;
        if(scope==null||scope.Consumed||!ReferenceEquals(scope.Grid,grid)||scope.Slot!=slot||!RemoteActionDesired(scope,desired)||!scope.Click.Current())return false;
        scope.LeafArgument=desired;scope.Consumed=true;return true;
    }
    private static void ObserveRemoteActionGrid(ItemStackGrid grid,int slot,ItemStack desired,bool ran)
    {
        var scope=actionRemoteWrite;
        if(scope==null||!ReferenceEquals(scope.Grid,grid)||scope.Slot!=slot||!ReferenceEquals(scope.LeafArgument,desired))return;
#if REBIRTH_DEBUG
        if(ran&&ThreadManager.IsMainThread()&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")ActionPaymentScenarioArgumentMutation?.Invoke(scope.Click.Operation,desired,grid.items[slot]);
#endif
        scope.Observed=true;scope.Ran=ran;
    }
    private static bool RemoteActionOwnerArguments(object target,MethodBase method,object[] args)
    {
        var scope=actionRemoteWrite;if(scope==null||scope.Consumed||args==null||args.Length<2||args[0] is not int slot||slot!=scope.Slot||!scope.Click.Current())return false;
        return scope.Frozen.Matches(scope.Desired)&&(target is Bag bag&&ReferenceEquals(bag.ItemGrid,scope.Grid)&&method.Name=="SetSlot"&&args.Length==2&&ReferenceEquals(args[1],scope.Desired)||
            target is Inventory belt&&ReferenceEquals(belt.ItemGrid,scope.Grid)&&method.Name=="SetItem"&&args.Length==3&&ReferenceEquals(args[1],scope.Desired.itemValue)&&args[2] is int count&&count==scope.Frozen.Expected.count);
    }
    private static void ObserveRemoteActionOwnerArguments(object target,MethodBase method,object[] args,bool ran)
    {
        var scope=actionRemoteWrite;
        if(scope!=null&&!ran&&!scope.Observed&&RemoteActionOwnerArguments(target,method,args)){scope.Observed=true;scope.Ran=false;}
    }
}
