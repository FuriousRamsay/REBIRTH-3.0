using System;
using System.Globalization;

// Preserve authored native effects while digestion retains Food/Water authority.
internal static class RebirthPreservedConsumableEffects
{
    internal static void Apply(EntityPlayer player,ItemValue itemValue,bool authoritative,bool debug)
    {
        if (player == null || itemValue == null)
            return;

        // We still fire the native item-use event so healing, buffs, quests and other secondary
        // authored effects survive. Food/Water themselves are different: those reserves belong
        // exclusively to the REBIRTH digestion pipeline and may not jump on the use event.
        float nutritionBeforeEvent = player.Stats != null ? player.Stats.Food.Value : 0f;
        float hydrationBeforeEvent = player.Stats != null ? player.Stats.Water.Value : 0f;
        float nativeFoodPayloadBefore = player.Buffs != null ? player.Buffs.GetCustomVar("$foodAmountAdd") : 0f;
        float nativeWaterPayloadBefore = player.Buffs != null ? player.Buffs.GetCustomVar("$waterAmountAdd") : 0f;
        bool previousLocalEvent=player.MinEventContext.IsLocal;
        RebirthConsumptionTrace.Note("effects.before",player,item:itemValue);
        try
        {
            // This event is an authoritative server action even when its owner
            // is remote. Native buff/CVar actions use IsLocal to send to that owner.
            if(authoritative)player.MinEventContext.IsLocal=true;
            player.MinEventContext.ItemValue = itemValue;
            QuestEventManager.Current.UsedItem(itemValue);
            player.FireEvent(MinEvent.End[0]);
            RebirthConsumptionTrace.Note("effects.native-end",player,success:true,item:itemValue);

            EntityPlayerLocal local = player as EntityPlayerLocal;
            ItemClass itemClass = itemValue.ItemClass;
            if (local != null && itemClass != null && itemClass.Actions != null && itemClass.Actions.Length > 0)
            {
                ItemActionEat eat = itemClass.Actions[0] as ItemActionEat;
                if (eat != null && eat.smellUse > 0f)
                    local.Stealth.SetSmellEat(eat.smellUse);
            }
        }
        catch (Exception ex)
        {
            RebirthConsumptionTrace.Note("effects.exception",player,reason:ex.GetType().Name,success:false);
            if (debug)
                Log.Warning("[REBIRTH Metabolism] preserved consumable event failed: " + ex.Message);
        }
        finally
        {
            player.MinEventContext.IsLocal=previousLocalEvent;
            RebirthConsumptionTrace.Note("effects.restored-local",player);
            // Inherited vanilla consumables can recreate these payload CVars even when their
            // child item XML no longer contains the ModifyCVar node. Restore the pre-use values
            // so buffProcessConsumables has no native Food/Water payload left to process later.
            if (player.Buffs != null)
            {
                player.Buffs.SetCustomVar("$foodAmountAdd", nativeFoodPayloadBefore);
                player.Buffs.SetCustomVar("$waterAmountAdd", nativeWaterPayloadBefore);
            }

            if (player.Stats != null)
            {
                if (player.Stats.Food.Value > nutritionBeforeEvent)
                {
                    if (debug)
                        Log.Out("[REBIRTH Metabolism] blocked immediate pre-digestion Nutrition gain="
                            + (player.Stats.Food.Value - nutritionBeforeEvent).ToString("0.000", CultureInfo.InvariantCulture));
                    player.Stats.Food.Value = nutritionBeforeEvent;
                }
                if (player.Stats.Water.Value > hydrationBeforeEvent)
                {
                    if (debug)
                        Log.Out("[REBIRTH Metabolism] blocked immediate pre-absorption Hydration gain="
                            + (player.Stats.Water.Value - hydrationBeforeEvent).ToString("0.000", CultureInfo.InvariantCulture));
                    player.Stats.Water.Value = hydrationBeforeEvent;
                }
            }
        }
    }
}
