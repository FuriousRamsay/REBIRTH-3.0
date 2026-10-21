using System;
using System.Linq;
using System.Collections.Generic;

public static partial class RemoteResourceTransactions
{
    private static bool ActionRemoteImage(ActionRemoteClick click,ItemStack[] bag,ItemStack[] belt,ItemStack[] bagReferences,ItemStack[] beltReferences)
    {
        if(!click.Current()||!SameActionItems(click.Bag.items,bag)||!SameActionItems(click.Belt.items,belt)||bagReferences==null||beltReferences==null||
            bagReferences.Length!=click.Bag.items.Length||beltReferences.Length!=click.Belt.items.Length)return false;
        for(int i=0;i<bagReferences.Length;i++)if(!ReferenceEquals(bagReferences[i],click.Bag.items[i]))return false;
        for(int i=0;i<beltReferences.Length;i++)if(!ReferenceEquals(beltReferences[i],click.Belt.items[i]))return false;
        return true;
    }
    private static void PublishActionRemoteNative(ActionRemoteClick click)
    {
        if(!click.OwnerMetadataCurrent())return;
        float phase=click.Actor.Buffs.GetCustomVar(ActionOwnerMarker(click.Operation));
        if(click.Plan!=null&&!click.Plan.NoOwnerContribution&&(phase==2f||phase==3f||phase==8f))
        {if(!SaveActionClientOutcome(click,phase))return;}
        else GameManager.Instance.SaveLocalPlayerData();
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(click.Actor),false);
    }
    private static void ApplyActionRemoteGrant(ActionRemoteClick click,bool refund)
    {
        var plan=click.Plan;if(plan==null||plan.NoOwnerContribution||!click.OwnerMetadataCurrent())return;
        if(refund?click.Refunded:click.Debited||click.DebitRefused){PublishActionRemoteNative(click);return;}
        if(refund?click.RefundEntered||!click.Debited:click.DebitEntered){PublishActionRemoteNative(click);return;}
        if(!click.Current())return;
        if(!ActionRemoteImage(click,refund?plan.BagAfter:click.BagBefore,refund?plan.BeltAfter:click.BeltBefore,
            refund?click.AppliedBagReferences:click.BagReferences,refund?click.AppliedBeltReferences:click.BeltReferences))return;
        float phase=click.Actor.Buffs.GetCustomVar(ActionOwnerMarker(click.Operation));
        if(phase!=(refund?2f:1f)&&phase!=(refund?5f:4f))return;
        var takes=(refund?plan.Takes.AsEnumerable().Reverse():plan.Takes).ToArray();
        using var freezeWorkspace=new ActionFreezeWorkspace();
        var frozenArguments=new List<ActionFrozenDesired>();
        try
        {
        // Freeze every desired image before Entered or any setter. Allocation
        // failure here is positively before invocation and cannot consume input.
        foreach(var take in takes)frozenArguments.Add(new ActionFrozenDesired((refund?(take.Belt?click.BeltBefore:click.BagBefore):(take.Belt?plan.BeltAfter:plan.BagAfter))[take.Slot],freezeWorkspace));
        if(!ActionRemoteImage(click,refund?plan.BagAfter:click.BagBefore,refund?plan.BeltAfter:click.BeltBefore,
            refund?click.AppliedBagReferences:click.BagReferences,refund?click.AppliedBeltReferences:click.BeltReferences))return;
        // The actual local native Save and full readback precede the first
        // setter. A host grant alone cannot create a restart-safe attempt.
        if(!BeginActionClientCheckpoint(click,refund))return;
        if(!ActionRemoteImage(click,refund?plan.BagAfter:click.BagBefore,refund?plan.BeltAfter:click.BeltBefore,
            refund?click.AppliedBagReferences:click.BagReferences,refund?click.AppliedBeltReferences:click.BeltReferences))return;
        // The host's full original current-head intent precedes this grant.
        // Entered is retained before the first native call and cannot be reset by
        // a duplicate courier or by an unobserved native return.
        if(refund)click.RefundEntered=true;else click.DebitEntered=true;
        click.Actor.Buffs.SetCustomVar(ActionOwnerMarker(click.Operation),refund?5f:4f,true);
        var bag=ItemStack.Clone(refund?plan.BagAfter:click.BagBefore);var belt=ItemStack.Clone(refund?plan.BeltAfter:click.BeltBefore);
        var bagRefs=(ItemStack[])(refund?click.AppliedBagReferences:click.BagReferences).Clone();var beltRefs=(ItemStack[])(refund?click.AppliedBeltReferences:click.BeltReferences).Clone();
        int returned=0;
        for(int ordinal=0;ordinal<takes.Length;ordinal++)
        {
            var take=takes[ordinal];var frozen=frozenArguments[ordinal];
            if(take.Belt&&take.Slot>=RebirthToolbeltCapacity.GetOwnedSlotCount(click.Actor,click.Belt.items.Length)||!ActionRemoteImage(click,bag,belt,bagRefs,beltRefs))return;
            var desired=frozen.Argument;
            using(var native=new ActionRemoteWrite(click,take.Belt,take.Slot,frozen))
            {
                // Exceptions preserve the attempted marker and Entered. A later
                // request may publish its native PDS but cannot invoke again.
                if(take.Belt)click.Actor.inventory.SetItem(take.Slot,desired.itemValue,desired.count);else click.Actor.bag.SetSlot(take.Slot,desired);
                if(native.Observed&&!native.Ran&&returned==0&&!refund&&ActionRemoteImage(click,bag,belt,bagRefs,beltRefs))
                {
                    click.DebitRefused=true;click.Actor.Buffs.SetCustomVar(ActionOwnerMarker(click.Operation),8f,true);PublishActionRemoteNative(click);return;
                }
                if(!native.Observed||!native.Ran||!native.Consumed)return;
                (take.Belt?belt:bag)[take.Slot]=frozen.Expected;
                (take.Belt?beltRefs:bagRefs)[take.Slot]=(take.Belt?click.Belt:click.Bag).items[take.Slot];
                if(!frozen.Matches((take.Belt?click.Belt:click.Bag).items[take.Slot])||!ActionRemoteImage(click,bag,belt,bagRefs,beltRefs))return;returned++;
            }
        }
        if(refund)click.Refunded=true;
        else{click.Debited=true;click.AppliedBagReferences=click.Bag.items.ToArray();click.AppliedBeltReferences=click.Belt.items.ToArray();}
        click.Actor.Buffs.SetCustomVar(ActionOwnerMarker(click.Operation),refund?3f:2f,true);PublishActionRemoteNative(click);
        }
        finally{foreach(var frozen in frozenArguments)frozen.Dispose();}
    }
    internal static void ReceiveActionRemoteGrant(NetPackage packet,World world,int actorId,Guid operation,byte phase,string hash,int total,int count,int index,string fragment)
    {
        var click=actionRemoteClick;
        actionRemoteTerminalReceipts.TryGetValue(operation,out var terminal);
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||phase>8||total>24*1024*1024+12||
            !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection))return;
        bool active=click!=null&&click.OwnerMetadataCurrent()&&actorId==click.Actor.entityId&&operation==click.Operation&&ReferenceEquals(connection,click.Connection);
        bool receipt=phase==7&&terminal?.Current()==true&&actorId==terminal.Actor.entityId&&ReferenceEquals(connection,terminal.Connection);
        if(!active&&!receipt||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return;
        using(scratch)
        {
        var scope=actionConnectionScopes.GetValue(connection,_=>new ActionConnectionScope());
        if(!RebirthRemoteResourceFragments.TryAdd("electrical-owner-grant|"+scope.Value.ToString("N")+"|"+phase,operation,hash,total,count,index,fragment,UnityEngine.Time.realtimeSinceStartup,out var complete)||complete==null)return;
        ReadActionRemoteEnvelope(complete,out string original,out byte[] nativeBefore);
        // Retain the actual three parsed buffers during qualification instead
        // of the maximum envelope reservation after parsing has completed.
        if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(scratch,checked(65536L+complete.LongLength+2L*original.Length+nativeBefore.LongLength)))return;
        if(phase==7){ReceiveActionRemoteTerminalAck(operation,original);return;}
        if(!active)return;
        if(phase==8){ReceiveRemoteActionNoAdmission(click,original,nativeBefore);return;}
        if(click.Plan==null)
        {
            if(phase!=0&&phase!=5&&phase!=6)return;
            IDisposable memory=null;
            try{var plan=QualifyRemoteActionDebit(click,original,nativeBefore,out memory,phase==5||phase==6);if(plan==null)return;click.Plan=plan;click.GrantMemory=memory;memory=null;}
            finally{memory?.Dispose();}
        }
        if(click.Plan.Original!=original||!click.Plan.HostNativeBefore.SequenceEqual(nativeBefore))return;
        if(phase==0)ApplyActionRemoteGrant(click,false);
        else if(phase==1)ApplyActionRemoteGrant(click,true);
        else if(phase==2||phase==3||phase==4)PublishActionRemoteNative(click);
        else if(phase==5||phase==6)FinishActionRemoteTerminal(click,phase);
        }
    }
}
