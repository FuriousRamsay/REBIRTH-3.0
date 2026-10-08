using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcPlayerInventoryEndpointService
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,string> ActorEndpoints=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public static string Ensure(EntityPlayer actor,string actorId){if(actor==null||string.IsNullOrWhiteSpace(actorId))return string.Empty;string id="player:"+actor.entityId;lock(Sync){ActorEndpoints[actorId]=id;}RebirthNpcExternalInventoryEndpointRegistry.Register(new RebirthNpcPlayerInventoryEndpoint(actor.entityId,id));return id;}
    public static bool TryGet(string actorId,out string endpointId){lock(Sync)return ActorEndpoints.TryGetValue(actorId??string.Empty,out endpointId);}
    public static void Reset(){lock(Sync)ActorEndpoints.Clear();}
}

public sealed class RebirthNpcPlayerInventoryEndpoint:IRebirthNpcExternalInventoryEndpoint,IRebirthNpcExternalInventoryQuantityEndpoint
{
    private static readonly object ReservationSync=new object();
    private static readonly Dictionary<string,int> DebitReservations=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> CreditReservations=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> DebitSlotReservations=new Dictionary<string,int>(StringComparer.Ordinal);

    private sealed class DebitClaim
    {
        public bool Backpack;
        public int Slot;
        public int Quantity;
        public int ItemFingerprint;
    }

    private sealed class Reservation:IRebirthNpcExternalInventoryReservation,IRebirthNpcExternalInventoryReservationOutcome
    {
        private enum ReservationState:byte { Prepared=0, Committed=1, Failed=2, RolledBack=3, Indeterminate=4 }
        private readonly RebirthNpcPlayerInventoryEndpoint owner;
        private readonly string item;
        private readonly int quantity;
        private readonly bool debit;
        private readonly DebitClaim[] debitClaims;
        private ReservationState state;
        private bool released;
        private string terminalError=string.Empty;
        public string EndpointId=>owner.EndpointId;
        public Guid TransactionId{get;private set;}
        RebirthTransactionState IRebirthNpcExternalInventoryReservationOutcome.State
        {
            get
            {
                switch(state)
                {
                    case ReservationState.Committed:return RebirthTransactionState.Committed;
                    case ReservationState.RolledBack:return RebirthTransactionState.RolledBack;
                    case ReservationState.Failed:return RebirthTransactionState.Failed;
                    case ReservationState.Indeterminate:return RebirthTransactionState.Indeterminate;
                    default:return RebirthTransactionState.Prepared;
                }
            }
        }
        string IRebirthNpcExternalInventoryReservationOutcome.Detail { get { return terminalError??string.Empty; } }

        public Reservation(RebirthNpcPlayerInventoryEndpoint owner,Guid id,string item,int quantity,bool debit,DebitClaim[] debitClaims)
        { this.owner=owner;TransactionId=id;this.item=item;this.quantity=quantity;this.debit=debit;this.debitClaims=debitClaims; }

        public bool Commit(out string error)
        {
            lock(this)
            {
                if(state==ReservationState.Committed){error=string.Empty;return true;}
                if(state!=ReservationState.Prepared){error=terminalError.Length>0?terminalError:"Reservation is no longer commit-capable.";return false;}
                try
                {
                    bool success=owner.Commit(item,quantity,debit,debitClaims,out error);
                    terminalError=error??string.Empty;
                    state=success?ReservationState.Committed:(IsIndeterminate(error)?ReservationState.Indeterminate:ReservationState.Failed);
                    return success;
                }
                catch(Exception ex)
                {
                    terminalError=ex.GetType().Name+": "+ex.Message;
                    error=terminalError;
                    state=ReservationState.Indeterminate;
                    return false;
                }
                finally { ReleaseOnce(); }
            }
        }

        public bool Rollback(out string error)
        {
            lock(this)
            {
                if(state==ReservationState.RolledBack){error=string.Empty;return true;}
                if(state==ReservationState.Committed||state==ReservationState.Indeterminate){error="Committed or indeterminate reservation cannot be rolled back without a compensation receipt.";return false;}
                ReleaseOnce();
                state=ReservationState.RolledBack;
                terminalError=string.Empty;
                error=string.Empty;
                return true;
            }
        }

