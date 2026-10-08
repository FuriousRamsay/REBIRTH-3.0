using System;

// Native definition adapter; no entity creation, world loading or save mutation.
internal static class RebirthRecoveryNativeChunkEvidence
{
    internal static bool TryMatch(byte[] payload,uint version,int x,int y,int z,RebirthGearTransferState pending,Guid publicationId,string ownerKey,int backpackClassId,int itemClassId,Func<bool> isCurrent,out int entityEnd)
    {
        entityEnd=0;
        if(!ThreadManager.IsMainThread()||isCurrent==null)return false;
        try
        {
            var items=ItemClass.list;
            var backpack=EntityClass.GetEntityClass(backpackClassId);var item=EntityClass.GetEntityClass(itemClassId);
            if(items==null||backpack?.classname!=typeof(EntityRebirthGearRecoveryBackpack)||item?.classname!=typeof(EntityRebirthGearRecoveryItem)||!isCurrent())return false;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(items,ItemClass.list)&&ReferenceEquals(backpack,EntityClass.GetEntityClass(backpackClassId))&&ReferenceEquals(item,EntityClass.GetEntityClass(itemClassId))&&isCurrent();
            Func<int,int> kind=id=>{if(!current())return -1;var definition=ItemClass.GetForId(id);return definition==null?-1:definition is ItemClassModifier?1:0;};
            int[] special={EntityClass.itemClass,EntityClass.fallingBlockClass,EntityClass.fallingBlocksClass,EntityClass.fallingTreeClass,EntityClass.playerMaleClass,EntityClass.playerFemaleClass,EntityClass.junkDroneClass};
            if(!current()||!RebirthRecoveryChunkEvidence.TryMatch(payload,version,x,y,z,pending,publicationId,ownerKey,backpackClassId,itemClassId,(byte)EnumSpawnerSource.StaticSpawner,special,Block.ItemsStartHere,kind,out int end)||!current())return false;
            var blocks=Block.list;var traders=TraderInfo.traderInfoList;
            if(blocks==null||traders==null)return false;
            Func<bool> tailCurrent=()=>current()&&ReferenceEquals(blocks,Block.list)&&ReferenceEquals(traders,TraderInfo.traderInfoList);
            Func<int,int> rentable=id=>tailCurrent()&&id>=0&&id<traders.Length&&traders[id]!=null?(traders[id].Rentable?1:0):-1;
            Func<int,bool> composite=id=>tailCurrent()&&id>=0&&id<blocks.Length&&blocks[id] is BlockCompositeTileEntity;
            Func<int,int,int> tile=(type,body)=>
            {
                if(!tailCurrent())return -1;int next;RebirthRecoveryTileKind utility;
                switch((TileEntityType)type)
                {
                    case TileEntityType.Workstation:return RebirthRecoveryStationRecord.TrySkip(payload,body,Block.ItemsStartHere,kind,out next)?next:-1;
                    case TileEntityType.Composite:return RebirthRecoveryCompositeRecord.TrySkip(payload,body,composite,out next)?next:-1;
                    case TileEntityType.VendingMachine:return RebirthRecoveryVendingRecord.TrySkip(payload,body,Block.ItemsStartHere,kind,rentable,out next)?next:-1;
                    case TileEntityType.Collector:utility=RebirthRecoveryTileKind.Collector;break;
                    case TileEntityType.Forge:utility=RebirthRecoveryTileKind.Forge;break;
                    case TileEntityType.Light:utility=RebirthRecoveryTileKind.Light;break;
                    case TileEntityType.Sleeper:utility=RebirthRecoveryTileKind.Sleeper;break;
                    case TileEntityType.Powered:utility=RebirthRecoveryTileKind.PoweredBlock;break;
                    case TileEntityType.PowerSource:utility=RebirthRecoveryTileKind.PowerSource;break;
                    case TileEntityType.Trigger:utility=RebirthRecoveryTileKind.PoweredTrigger;break;
                    case TileEntityType.PowerRangeTrap:utility=RebirthRecoveryTileKind.RangedTrap;break;
                    case TileEntityType.PowerMeleeTrap:utility=RebirthRecoveryTileKind.MeleeTrap;break;
                    default:return -1;
                }
                return RebirthRecoveryUtilityTileRecord.TrySkip(payload,body,utility,Block.ItemsStartHere,kind,out next)?next:-1;
            };
            if(!RebirthRecoveryChunkTail.TryValidate(payload,version,end,tile)||!tailCurrent())return false;
            entityEnd=end;return true;
        }
        catch{return false;}
    }
}