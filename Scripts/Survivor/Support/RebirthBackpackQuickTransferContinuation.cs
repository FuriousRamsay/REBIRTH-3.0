using System;

// One receipt's continuation gate. It grants a fresh prepare, never replays a debit.
public sealed class RebirthBackpackQuickTransferContinuation
{
    private readonly object owner,world;
    private readonly RebirthBackpackLibraryReceipt receipt;
    private readonly ItemValue afterPack;
    private readonly ItemStack afterSource;
    private bool consumed;
    private RebirthBackpackQuickTransferContinuation(object owner,object world,
        RebirthBackpackLibraryReceipt receipt,ItemValue pack,ItemStack source)
    {this.owner=owner;this.world=world;this.receipt=receipt;afterPack=pack.Clone();afterSource=source.Clone();}
    private static bool Same(ItemStack a,ItemStack b)
        =>a!=null&&b!=null&&a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||
            !a.IsEmpty()&&!b.IsEmpty()&&RebirthNativeItemCodec.Encode(a.itemValue)==RebirthNativeItemCodec.Encode(b.itemValue));
    public static bool TryCreate(object owner,object world,RebirthBackpackLibraryReceipt receipt,
        ItemStack expectedSource,out RebirthBackpackQuickTransferContinuation result)
    {
        result=null;
        if(owner==null||world==null||receipt==null||expectedSource==null||expectedSource.IsEmpty())return false;
        try
        {
            if(!receipt.TryGetImages(out var beforePack,out var afterPack,out var beforeSlot,out var afterSlot))return false;
            ItemStack beforeSource=beforeSlot,afterSource=afterSlot;
            if(!receipt.Deposit)
            {
                ItemStack[] before=null,after=null;
                bool read=receipt.IsSellStash
                    ?RebirthBackpackSellStashContents.TryRead(beforePack,out before)&&RebirthBackpackSellStashContents.TryRead(afterPack,out after)
                    :RebirthBackpackLibraryContents.TryRead(beforePack,out before)&&RebirthBackpackLibraryContents.TryRead(afterPack,out after);
                if(!read||receipt.LibrarySlot<0||receipt.LibrarySlot>=before.Length||receipt.LibrarySlot>=after.Length)return false;
                beforeSource=before[receipt.LibrarySlot];afterSource=after[receipt.LibrarySlot];
            }
            if(!Same(expectedSource,beforeSource)||afterSource.count!=beforeSource.count-receipt.Quantity)return false;
            if(!afterSource.IsEmpty())
            {
                var expected=beforeSource.Clone();expected.count=afterSource.count;
                if(!Same(expected,afterSource))return false;
            }
            result=new RebirthBackpackQuickTransferContinuation(owner,world,receipt,afterPack,afterSource);
            return true;
        }
        catch{return false;}
    }
    // Owner-only projection is not a physical item. Compare its complete section and
    // exact receipt revision; the next server prepare still authenticates physical custody.
    public bool TryAdvanceProjection(object currentOwner,object currentWorld,string creation,long revision,
        bool pending,RebirthBackpackLibrarySettlement settlement,string backpackId,ItemStack[] section,
        ItemStack currentSource,out ItemStack remainder)
    {
        remainder=null;
        if(!Admits(currentOwner,currentWorld,creation,revision,pending,settlement))return false;
        try
        {
            ItemStack[] expected=null;
            bool read=receipt.IsSellStash?RebirthBackpackSellStashContents.TryRead(afterPack,out expected)
                :RebirthBackpackLibraryContents.TryRead(afterPack,out expected);
            if(!read||backpackId!=afterPack.ItemClass.GetItemName()||section==null||section.Length!=expected.Length||
                !Same(currentSource,afterSource))return false;
            for(int i=0;i<section.Length;i++)if(!Same(section[i],expected[i]))return false;
            consumed=true;remainder=afterSource.Clone();return true;
        }
        catch{return false;}
    }
    private bool Admits(object currentOwner,object currentWorld,string creation,long revision,
        bool pending,RebirthBackpackLibrarySettlement settlement)
        =>!consumed&&!pending&&ReferenceEquals(owner,currentOwner)&&ReferenceEquals(world,currentWorld)&&
            settlement!=null&&settlement.Applied&&settlement.TransactionId==receipt.TransactionId&&
            settlement.CreationId==receipt.CreationId&&creation==receipt.CreationId&&
            receipt.ExpectedGearRevision!=long.MaxValue&&revision==receipt.ExpectedGearRevision+1&&
            settlement.GearRevision==revision;
    public bool TryAdvance(object currentOwner,object currentWorld,string creation,long revision,
        bool pending,RebirthBackpackLibrarySettlement settlement,ItemValue currentPack,ItemStack currentSource,out ItemStack remainder)
    {
        remainder=null;
        if(consumed||pending||!ReferenceEquals(owner,currentOwner)||!ReferenceEquals(world,currentWorld)||
            settlement==null||!settlement.Applied||settlement.TransactionId!=receipt.TransactionId||
            settlement.CreationId!=receipt.CreationId||creation!=receipt.CreationId||
            receipt.ExpectedGearRevision==long.MaxValue||revision!=receipt.ExpectedGearRevision+1||
            settlement.GearRevision!=revision)return false;
        try
        {
            if(currentPack==null||RebirthNativeItemCodec.Encode(currentPack)!=RebirthNativeItemCodec.Encode(afterPack)||
                !Same(currentSource,afterSource))return false;
            consumed=true;
            remainder=afterSource.Clone();
            return true;
        }
        catch{return false;}
    }
}