        public void Dispose()
        {
            lock(this)
            {
                if(state!=ReservationState.Prepared)return;
                ReleaseOnce();
                state=ReservationState.RolledBack;
            }
        }

        private void ReleaseOnce()
        {
            if(released)return;
            owner.Release(item,quantity,debit,debitClaims);
            released=true;
        }
        private static bool IsIndeterminate(string error)
        {
            return !string.IsNullOrEmpty(error) &&
                error.IndexOf("indeterminate",StringComparison.OrdinalIgnoreCase)>=0;
        }
    }

    private readonly int playerEntityId;public string EndpointId{get;private set;}
    public RebirthNpcPlayerInventoryEndpoint(int entityId,string endpointId){playerEntityId=entityId;EndpointId=endpointId;}
    public int GetAvailableDebitQuantity(string itemKey){EntityPlayer p=Player();int type;if(p==null||!TryType(itemKey,out type))return 0;int reserved;lock(ReservationSync)DebitReservations.TryGetValue(Key(itemKey),out reserved);return Math.Max(0,Count(p,type)-reserved);}
    public int GetAvailableCreditQuantity(string itemKey){EntityPlayer p=Player();ItemValue v=ItemClass.GetItem(itemKey,false);if(p==null||v.IsEmpty())return 0;int reserved;lock(ReservationSync)CreditReservations.TryGetValue(Key(itemKey),out reserved);return Math.Max(0,Capacity(p,v)-reserved);}
    public bool TryReserveDebit(Guid transactionId,string itemKey,int quantity,out IRebirthNpcExternalInventoryReservation reservation,out string error)
    {
        reservation=null;error=string.Empty;DebitClaim[] claims;
        if(transactionId==Guid.Empty||quantity<=0||!ReserveDebit(itemKey,quantity,out claims))
        {error="Player inventory has insufficient exact unreserved quantity.";return false;}
        reservation=new Reservation(this,transactionId,itemKey,quantity,true,claims);return true;
    }
    public bool TryReserveCredit(Guid transactionId,string itemKey,int quantity,out IRebirthNpcExternalInventoryReservation reservation,out string error)
    {
        reservation=null;error=string.Empty;
        if(transactionId==Guid.Empty||quantity<=0||!ReserveCredit(itemKey,quantity))
        {error="Player inventory has insufficient unreserved capacity.";return false;}
        reservation=new Reservation(this,transactionId,itemKey,quantity,false,null);return true;
    }
    private string Key(string item){return playerEntityId+"|"+(item??string.Empty);}
    private string SlotKey(bool backpack,int slot){return playerEntityId+"|"+(backpack?"b":"t")+"|"+slot;}
    private bool ReserveCredit(string item,int quantity)
    {
        lock(ReservationSync)
        {
            int available=GetRawCredit(item);int existing;CreditReservations.TryGetValue(Key(item),out existing);
            if(available-existing<quantity)return false;CreditReservations[Key(item)]=existing+quantity;return true;
        }
    }
    private bool ReserveDebit(string item,int quantity,out DebitClaim[] claims)
    {
        claims=null;EntityPlayer player=Player();int type;
        if(player==null||!TryType(item,out type))return false;
        lock(ReservationSync)
        {
            List<DebitClaim> selected=new List<DebitClaim>();int left=quantity;
            SelectDebitClaims(player.bag.ItemGrid.items,true,type,ref left,selected);
            SelectDebitClaims(player.inventory.ItemGrid.items,false,type,ref left,selected);
            if(left>0)return false;
            for(int i=0;i<selected.Count;i++)
            {
                DebitClaim claim=selected[i];string slotKey=SlotKey(claim.Backpack,claim.Slot);int reserved;
                DebitSlotReservations.TryGetValue(slotKey,out reserved);DebitSlotReservations[slotKey]=reserved+claim.Quantity;
            }
            int aggregate;DebitReservations.TryGetValue(Key(item),out aggregate);DebitReservations[Key(item)]=aggregate+quantity;
            claims=selected.ToArray();return true;
        }
    }
    private void SelectDebitClaims(ItemStack[] slots,bool backpack,int type,ref int left,List<DebitClaim> selected)
    {
        if(slots==null||left<=0)return;
        for(int i=0;i<slots.Length&&left>0;i++)
        {
            ItemStack stack=slots[i];if(stack==null||stack.IsEmpty()||stack.itemValue==null||stack.itemValue.type!=type||!IsFungible(stack.itemValue))continue;
            int reserved;DebitSlotReservations.TryGetValue(SlotKey(backpack,i),out reserved);
            int available=Math.Max(0,stack.count-reserved);if(available<=0)continue;
            int take=Math.Min(left,available);
            selected.Add(new DebitClaim{Backpack=backpack,Slot=i,Quantity=take,ItemFingerprint=ItemFingerprint(stack.itemValue)});
            left-=take;
        }
    }
    private void Release(string item,int quantity,bool debit,DebitClaim[] claims)
    {
        lock(ReservationSync)
        {
            Dictionary<string,int> map=debit?DebitReservations:CreditReservations;int existing;
            if(map.TryGetValue(Key(item),out existing)){existing-=quantity;if(existing>0)map[Key(item)]=existing;else map.Remove(Key(item));}
            if(debit&&claims!=null)for(int i=0;i<claims.Length;i++)
            {
                string slotKey=SlotKey(claims[i].Backpack,claims[i].Slot);int slotReserved;
                if(!DebitSlotReservations.TryGetValue(slotKey,out slotReserved))continue;
                slotReserved-=claims[i].Quantity;if(slotReserved>0)DebitSlotReservations[slotKey]=slotReserved;else DebitSlotReservations.Remove(slotKey);
            }
        }
    }
    private int GetRawDebit(string item){EntityPlayer p=Player();int type;return p!=null&&TryType(item,out type)?Count(p,type):0;}
    private int GetRawCredit(string item){EntityPlayer p=Player();ItemValue v=ItemClass.GetItem(item,false);return p==null||v.IsEmpty()?0:Capacity(p,v);}
    private EntityPlayer Player(){World w=GameManager.Instance!=null?GameManager.Instance.World:null;return w!=null?w.GetEntity(playerEntityId) as EntityPlayer:null;}

