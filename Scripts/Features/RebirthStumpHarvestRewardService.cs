using System.Text;

#nullable disable

/// <summary>
/// Manual reward service for stored stump/vehicle context.
/// 
/// This is intentionally command/manual first. It prevents the old vehicle-stump behavior from
/// being enabled globally before dedicated-server authority is verified.
/// </summary>
public static class RebirthStumpHarvestRewardService
{
    public static bool TryPreview(Vector3i blockPos, out string report)
    {
        RebirthStumpHarvestRewardResult result = Evaluate(blockPos, false);
        report = Format(result, true);
        return result.Kind == RebirthStumpHarvestRewardResultKind.PreviewEligible
            || result.Kind == RebirthStumpHarvestRewardResultKind.Granted;
    }

    public static bool TryGrant(Vector3i blockPos, out string report)
    {
        RebirthStumpHarvestRewardResult result = Evaluate(blockPos, true);
        report = Format(result, false);
        return result.Kind == RebirthStumpHarvestRewardResultKind.Granted;
    }

    public static bool TryPreviewNewest(out string report)
    {
        Vector3i pos;
        RebirthStumpHarvestContextStore.Context context;
        if (!RebirthStumpHarvestContextStore.TryGetNewest(out pos, out context))
        {
            RebirthStumpHarvestRewardResult result = new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.NoContext,
                Message = "No stored context."
            };
            report = Format(result, true);
            return false;
        }

