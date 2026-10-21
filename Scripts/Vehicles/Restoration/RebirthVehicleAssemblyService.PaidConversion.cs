using System;

public static partial class RebirthVehicleAssemblyService
{
    internal static bool TryFinishPaidConversionRemoval(World world,RebirthVehicleTransferPlan plan,Func<bool> paidOriginal)
    {
        if(paidOriginal==null||!paidOriginal()||!RebirthVehicleAssemblyIndex.TryLocatePaidConversionPair(world,plan,paidOriginal,out var native))return false;
        var position=new Vector3i(plan.Target.X,plan.Target.Y,plan.Target.Z);
        var block=world.GetBlock(position);var content=block.Block==null?null:RebirthVehicleDefinitionRegistry.GetContentForBlock(block.Block.GetBlockName());
        if(block.isair||content?.ContentId!=plan.ReadBefore().ContentId||!paidOriginal()||
            !RebirthVehicleAssemblyIndex.TryLocatePaidConversionPair(world,plan,paidOriginal,out var same)||!ReferenceEquals(native,same))return false;
        // Native vehicle already carries this original's immutable After. Finish
        // only its old source removal; never spawn or consume another kit.
        world.SetBlockRPC((BlockValueRef)position,BlockValue.Air);
        return paidOriginal()&&world.GetBlock(position).isair&&world.GetTileEntity(position)==null&&
            RebirthVehicleTransferResolver.TryObserve(world,plan,paidOriginal,out var after,out _,out var id)&&id==native.entityId&&
            RebirthVehicleAssemblySerializer.ToBase64(after)==plan.AfterPayload;
    }
    // No kit search/debit/refund occurs here. The actual coordinator requires its
    // independently saved exact kit receipt before entering the existing native
    // conversion, whose stable assembly identity and content binding are kept.
    internal static bool TryConvertPaid(World world,EntityPlayer player,RebirthVehicleTransferPlan plan,Func<bool> current)
    {
        if(current==null||!current()||plan?.Target?.ConvertsToEntity!=true||world==null||world.IsRemote())return false;
        var position=new Vector3i(plan.Target.X,plan.Target.Y,plan.Target.Z);var tile=world.GetTileEntity(position) as TileEntityRepairableVehicleRebirth;
        if(tile==null||RebirthVehicleAssemblySerializer.ToBase64(tile.Assembly)!=plan.AfterPayload)return false;
        var content=RebirthVehicleDefinitionRegistry.GetContent(tile.Assembly.ContentId);
        if(content==null||content.Availability!=RebirthVehicleContentAvailability.Enabled||!RebirthVehicleEntityFactory.HasPersistentOwner(player))return false;
        if(!TryConvert(world,position,tile,player,content,out _,out _))return false;
        return current()&&world.GetBlock(position).isair&&world.GetTileEntity(position)==null&&
            RebirthVehicleTransferResolver.TryObserve(world,plan,current,out var actual,out _,out var id)&&id>=0&&
            ReferenceEquals(world.GetEntity(id)?.world,world)&&RebirthVehicleAssemblySerializer.ToBase64(actual)==plan.AfterPayload;
    }
}