    private bool Commit(string itemKey,int quantity,bool debit,DebitClaim[] debitClaims,out string error)
    {
        error=string.Empty;
        EntityPlayer p=Player();
        if(p==null){error="Player is unavailable.";return false;}
        ItemValue value=ItemClass.GetItem(itemKey,false);
        if(value.IsEmpty()){error="Unknown item key: "+itemKey;return false;}
        ItemStack[] bag=Clone(p.bag.ItemGrid.items),belt=Clone(p.inventory.ItemGrid.items);
        try
        {
            bool ok=debit?RemoveExactClaims(p,value.type,quantity,debitClaims):Add(p,value,quantity);
            if(ok)return true;
            Restore(p,bag,belt);
            error=debit?"Player item removal failed and was restored.":"Player inventory insertion failed and was restored.";
            return false;
        }
        catch(Exception ex)
        {
            try { Restore(p,bag,belt); }
            catch(Exception restoreEx)
            {
                error="Player inventory mutation failed and rollback is indeterminate: "+ex.GetType().Name+": "+ex.Message+"; rollback "+restoreEx.GetType().Name+": "+restoreEx.Message;
                return false;
            }
            error="Player inventory mutation failed and was restored: "+ex.GetType().Name+": "+ex.Message;
            return false;
        }
    }