        return TryPreview(pos, out report);
    }

    public static bool TryGrantNewest(out string report)
    {
        Vector3i pos;
        RebirthStumpHarvestContextStore.Context context;
        if (!RebirthStumpHarvestContextStore.TryGetNewest(out pos, out context))
        {
            RebirthStumpHarvestRewardResult result = new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.NoContext,
                Message = "No stored context."
            };
            report = Format(result, false);
            return false;
        }

        return TryGrant(pos, out report);
    }

    private static RebirthStumpHarvestRewardResult Evaluate(Vector3i blockPos, bool grant)
    {
        if (!RebirthStumpHarvestRewardPolicy.Enabled)
        {
            return new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.Disabled,
                BlockPos = blockPos,
                Message = "Reward policy disabled."
            };
        }

        RebirthStumpHarvestContextStore.Context context;
        bool hasContext = RebirthStumpHarvestContextStore.TryGet(blockPos, out context);
        if (RebirthStumpHarvestRewardPolicy.RequireContext && !hasContext)
        {
            return new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.NoContext,
                BlockPos = blockPos,
                Message = "No context for position."
            };
        }

        if (hasContext && RebirthStumpHarvestRewardPolicy.RequireVehicleAttachedContext && !context.DestroyerAttachedToVehicle)
        {
            return new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.RejectedNotVehicleAttached,
                BlockPos = blockPos,
                PlayerEntityId = context.DestroyerEntityId,
                VehicleAttached = context.DestroyerAttachedToVehicle,
                Message = "Context was not vehicle-attached."
            };
        }

        if (!grant)
        {
            return new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.PreviewEligible,
                BlockPos = blockPos,
                PlayerEntityId = hasContext ? context.DestroyerEntityId : 0,
                RewardItemName = RebirthStumpHarvestRewardPolicy.RewardItemName,
                RewardCount = RebirthStumpHarvestRewardPolicy.RewardCount,
                VehicleAttached = hasContext && context.DestroyerAttachedToVehicle,
                ContextConsumed = false,
                Message = "Descriptive preview only. Policy/context are eligible; inventory acceptance is checked only by grant."
            };
        }

        if (!hasContext)
        {
            return new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.NoContext,
                BlockPos = blockPos,
                Message = "Grant requires context."
            };
        }

        // Reserve/consume the exact context generation before attempting the reward. A clean
        // no-capacity failure restores the reservation. Once inventory mutation is attempted, an
        // exception is treated as indeterminate and the reservation stays consumed so a repeated
        // manual request cannot duplicate a possibly-granted reward.
        if(!RebirthStumpHarvestContextStore.TryReserve(blockPos,out context))
        {
            return new RebirthStumpHarvestRewardResult{Kind=RebirthStumpHarvestRewardResultKind.NoContext,BlockPos=blockPos,Message="Context changed before grant reservation."};
        }

        EntityPlayer player = TryGetPlayer(context.DestroyerEntityId);
        if (player == null)
        {
            return RestoreAndReturn(blockPos,context,new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.NoPlayer,
                BlockPos = blockPos,
                PlayerEntityId = context.DestroyerEntityId,
                Message = "Could not resolve player."
            });
        }

        string itemName = RebirthStumpHarvestRewardPolicy.RewardItemName;
        int count = RebirthStumpHarvestRewardPolicy.RewardCount;
        if (string.IsNullOrEmpty(itemName) || count <= 0)
        {
            return RestoreAndReturn(blockPos,context,new RebirthStumpHarvestRewardResult
            {
                Kind = RebirthStumpHarvestRewardResultKind.InvalidReward,
                BlockPos = blockPos,
                PlayerEntityId = context.DestroyerEntityId,
                RewardItemName = itemName,
                RewardCount = count,
                Message = "Invalid reward item/count."
            });
        }

        ItemStack rewardStack;
        try
        {
            ItemValue item = ItemClass.GetItem(itemName, false);
            if (item == null || item.IsEmpty() || item.ItemClass == null)
                return RestoreAndReturn(blockPos,context,new RebirthStumpHarvestRewardResult
                {
                    Kind=RebirthStumpHarvestRewardResultKind.InvalidReward,BlockPos=blockPos,
                    PlayerEntityId=context.DestroyerEntityId,RewardItemName=itemName,RewardCount=count,
                    Message="Reward item could not be resolved; no inventory mutation attempted."
                });
            rewardStack = new ItemStack(item, count);
        }
        catch
        {
            return RestoreAndReturn(blockPos,context,new RebirthStumpHarvestRewardResult
            {
                Kind=RebirthStumpHarvestRewardResultKind.InvalidReward,BlockPos=blockPos,
                PlayerEntityId=context.DestroyerEntityId,RewardItemName=itemName,RewardCount=count,
                Message="Reward construction failed; no inventory mutation attempted."
            });
        }
        bool mutationAttempted=false;
        try
        {
            bool given=false;
            if(player.inventory.CanStackNoEmpty(rewardStack))
            {
                mutationAttempted=true;
                given=player.inventory.AddItem(rewardStack);
            }
            if(!given)
            {
                mutationAttempted=true;
                given=player.bag.AddItem(rewardStack);
            }
            if(!given)
            {
                mutationAttempted=true;
                given=player.inventory.AddItem(rewardStack);
            }
            if (!given)
            {
                if(rewardStack.count>=count)
                {
                    return RestoreAndReturn(blockPos,context,new RebirthStumpHarvestRewardResult
                    {
                        Kind = RebirthStumpHarvestRewardResultKind.InventoryFull,
                        BlockPos = blockPos,
                        PlayerEntityId = context.DestroyerEntityId,
                        RewardItemName = itemName,
                        RewardCount = count,
                        VehicleAttached = context.DestroyerAttachedToVehicle,
                        Message = "Inventory/bag could not accept reward; no quantity moved."
                    });
                }
                return new RebirthStumpHarvestRewardResult
                {
                    Kind=RebirthStumpHarvestRewardResultKind.IndeterminateAfterCommit,BlockPos=blockPos,PlayerEntityId=context.DestroyerEntityId,
                    RewardItemName=itemName,RewardCount=count,VehicleAttached=context.DestroyerAttachedToVehicle,ContextConsumed=true,
                    Message="Reward was only partially accepted; context remains consumed to prevent duplicate replay."
                };
            }
        }
        catch
        {
            if(!mutationAttempted)
                return RestoreAndReturn(blockPos,context,new RebirthStumpHarvestRewardResult
                {
                    Kind=RebirthStumpHarvestRewardResultKind.InvalidReward,BlockPos=blockPos,
                    PlayerEntityId=context.DestroyerEntityId,RewardItemName=itemName,RewardCount=count,
                    Message="Reward failed before inventory mutation."
                });
            return new RebirthStumpHarvestRewardResult
            {
                Kind=RebirthStumpHarvestRewardResultKind.IndeterminateAfterCommit,BlockPos=blockPos,PlayerEntityId=context.DestroyerEntityId,
                RewardItemName=itemName,RewardCount=count,VehicleAttached=context.DestroyerAttachedToVehicle,ContextConsumed=true,
                Message=mutationAttempted?"Reward mutation became indeterminate; context remains consumed to prevent duplicate retry.":"Reward failed before mutation."
            };
        }

        if(!RebirthStumpHarvestRewardPolicy.ConsumeContextOnReward)
            RebirthStumpHarvestContextStore.TryRestoreReservation(blockPos,context);

        return new RebirthStumpHarvestRewardResult
        {
            Kind = RebirthStumpHarvestRewardResultKind.Granted,
            BlockPos = blockPos,
            PlayerEntityId = context.DestroyerEntityId,
            RewardItemName = itemName,
            RewardCount = count,
            VehicleAttached = context.DestroyerAttachedToVehicle,
            ContextConsumed = RebirthStumpHarvestRewardPolicy.ConsumeContextOnReward,
            Message = "Reward granted."
        };
    }

    private static RebirthStumpHarvestRewardResult RestoreAndReturn(Vector3i blockPos,RebirthStumpHarvestContextStore.Context context,RebirthStumpHarvestRewardResult result)
    {
        bool restored = RebirthStumpHarvestContextStore.TryRestoreReservation(blockPos,context);
        result.ContextConsumed = !restored;
        result.Message = (result.Message ?? string.Empty) + (restored
            ? " Context restored for retry."
            : " Context expired, was superseded, or changed world; retry evidence was not restored.");
        return result;
    }

    private static EntityPlayer TryGetPlayer(int entityId)
    {
        try
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return null;

            return GameManager.Instance.World.GetEntity(entityId) as EntityPlayer;
        }
        catch
        {
            return null;
        }
    }

    private static string Format(RebirthStumpHarvestRewardResult result, bool preview)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine(preview ? "[RebirthStumpHarvestRewardService] preview:" : "[RebirthStumpHarvestRewardService] grant:");
        sb.AppendLine("  result: " + result.Kind);
        sb.AppendLine("  message: " + (result.Message ?? string.Empty));
        sb.AppendLine("  blockPos: " + result.BlockPos);
        sb.AppendLine("  playerEntityId: " + result.PlayerEntityId);
        sb.AppendLine("  vehicleAttached: " + result.VehicleAttached);
        sb.AppendLine("  rewardItemName: " + (result.RewardItemName ?? string.Empty));
        sb.AppendLine("  rewardCount: " + result.RewardCount);
        sb.AppendLine("  contextConsumed: " + result.ContextConsumed);
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        return "[RebirthStumpHarvestRewardService] manual only; preview is descriptive (not a grant guarantee); grant reserves context before mutation and consumes indeterminate/partial outcomes to prevent duplicate replay.";
    }
}
