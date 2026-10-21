using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class PersonalOutputRecoveryGate
    {
        internal World World;internal Guid[] Operations;
        internal readonly Dictionary<Guid,int> Slots=new Dictionary<Guid,int>();
    }
    private static readonly ConditionalWeakTable<EntityPlayerLocal,PersonalOutputRecoveryGate> personalOutputRecoveryGates=new ConditionalWeakTable<EntityPlayerLocal,PersonalOutputRecoveryGate>();
    private static void ObservePersonalOutputRecoveryInput(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool ran)
    {
        try
        {
            if(!ran||!ThreadManager.IsMainThread()||mode!=global::StreamModeRead.Persistency||actor is not EntityPlayerLocal local||
                actor.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||data?.buffData==null||data.buffData.Length>1024*1024||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRecipeOutputKeys(data.buffData.ToArray(),out var operations)||operations.Length==0)return;
            // Discovery is a hold only. An unknown/failed Load lineage must not
            // let a restored native queue generate an already-delivered primary.
            personalOutputRecoveryGates.Remove(local);
            personalOutputRecoveryGates.Add(local,new PersonalOutputRecoveryGate{World=actor.world,Operations=operations});
        }
        catch{/* This observer issues no original or setter permission. */}
    }
    internal static bool CanAdvancePersonalOutputRecovery(XUiC_RecipeStack card)
    {
        try
        {
            var actor=card?.xui?.playerUI?.entityPlayer;
            if(actor==null||card.GetParentByType<XUiC_WorkstationWindowGroup>()!=null||
                !personalOutputRecoveryGates.TryGetValue(actor,out var gate)||!ReferenceEquals(gate.World,actor.world))return true;
            var cards=card.xui.FindWindowGroupByName("crafting")?.GetChildByType<XUiC_CraftingQueue>()?.GetRecipesToCraft();
            if(cards==null)return false;int slot=Array.FindIndex(cards,value=>ReferenceEquals(value,card));if(slot<0)return true;
            foreach(var operation in gate.Operations)
            {
                if(actor.Buffs.GetCustomVar(PersonalOutputReceipt(operation))==0f)continue;
                if(gate.Slots.TryGetValue(operation,out int originalSlot)&&originalSlot!=slot)continue;
                // Only the exact reattached original may pass to its existing
                // cached/remaining-output continuation. A marker is not a grant.
                if(pendingUiOutputs.TryGetValue(card,out var pending)&&pending.Operation==operation&&pending.Source!=null)continue;
                return false;
            }
            return true;
        }
        catch{return false;}
    }
    private static bool BindPersonalOutputRecoverySlot(EntityPlayerLocal actor,Guid operation,int slot)
    {
        if(slot<0||slot>=255||!personalOutputRecoveryGates.TryGetValue(actor,out var gate)||!ReferenceEquals(gate.World,actor.world)||!gate.Operations.Contains(operation))return false;
        if(gate.Slots.TryGetValue(operation,out int prior))return prior==slot;
        gate.Slots.Add(operation,slot);return true;
    }
}
