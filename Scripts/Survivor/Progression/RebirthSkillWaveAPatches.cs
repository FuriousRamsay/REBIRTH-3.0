using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Server observes the real lock-session request before accepting later client success evidence.
/// The target is resolved by name so this bridge does not require TELockServer to be public in the
/// compile-time GameManager surface used by RebirthUtils.
/// </summary>
[HarmonyPatch]
internal static class RebirthSkillWaveALockServerPatch
{
    private static readonly System.Type[] Signature =
    {
        typeof(int), typeof(Vector3i), typeof(int), typeof(int), typeof(string)
    };

    internal static System.Reflection.MethodBase ResolveTarget()
    {
        return typeof(GameManager).GetMethod(
            "TELockServer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, Signature, null);
    }

    private static bool Prepare()
    {
        return ResolveTarget() != null;
    }

    private static System.Reflection.MethodBase TargetMethod()
    {
        return ResolveTarget();
    }

    private static void Postfix(int _clrIdx, Vector3i _blockPos, int _lootEntityId, int _entityIdThatOpenedIt, string _customUi)
    {
        RegisterServerAttempt(_clrIdx, _blockPos, _entityIdThatOpenedIt, _customUi);
    }

    internal static void RegisterServerAttempt(int clrIdx, Vector3i blockPos, int playerEntityId, string customUi)
    {
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null || !c.IsServer || !string.Equals(customUi, "lockpick", System.StringComparison.OrdinalIgnoreCase)) return;
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        World world = GameManager.Instance.World;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        if (player == null) return;

        BlockValue bv = world.GetBlock(blockPos);
        Block secureBlock = bv.Block;
        if (secureBlock == null) return;

        // Do not depend on BlockSecureLoot being exposed by the compile-time game reference.
        // LockPickTime is the native property key on secure-loot blocks.
        float baseTime = 15f;
        string raw;
        if (secureBlock.Properties != null && secureBlock.Properties.Values != null &&
            secureBlock.Properties.Values.TryGetValue("LockPickTime", out raw))
            baseTime = StringParsers.ParseFloat(raw);

        float effective = EffectManager.GetValue(
            PassiveEffects.LockPickTime,
            player.inventory != null ? player.inventory.holdingItemItemValue : null,
            baseTime,
            player);

        float remaining = ReadPickTimeLeft(world, clrIdx, blockPos);
        RebirthSkillWaveAService.RegisterServerLockpickAttempt(player, blockPos, baseTime, effective, remaining);
    }

