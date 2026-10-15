using System;
using System.Collections.Generic;

// Apply only a server-authored source delta. Never replace the whole backpack
// or repeat metabolism/effects on the owner when receiving the inventory reply.
internal static class RebirthConsumptionOwnerPublication
{
    private static World currentWorld;
    private static EntityPlayer currentPlayer;
    private static string currentCreation;
    internal static bool TryCaptureCreation(EntityPlayer player,out string creation)
    {
        creation=null;
        if(!ThreadManager.IsMainThread()||player?.world==null||
            !ReferenceEquals(player.world,GameManager.Instance?.World)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead())return false;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){creation=string.Empty;return true;}
        // Client hold reads the authenticated projected creation readiness state,
        // so an old projected ID during replacement is not permission to debit.
        if(RebirthCharacterCreationHoldService.IsHeld(player))return false;
        if(player.world.IsRemote())
            return RebirthSurvivorRequestScope.TryNormalize(RebirthSurvivorClientState.GetProjectedCreationId(player),out creation);
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete
            &&RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)
            &&RebirthSurvivorRequestScope.TryNormalize(record.Origin?.CreationId,out creation);
    }
    private static bool MatchesCreation(EntityPlayer player,string creation)
        =>TryCaptureCreation(player,out var current)&&string.Equals(current,creation,StringComparison.Ordinal);
    private static readonly HashSet<Guid> applied=new HashSet<Guid>();
    private static readonly Queue<Guid> order=new Queue<Guid>();
    internal static bool WasApplied(World world,int owner,Guid id,string creation="")
    {
        var player=world?.GetPrimaryPlayer();
        return ThreadManager.IsMainThread()&&world!=null&&world.IsRemote()&&ReferenceEquals(currentWorld,world)
            &&player!=null&&player.entityId==owner&&ReferenceEquals(player.world,world)&&MatchesCreation(player,creation)
            &&ReferenceEquals(currentPlayer,player)&&string.Equals(currentCreation,creation,StringComparison.Ordinal)
            &&id!=Guid.Empty&&applied.Contains(id);
    }
    internal static bool Apply(World world,int owner,Guid id,RebirthMetabolismSourceKind kind,int slot,ItemStack before,ItemStack after,string creation="")
    {
        if(!ThreadManager.IsMainThread()||world==null||!world.IsRemote()||id==Guid.Empty)return false;
        var player=world.GetPrimaryPlayer();
        if(player==null||player.entityId!=owner||!ReferenceEquals(player.world,world)||!MatchesCreation(player,creation)||before==null||after==null)
        {RebirthConsumptionTrace.Note("owner.refused",player,id,kind,slot,before,after,"owner-world-or-image",false);return false;}
        if(!ReferenceEquals(currentWorld,world)||!ReferenceEquals(currentPlayer,player)||
            !string.Equals(currentCreation,creation,StringComparison.Ordinal))
        {currentWorld=world;currentPlayer=player;currentCreation=creation;applied.Clear();order.Clear();}
        if(applied.Contains(id))
        {RebirthConsumptionTrace.Note("owner.duplicate",player,id,kind,slot,before,after,"already-applied",false);return false;}
        ItemStack[] slots=kind==RebirthMetabolismSourceKind.Backpack?player.bag?.ItemGrid.items
            :kind==RebirthMetabolismSourceKind.HeldToolbelt?player.inventory?.ItemGrid.items:null;
        if(slots==null||slot<0||slot>=slots.Length)
        {RebirthConsumptionTrace.Note("owner.refused",player,id,kind,slot,before,after,"source-grid-or-slot",false);return false;}
        var actual=slots[slot];
        // Native inventory synchronization can win the race with this reply.
        // Acknowledge its exact postimage without executing a second slot write.
        if(Same(actual,after)&&!Same(before,after))
        {Remember(id);RebirthConsumptionTrace.Note("owner.native-postimage",player,id,kind,slot,before,actual,"no-second-write",true);return true;}
        // A moved/changed source is not permission to overwrite a different item.
        if(!Same(actual,before))
        {RebirthConsumptionTrace.Note("owner.refused",player,id,kind,slot,before,actual,"source-preimage-changed",false);return false;}
        if(kind==RebirthMetabolismSourceKind.Backpack)player.bag.SetSlot(slot,after.Clone());
        else player.inventory.SetItem(slot,after.Clone());
        // Native setters may replace the backing grid; observe live custody again,
        // as the authoritative source-writeback path does, rather than the old array.
        slots=kind==RebirthMetabolismSourceKind.Backpack?player.bag?.ItemGrid.items
            :player.inventory?.ItemGrid.items;
        if(slots==null||slot<0||slot>=slots.Length)return false;
        actual=slots[slot];
        if(!Same(actual,after))
        {RebirthConsumptionTrace.Note("owner.refused",player,id,kind,slot,before,actual,"setter-postimage-mismatch",false);return false;}
        Remember(id);
        RebirthConsumptionTrace.Note("owner.applied",player,id,kind,slot,before,actual,"exact-postimage",true);
        return true;
    }
    private static bool Same(ItemStack left,ItemStack right)
        =>left!=null&&right!=null&&left.count==right.count&&left.itemValue.Equals(right.itemValue);
    private static void Remember(Guid id)
    {
        applied.Add(id);order.Enqueue(id);while(order.Count>128)applied.Remove(order.Dequeue());
    }
}
