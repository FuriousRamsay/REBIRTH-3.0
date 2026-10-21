using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Called by the original native actor action, before either leg mutates.
    // Returning true transfers this exact frozen source to the controller;
    // it is not a claim that either native publication is already saved.
    internal static bool BeginAuthorityAction(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string purpose,
        ItemStack[] input,RecipeQueueItem[] queue,IList<ItemStack> returns,ItemStack[] output,ItemStack[] tools,
        Func<bool> current,out string reason,bool cycle=false,float workSeconds=0)
    {
        reason="xuiRebirthCamperStationSaving";
        if(actor==null||actor.world?.IsRemote()!=false||station==null||operation==Guid.Empty)return false;
        if(!TrySelectAuthorityAttempt(actor,station,operation,purpose,out operation))return false;
        var pending=authorityOriginals.FirstOrDefault(value=>value.Source?.Operation==operation);
        if(pending!=null)
            return ReferenceEquals(pending.Source.Actor,actor)&&ReferenceEquals(pending.Source.Station,station)&&pending.Source.Purpose==purpose&&pending.Source.Current();
        if(HasAuthorityTerminal(actor,station,operation,purpose)){reason=null;return true;}
        Original original=null;
        try
        {
            original=Original.Capture(actor,station,operation,purpose,input,queue,returns,output,tools,current,cycle,workSeconds);
            if(original==null||!TryRetainAuthority(original))return false;
            original=null;reason=null;return true;
        }
        catch{return false;}
        finally{original?.Dispose();}
    }
}
