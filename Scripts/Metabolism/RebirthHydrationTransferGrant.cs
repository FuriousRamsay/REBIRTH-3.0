using System;
using System.Collections.Generic;

// A warm actual local-authority grant, not a reconstructed receipt or delegate.
// Remote grants require their own authenticated network producer (not provided
// by this local factory). No factory here applies inventory/metabolism changes.
internal sealed partial class RebirthHydrationTransferGrant
{
    private static readonly Dictionary<Guid,RebirthHydrationTransferGrant> issued=new Dictionary<Guid,RebirthHydrationTransferGrant>();
    internal static bool HasIssued(Guid operation)=>issued.ContainsKey(operation);
    internal readonly EntityPlayerLocal Player;
    internal readonly RebirthHydrationTransferOriginal Original;
    private readonly World world;private readonly GameManager game;private readonly RebirthMetabolismState state;
    private RebirthHydrationTransferGrant(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,RebirthMetabolismState state)
    {Player=player;Original=original;this.state=state;world=player.world;game=GameManager.Instance;}
    internal bool Current()
    {
        try{return issued.TryGetValue(Original.Operation,out var current)&&ReferenceEquals(current,this)&&
            ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(Player.world,world)&&!world.IsRemote()&&
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&RebirthHydrationOwnerHold.Matches(Player,Original)&&
            Source(Player,Original,false,out var source)&&ReferenceEquals(source,state);}
        catch{return false;}
    }
    internal static bool TryIssueLocal(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationTransferGrant grant)
    {
        grant=null;
        try
        {
            if(original==null||player?.world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
                !RebirthHydrationOwnerHold.Matches(player,original))return false;
            if(issued.TryGetValue(original.Operation,out var retained))
            {if(!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=original.Digest||!retained.Current())return false;grant=retained;return true;}
            if(issued.Count>=64||!Source(player,original,true,out var source)||
                !RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original)||
                !RebirthHydrationTransferJournal.TryPrepare(player,original,out _)||
                !RebirthHydrationTransferJournal.TryMarkAttempted(player,original,out _)||
                !RebirthHydrationOwnerHold.Matches(player,original)||!Source(player,original,true,out var latest)||!ReferenceEquals(source,latest)||
                !RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original))return false;
            var next=new RebirthHydrationTransferGrant(player,original,source);issued.Add(original.Operation,next);
            if(!next.Current())return false;grant=next;return true;
        }
        catch{return false;}
    }
    private static bool Source(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,bool initial,out RebirthMetabolismState state)
    {
        state=null;
        if(!RebirthHydrationOwnerHold.Matches(player,original)||!RebirthMetabolismStateRepository.TryGet(player,out state)||
            !RebirthHydrationTransferScope.MatchesState(original.Creation,state)||initial&&state.Revision!=original.Revision||
            !original.TryGetPlan(out var plan))return false;
        var hydration=state.HydrationSlotItem;
        int count=hydration==null||hydration.IsEmpty()?0:hydration.count;
        string data=count==0?string.Empty:RebirthNativeItemCodec.Encode(hydration.itemValue);
        var receipt=state.HydrationTransferReceipt;
        if(receipt!=null&&receipt.Operation==original.Operation&&receipt.Digest!=original.Digest)return false;
        bool settled=receipt!=null&&receipt.Digest==original.Digest;
        if(initial&&settled)return false;
        var expected=settled?plan.GearAfter:plan.GearBefore;
        if(count!=expected.Count||data!=expected.ItemData)return false;
        var container=original.Equip?plan.GearAfter:plan.GearBefore;
        if(!RebirthNativeItemCodec.TryDecode(container.ItemData,out var value)||
            !RebirthConsumableResolver.TryResolve(value,out var definition)||definition==null||!definition.IsDrink||!definition.HydrationEquippable)return false;
        if(original.Equip&&RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            if(!RebirthWorldCharacterService.TryGet(player,out var character)||character?.Support==null)return false;
            if(character.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.SupportSlotId,out var support)&&!string.IsNullOrEmpty(support))return false;
        }
        return true;
    }
}