    private static float ReadPickTimeLeft(World world, int clrIdx, Vector3i blockPos)
    {
        if (world == null) return -1f;
        object tileEntity = null;
        try
        {
            // The secure-loot tile type and GetTileEntity overload have changed between game builds.
            // Resolve both by reflection so RebirthUtils does not compile against either surface.
            System.Reflection.MethodInfo getByCluster = AccessTools.Method(
                world.GetType(),
                "GetTileEntity",
                new System.Type[] { typeof(int), typeof(Vector3i) });
            if (getByCluster != null)
                tileEntity = getByCluster.Invoke(world, new object[] { clrIdx, blockPos });
            else
            {
                System.Reflection.MethodInfo getByPos = AccessTools.Method(
                    world.GetType(),
                    "GetTileEntity",
                    new System.Type[] { typeof(Vector3i) });
                if (getByPos != null) tileEntity = getByPos.Invoke(world, new object[] { blockPos });
            }
        }
        catch
        {
            return -1f;
        }

        if (tileEntity == null) return -1f;
        try
        {
            System.Reflection.FieldInfo field = AccessTools.Field(tileEntity.GetType(), "PickTimeLeft");
            if (field != null)
            {
                object raw = field.GetValue(tileEntity);
                if (raw is float) return (float)raw;
                if (raw != null) return System.Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture);
            }
            System.Reflection.PropertyInfo property = AccessTools.Property(tileEntity.GetType(), "PickTimeLeft");
            if (property != null && property.CanRead)
            {
                object raw = property.GetValue(tileEntity, null);
                if (raw is float) return (float)raw;
                if (raw != null) return System.Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch { }
        return -1f;
    }
}

internal static class RebirthSkillWaveAReflection
{
    internal static System.Type FindTypeSilently(System.Reflection.Assembly assembly, string typeName)
    {
        if (assembly == null || string.IsNullOrEmpty(typeName)) return null;

        System.Type exact = assembly.GetType(typeName, false, false);
        if (exact != null) return exact;

        System.Type[] types;
        try { types = assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
        if (types == null) return null;
        for (int i = 0; i < types.Length; i++)
        {
            System.Type candidate = types[i];
            if (candidate != null && string.Equals(candidate.Name, typeName, System.StringComparison.Ordinal))
                return candidate;
        }
        return null;
    }

    internal static System.Reflection.MethodInfo FindMethodSilently(System.Type type, string methodName, System.Type[] signature)
    {
        if (type == null || string.IsNullOrEmpty(methodName)) return null;
        return type.GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, signature ?? System.Type.EmptyTypes, null);
    }
}

/// <summary>
/// Fallback for game builds where TELockServer is not discoverable directly on GameManager.
/// Joined-client lock requests still arrive through NetPackageTELock; fields are read reflectively
/// so this fallback does not add another compile-time dependency on a version-specific package API.
/// </summary>
[HarmonyPatch]
internal static class RebirthSkillWaveANetLockServerPatch
{
    private static System.Reflection.MethodBase ResolveTarget()
    {
        System.Type t = RebirthSkillWaveAReflection.FindTypeSilently(typeof(GameManager).Assembly, "NetPackageTELock");
        if (t == null) return null;

        // ProcessPackage signatures vary by build. Resolve by name without Harmony's warning-producing helpers.
        System.Reflection.MethodInfo[] methods = t.GetMethods(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
            if (string.Equals(methods[i].Name, "ProcessPackage", System.StringComparison.Ordinal)) return methods[i];
        return null;
    }

    private static bool Prepare()
    {
        return RebirthSkillWaveALockServerPatch.ResolveTarget() == null && ResolveTarget() != null;
    }

    private static System.Reflection.MethodBase TargetMethod()
    {
        return ResolveTarget();
    }

    private static void Postfix(object __instance, World _world)
    {
        if (__instance == null || _world == null) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null || !c.IsServer) return;

        System.Type t = __instance.GetType();
        System.Reflection.FieldInfo typeField = AccessTools.Field(t, "type");
        System.Reflection.FieldInfo clrField = AccessTools.Field(t, "clrIdx");
        System.Reflection.FieldInfo xField = AccessTools.Field(t, "posX");
        System.Reflection.FieldInfo yField = AccessTools.Field(t, "posY");
        System.Reflection.FieldInfo zField = AccessTools.Field(t, "posZ");
        System.Reflection.FieldInfo playerField = AccessTools.Field(t, "entityIdThatOpenedIt");
        System.Reflection.FieldInfo uiField = AccessTools.Field(t, "customUi");
        if (typeField == null || clrField == null || xField == null || yField == null || zField == null || playerField == null) return;

        object typeValue = typeField.GetValue(__instance);
        if (typeValue == null || !string.Equals(typeValue.ToString(), "LockServer", System.StringComparison.OrdinalIgnoreCase)) return;

        int clrIdx = (int)clrField.GetValue(__instance);
        Vector3i pos = new Vector3i((int)xField.GetValue(__instance), (int)yField.GetValue(__instance), (int)zField.GetValue(__instance));
        int playerId = (int)playerField.GetValue(__instance);
        string customUi = uiField != null ? uiField.GetValue(__instance) as string : null;
        RebirthSkillWaveALockServerPatch.RegisterServerAttempt(clrIdx, pos, playerId, customUi);
    }
}

/// <summary>
/// Native secure-loot timer success is the only client-side lock event that reports success.
/// The target type is resolved at runtime because some b259 compile/reference surfaces do not expose
/// BlockSecureLoot even though the native secure-loot implementation exists in the running game.
/// </summary>
[HarmonyPatch]
internal static class RebirthSkillWaveALockSuccessPatch
{
    private static System.Reflection.MethodBase ResolveTarget()
    {
        System.Type secureType = RebirthSkillWaveAReflection.FindTypeSilently(typeof(Block).Assembly, "BlockSecureLoot");
        if (secureType != null)
        {
            System.Reflection.MethodBase exact = RebirthSkillWaveAReflection.FindMethodSilently(secureType, "EventData_Event", new System.Type[] { typeof(TimerEventData) });
            if (exact != null) return exact;
        }

        // Defensive rename fallback: only consider Block-derived secure-loot types with the exact
        // timer-event signature. This avoids binding to unrelated timer handlers.
        System.Type[] types;
        try { types = typeof(Block).Assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
        if (types == null) return null;
        for (int i = 0; i < types.Length; i++)
        {
            System.Type t = types[i];
            if (t == null || !typeof(Block).IsAssignableFrom(t)) continue;
            if (t.Name.IndexOf("SecureLoot", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            System.Reflection.MethodBase m = RebirthSkillWaveAReflection.FindMethodSilently(t, "EventData_Event", new System.Type[] { typeof(TimerEventData) });
            if (m != null) return m;
        }
        return null;
    }

    private static bool Prepare()
    {
        return ResolveTarget() != null;
    }

    private static System.Reflection.MethodBase TargetMethod()
    {
        return ResolveTarget();
    }

    private static void Postfix(TimerEventData timerData)
    {
        if (timerData == null || !(timerData.Data is object[] data) || data.Length < 4) return;
        EntityPlayerLocal player = data[3] as EntityPlayerLocal;
        if (player == null || !(data[2] is Vector3i)) return;
        RebirthSkillWaveAService.ReportClientLockpickSuccess(player, (Vector3i)data[2]);
    }
}

internal struct RebirthSkillWaveATradeState
{
    public EntityPlayerLocal Player;
    public int ItemType;
    public int RequestedCount;
    public int CurrencyBefore;
    public bool Eligible;
}

/// <summary>Completed NPC purchases only. Vending/player-owned machines are explicitly excluded.</summary>
[HarmonyPatch(typeof(ItemActionEntryPurchase), nameof(ItemActionEntryPurchase.OnActivated))]
internal static class RebirthSkillWaveABuyPatch
{
    private static void Prefix(ItemActionEntryPurchase __instance, out RebirthSkillWaveATradeState __state)
    {
        __state = CapturePurchase(__instance);
    }
    private static void Postfix(RebirthSkillWaveATradeState __state)
    {
        if (!__state.Eligible || __state.Player == null) return;
        int after = CurrencyCount(__state.Player.PlayerUI.xui);
        int spent = __state.CurrencyBefore - after;
        if (spent > 0) RebirthSkillWaveAService.ReportClientBarter(__state.Player, true, __state.ItemType, __state.RequestedCount, spent);
    }
    private static RebirthSkillWaveATradeState CapturePurchase(ItemActionEntryPurchase e)
    {
        RebirthSkillWaveATradeState s = new RebirthSkillWaveATradeState();
        if (e == null || e.ItemController == null || e.isOwner || e.isVending) return s;
        XUiC_TraderItemEntry entry = e.ItemController as XUiC_TraderItemEntry;
        if (entry == null || entry.Item == null || entry.Item.IsEmpty() || entry.InfoWindow == null || entry.InfoWindow.BuySellCounter == null) return s;
        EntityPlayerLocal player = e.ItemController.xui != null && e.ItemController.xui.playerUI != null ? e.ItemController.xui.playerUI.entityPlayer : null;
        if (player == null || !HasNpcTrader(e.ItemController.xui)) return s;
        s.Player = player; s.ItemType = entry.Item.itemValue.type; s.RequestedCount = entry.InfoWindow.BuySellCounter.Count; s.CurrencyBefore = CurrencyCount(e.ItemController.xui); s.Eligible = s.ItemType > 0 && s.RequestedCount > 0;
        return s;
    }
    internal static bool HasNpcTrader(XUi xui)
    {
        if (xui == null || xui.Trader == null) return false;
        object model = xui.Trader;
        object traderData = null;
        try
        {
            System.Reflection.FieldInfo traderField = AccessTools.Field(model.GetType(), "Trader");
            if (traderField != null) traderData = traderField.GetValue(model);
            if (traderData == null)
            {
                System.Reflection.PropertyInfo traderProperty = AccessTools.Property(model.GetType(), "Trader");
                if (traderProperty != null && traderProperty.CanRead) traderData = traderProperty.GetValue(model, null);
            }
        }
        catch { return false; }
        if (traderData == null) return false;

        // Player-owned/rentable traders are vending surfaces, not NPC barter training.
        try
        {
            object traderInfo = null;
            System.Reflection.FieldInfo infoField = AccessTools.Field(traderData.GetType(), "TraderInfo");
            if (infoField != null) traderInfo = infoField.GetValue(traderData);
            if (traderInfo == null)
            {
                System.Reflection.PropertyInfo infoProperty = AccessTools.Property(traderData.GetType(), "TraderInfo");
                if (infoProperty != null && infoProperty.CanRead) traderInfo = infoProperty.GetValue(traderData, null);
            }
            if (traderInfo != null)
            {
                if (ReadBoolMember(traderInfo, "PlayerOwned") || ReadBoolMember(traderInfo, "Rentable")) return false;
            }
        }
        catch { }
        return true;
    }

    private static bool ReadBoolMember(object instance, string memberName)
    {
        if (instance == null || string.IsNullOrEmpty(memberName)) return false;
        System.Reflection.FieldInfo field = AccessTools.Field(instance.GetType(), memberName);
        if (field != null)
        {
            object raw = field.GetValue(instance);
            return raw is bool && (bool)raw;
        }
        System.Reflection.PropertyInfo property = AccessTools.Property(instance.GetType(), memberName);
        if (property != null && property.CanRead)
        {
            object raw = property.GetValue(instance, null);
            return raw is bool && (bool)raw;
        }
        return false;
    }

    internal static int CurrencyCount(XUi xui)
    {
        if (xui == null || xui.PlayerInventory == null) return 0;
        ItemValue currency = ItemClass.GetItem(TraderInfo.CurrencyItem);
        int n = 0;
        if (xui.PlayerInventory.Backpack != null) n += xui.PlayerInventory.Backpack.GetItemCount(currency);
        if (xui.PlayerInventory.Toolbelt != null) n += xui.PlayerInventory.Toolbelt.GetItemCount(currency);
        return n;
    }
}

/// <summary>Completed NPC sales only. Success is proven by an increase in trader currency.</summary>
[HarmonyPatch(typeof(ItemActionEntrySell), nameof(ItemActionEntrySell.OnActivated))]
internal static class RebirthSkillWaveASellPatch
{
    private static void Prefix(ItemActionEntrySell __instance, out RebirthSkillWaveATradeState __state)
    {
        __state = new RebirthSkillWaveATradeState();
        if (__instance == null || __instance.ItemController == null || __instance.isOwner) return;
        XUiC_ItemStack entry = __instance.ItemController as XUiC_ItemStack;
        if (entry == null || entry.ItemStack == null || entry.ItemStack.IsEmpty() || entry.InfoWindow == null || entry.InfoWindow.BuySellCounter == null) return;
        XUi xui = __instance.ItemController.xui;
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player == null || !RebirthSkillWaveABuyPatch.HasNpcTrader(xui)) return;
        __state.Player = player; __state.ItemType = entry.ItemStack.itemValue.type; __state.RequestedCount = entry.InfoWindow.BuySellCounter.Count; __state.CurrencyBefore = RebirthSkillWaveABuyPatch.CurrencyCount(xui); __state.Eligible = __state.ItemType > 0 && __state.RequestedCount > 0;
    }
    private static void Postfix(RebirthSkillWaveATradeState __state)
    {
        if (!__state.Eligible || __state.Player == null) return;
        int after = RebirthSkillWaveABuyPatch.CurrencyCount(__state.Player.PlayerUI.xui);
        int received = after - __state.CurrencyBefore;
        if (received > 0) RebirthSkillWaveAService.ReportClientBarter(__state.Player, false, __state.ItemType, __state.RequestedCount, received);
    }
}


/// <summary>Rebirth-only player-facing name for the native Secret Stash surface now driven by Trading.</summary>
[HarmonyPatch(typeof(XUiC_TraderWindow), nameof(XUiC_TraderWindow.OnOpen))]
internal static class RebirthTradingSpecialStockLabelPatch
{
    private static void Postfix(XUiC_TraderWindow __instance)
    {
        if(__instance==null)return;
        bool rebirth=false;
        RebirthSurvivorOwnerStateSnapshot state=RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if(state!=null&&state.RebirthModeEnabled&&state.HasCharacter&&state.DefinitionsCompatible)rebirth=true;
        __instance.lblSecretStash=Localization.Get(rebirth?"xuiRebirthTradingSpecialStock":"xuiSecretStash");
        __instance.RefreshHeader();
    }
}
