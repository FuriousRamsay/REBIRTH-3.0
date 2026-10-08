using System;using System.Collections.Generic;
public partial class BlockToolSelection
{
    public Action AfterPlacement;
    public bool ExecuteUseAction(ItemInventoryData d,bool released,PlayerActionsLocal p)=>true;
    // Native placement/prediction services are a DOUBLE. Actual Third hook and codec/send-scope source execute around it.
    public bool PlaceBlock(ItemInventoryData data,BlockPlacement.Result result,ItemInventoryData placementData,Block block,BlockValue value)
    {
        var state=SeedNativeBindingCandidate.Begin(data,result,value);
        var world=data.world;world.block=result.blockValue;world.Tile=new TileEntityPlantGrowingRebirth();
        SeedPlacementIntentCodecReview.TryExpectedChange(data.itemValue,result.blockValue,out var flags,out var density,out var texture);
        world.Density=density;world.Texture=texture;
        var c=new BlockChangeInfo(new BlockValueRef(result.blockPos),result.blockValue,true,data.holdingEntity.entityId){bChangeDensity=(flags&4)!=0,bUpdateLight=(flags&16)!=0,bChangeTexture=(flags&32)!=0,density=density,textureFull=new TextureFullArray(texture)};
        var native=new NetPackageSetBlock{localPlayerThatChanged=data.holdingEntity.entityId,blockChanges=new List<BlockChangeInfo>{c}};
        SeedPlacementNativeHooksReview.SendWithSeedIntent(SingletonMonoBehaviour<ConnectionManager>.Instance,native,false);
        AfterPlacement?.Invoke();SeedNativeBindingCandidate.Report(state,true,null);return true;
    }
}