    private static void Restore(EntityPlayer p,ItemStack[] bag,ItemStack[] belt){for(int i=0;i<bag.Length;i++)p.bag.SetSlot(i,bag[i]);for(int i=0;i<belt.Length;i++)p.inventory.SetItem(i,belt[i]);}
    private bool RemoveExactClaims(EntityPlayer p,int type,int quantity,DebitClaim[] claims)
    {
        if(claims==null||claims.Length==0)return false;int total=0;
        // Validate every reserved instance before the first mutation.
        for(int i=0;i<claims.Length;i++)
        {
            DebitClaim claim=claims[i];ItemStack[] slots=claim.Backpack?p.bag.ItemGrid.items:p.inventory.ItemGrid.items;
            if(slots==null||claim.Slot<0||claim.Slot>=slots.Length)return false;
            ItemStack stack=slots[claim.Slot];
            if(stack==null||stack.IsEmpty()||stack.itemValue==null||stack.itemValue.type!=type||
                stack.count<claim.Quantity||!IsFungible(stack.itemValue)||ItemFingerprint(stack.itemValue)!=claim.ItemFingerprint)return false;
            int reserved;lock(ReservationSync)DebitSlotReservations.TryGetValue(SlotKey(claim.Backpack,claim.Slot),out reserved);
            if(reserved<claim.Quantity)return false;total+=claim.Quantity;
        }
        if(total!=quantity)return false;
        for(int i=0;i<claims.Length;i++)
        {
            DebitClaim claim=claims[i];ItemStack[] slots=claim.Backpack?p.bag.ItemGrid.items:p.inventory.ItemGrid.items;
            ItemStack next=slots[claim.Slot].Clone();next.count-=claim.Quantity;
            if(claim.Backpack)p.bag.SetSlot(claim.Slot,next.count>0?next:ItemStack.Empty);
            else p.inventory.SetItem(claim.Slot,next.count>0?next:ItemStack.Empty);
        }
        return true;
    }
    private static bool IsFungible(ItemValue value)
    {
        if(value==null||value.IsEmpty())return false;
        // These attributes have no representation in the legacy name/count store.
        // Seed is deliberately not checked: native constructors assign it to ordinary goods.
        if(value.Meta!=0 || value.Flags!=0 || value.SelectedAmmoTypeIndex!=0 ||
            (value.Stats!=null && value.Stats.Length>0) || !value.TextureFullArray.IsDefault)return false;
        if(value.Metadata!=null&&value.Metadata.Count>0)return false;
        if(value.modifications!=null)
            for(int i=0;i<value.modifications.Length;i++)
                if(value.modifications[i]!=null&&!value.modifications[i].IsEmpty())return false;
        if(value.cosmeticMods!=null)
            for(int i=0;i<value.cosmeticMods.Length;i++)
                if(value.cosmeticMods[i]!=null&&!value.cosmeticMods[i].IsEmpty())return false;
        // Abstract NPC inventory has no durable quality/durability channel. Refuse rather
        // than erase those attributes at the external boundary.
        if(value.Quality>0||Math.Abs(value.UseTimes)>0.0001f)return false;
        return true;
    }

