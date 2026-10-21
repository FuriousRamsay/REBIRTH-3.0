using System;
using System.IO;

// Native reagent bonus only. A failed/unknown attempted effect is retained; its
// absence never grants permission to mint another material output.
internal static class RebirthWorkstationReagentOriginal
{
    private static readonly object observedAuthority=new object();
    internal sealed class NativeObserved
    {
        private readonly RebirthWorkstationCompletionOriginal original;
        private readonly World world;private readonly TileEntityWorkstation tile;private readonly object worldState;
        private readonly string before,after;
        internal NativeObserved(RebirthWorkstationCompletionOutbox.State state,World world,TileEntityWorkstation tile,object authority)
        {
            if(!ReferenceEquals(authority,observedAuthority))throw new InvalidOperationException("Actual reagent setter/readback observation required");
            original=state.Original;this.world=world;this.tile=tile;worldState=world.worldState;before=state.ReagentBefore;after=state.ReagentAfter;
        }
        internal bool Matches(RebirthWorkstationCompletionOutbox.State state)=>ReferenceEquals(state.NativeReagentObserved,this)&&
            ReferenceEquals(state.Original,original)&&ReferenceEquals(state.NativeWorld,world)&&ReferenceEquals(state.NativeStation,tile)&&
            ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.worldState,worldState)&&
            ReferenceEquals(world.GetTileEntity(original.Position),tile)&&state.ReagentBefore==before&&state.ReagentAfter==after;
    }
    internal static bool Settle(RebirthWorkstationCompletionOutbox.State state,EntityPlayer owner)
    {
        var original=state?.Original;if(original==null||!original.MatchesOwner(owner))return false;
        if(!state.Saved&&!RebirthWorkstationCompletionOutbox.Save(state))return false;
        if(!RebirthWorkstationCompletionOutbox.TryReadSaved(original,out var savedHead)||!ReferenceEquals(savedHead,state))return false;
        if(!state.Decision)
        {
            state.Reagent=RebirthMedicalLootCraftSignatureService.ChooseWorkstationReagentOriginal(owner,original.RecipeName);
            state.Decision=true;if(!RebirthWorkstationCompletionOutbox.Save(state))return false;
        }
        if(state.Reagent.Length==0)return true;
        if(RebirthWorkstationReagentPublication.TryRecover(state))return true;
        if(state.ReagentObserved)return RebirthWorkstationReagentPublication.ObserveSaved(state);
        if(state.ReagentAttempted&&(!state.NativeReagentPrepared||state.ReagentSetterEntered))return false;
        if(!state.ReagentAttempted&&(state.NativeWorld==null||state.NativeStation==null||!ReferenceEquals(state.NativeWorld,GameManager.Instance?.World)||
            !ReferenceEquals(state.NativeWorld.GetTileEntity(original.Position),state.NativeStation)))RebirthWorkstationCompletionOutbox.TryRestoreUnattemptedNativeStation(state);
        var tile=state.NativeStation;var world=state.NativeWorld;
        if(world==null||tile==null||!ReferenceEquals(owner.world,world)||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(world.GetTileEntity(original.Position),tile)||tile.block?.GetBlockName()!=original.Block||tile.IsUserAccessing()||
            !RebirthWorkstationCompletionPipeline.CanSettleReagentOriginal(tile))return false;
        ItemStack[] after;
        if(!state.ReagentAttempted)
        {
            if(!RebirthMedicalLootCraftSignatureService.TryPlanWorkstationReagentOriginal(tile,state.Reagent,out after))return false;
            state.ReagentBefore=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(tile.Output));
            state.ReagentAfter=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(after));
            state.ReagentAttempted=true;state.NativeReagentPrepared=true;
        }
        else after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(state.ReagentAfter));
        if(!RebirthWorkstationCompletionOutbox.Save(state))return false; // attempted durable BEFORE setter
        if(!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetTileEntity(original.Position),tile)||
            tile.IsUserAccessing()||!RebirthWorkstationCompletionPipeline.CanSettleReagentOriginal(tile)||
            Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(tile.Output))!=state.ReagentBefore)return false;
        state.ReagentSetterEntered=true;
        tile.Output=after;tile.SetModified(); // exactly the existing native output, no input/refund simulation
        if(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(tile.Output))!=state.ReagentAfter)return false;
        state.NativeReagentObserved=new NativeObserved(state,world,tile,observedAuthority);
        state.ReagentObserved=true;
        if(!RebirthWorkstationCompletionOutbox.Save(state))return false;
        return RebirthWorkstationReagentPublication.ObserveSaved(state);
    }
}