    private static int ItemFingerprint(ItemValue value)
    {
        if(value==null||value.IsEmpty())return 0;
        unchecked
        {
            int hash=17;hash=hash*31+value.type;hash=hash*31+value.Meta;hash=hash*31+value.Quality;
            hash=hash*31+value.UseTimes.GetHashCode();
            if(value.Metadata!=null&&value.Metadata.Count>0)
            {
                List<string> keys=new List<string>(value.Metadata.Keys);keys.Sort(StringComparer.Ordinal);
                for(int i=0;i<keys.Count;i++){string key=keys[i]??string.Empty;object metadata=value.Metadata[key];
                    hash=hash*31+StringComparer.Ordinal.GetHashCode(key);hash=hash*31+(metadata!=null?metadata.GetHashCode():0);}
            }
            ItemValue[] mods=value.modifications;hash=hash*31+(mods!=null?mods.Length:0);
            for(int i=0;mods!=null&&i<mods.Length;i++)hash=hash*31+(mods[i]!=null?mods[i].type:0);
            ItemValue[] cosmetics=value.cosmeticMods;hash=hash*31+(cosmetics!=null?cosmetics.Length:0);
            for(int i=0;cosmetics!=null&&i<cosmetics.Length;i++)hash=hash*31+(cosmetics[i]!=null?cosmetics[i].type:0);
            return hash;
        }
    }
    private static bool Remove(EntityPlayer p,int type,int quantity){int left=quantity;ItemStack[] slots=p.bag.ItemGrid.items;for(int i=0;i<slots.Length&&left>0;i++){ItemStack s=slots[i];if(s==null||s.IsEmpty()||s.itemValue.type!=type)continue;int take=Math.Min(left,s.count);ItemStack n=s.Clone();n.count-=take;p.bag.SetSlot(i,n.count>0?n:ItemStack.Empty);left-=take;}slots=p.inventory.ItemGrid.items;for(int i=0;i<slots.Length&&left>0;i++){ItemStack s=slots[i];if(s==null||s.IsEmpty()||s.itemValue.type!=type)continue;int take=Math.Min(left,s.count);ItemStack n=s.Clone();n.count-=take;p.inventory.SetItem(i,n.count>0?n:ItemStack.Empty);left-=take;}return left==0;}
    private static bool Add(EntityPlayer p,ItemValue value,int quantity){ItemStack remain=new ItemStack(value.Clone(),quantity);if(p.bag.AddItem(remain))return true;return remain.count<=0||p.inventory.AddItem(remain)||remain.count<=0;}
    private static int Count(EntityPlayer p,int type){return Count(p.bag.ItemGrid.items,type)+Count(p.inventory.ItemGrid.items,type);}private static int Count(ItemStack[] a,int type){int n=0;for(int i=0;i<a.Length;i++)if(a[i]!=null&&!a[i].IsEmpty()&&a[i].itemValue.type==type)n+=a[i].count;return n;}
    private static int Capacity(EntityPlayer p, ItemValue v)
    {
        if (p == null || v == null || v.IsEmpty()) return 0;
        var probe = new ItemStack(v, 1);
        int bag = probe.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack)
            ? Capacity(p.bag.ItemGrid.items, v, false) : 0;
        int belt = probe.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt)
            ? Capacity(p.inventory.ItemGrid.items, v, true) : 0;
        // Native AddItem does not distribute this credit over partial stacks: either
        // the backpack accepts the whole stack or the toolbelt must accept it.
        return Math.Max(bag, belt);
    }
    private static int Capacity(ItemStack[] slots, ItemValue value, bool toolbelt)
    {
        if (slots == null || value == null || value.IsEmpty()) return 0;
        ItemClass item = ItemClass.GetForId(value.type);
        if (item == null || item.MaxCount <= 0) return 0;
        int max = item.MaxCount, best = 0;
        var probe = new ItemStack(value, max);
        int count = Math.Max(0, slots.Length - (toolbelt ? 1 : 0));
        for (int i = 0; i < count; i++)
        {
            ItemStack stack = slots[i];
            if (stack == null || stack.IsEmpty()) return max;
            int available;
            if (stack.CanStackPartlyWith(probe, out available))
                best = Math.Max(best, available);
        }
        return best;
    }
    private static bool TryType(string key,out int type){ItemValue v=ItemClass.GetItem(key,false);type=v.type;return !v.IsEmpty();}
    private static ItemStack[] Clone(ItemStack[] a){ItemStack[] r=new ItemStack[a.Length];for(int i=0;i<a.Length;i++)r[i]=a[i]!=null?a[i].Clone():ItemStack.Empty;return r;}
}

public static class RebirthNpcDirectActivationBinding
{
    private static int opens;
    private static int failures;

    public static void Record(bool opened)
    {
        if (opened)
            Interlocked.Increment(ref opens);
        else
            Interlocked.Increment(ref failures);
    }

    public static string GetReport()
    {
        return "[REBIRTH NPC Direct Activation] installed=1 candidates=1 opens=" +
               Volatile.Read(ref opens) + " failures=" + Volatile.Read(ref failures) +
               " binding=EntityRebirthNPC.OnEntityActivated";
    }
}
