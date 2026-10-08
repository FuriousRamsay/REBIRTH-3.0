using System;
using System.Collections.Generic;
using HarmonyLib;

// Private item-effect root. A token is staged only during native method execution and committed only on its successful normal return.
internal static class RebirthSelfMedicalItemEffectScope
{
    [ThreadStatic]
    static Root active;
    internal sealed class Root
    {
        internal readonly ItemActionEat Action;
        internal readonly EntityPlayer Player;
        internal readonly ItemStack Stack;
        internal readonly ItemValue Value;
        internal readonly ItemActionData Data;
        internal readonly World World;
        internal readonly ItemInventoryData Inventory;
        internal readonly ConnectionManager Connection;
        internal readonly object Peer;
        internal readonly List<Action> Commits = new List<Action>();
        internal bool Revoked, Finished;
        internal Root(ItemActionEat a, EntityPlayer p, ItemStack s, ItemActionData d)
        {
            Action = a;
            Player = p;
            Stack = s;
            Value = s.itemValue;
            Data = d;
            Inventory = d?.invData;
            World = p.world;
            Connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            Peer = World.IsRemote() ? Connection.connectionToServer[0] : null;
        }

        internal bool Current(bool effect = true) => !Revoked
            && !Finished
            && ReferenceEquals(active, this)
            && Player.world == World
            && RebirthSelfMedicalSimulationOwner.Current(Player)
            && ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance, Connection)
            && (!World.IsRemote() || ReferenceEquals(Connection.connectionToServer[0], Peer))
            && !Player.IsDead()
            && ReferenceEquals(World.GetEntity(Player.entityId), Player)
            && (!effect
            || ReferenceEquals(Stack.itemValue, Value))
            && (Data == null
            || ReferenceEquals(Data.invData, Inventory)
            && ReferenceEquals(Inventory.holdingEntity, Player)
            && ReferenceEquals(Inventory.itemStack, Stack));
    }

    internal static Root Enter(ItemActionEat action, EntityAlive entity, ItemStack stack, ItemActionData data)
    {
        if (active != null)
        {
            active.Revoked = true;
            return null;
        }

        var p = entity as EntityPlayer;
        if (action == null
            || p?.world == null
            || !RebirthSelfMedicalSimulationOwner.Current(p)
            || stack?.itemValue?.ItemClass == null
            || !RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(stack.itemValue.ItemClass.GetItemName()))
            return null;
        bool exact = false;
        if (stack.itemValue.ItemClass.Actions == null)
            return null;
        foreach (var a in stack.itemValue.ItemClass.Actions)
            if (ReferenceEquals(a, action))
                exact = true;
        if (!exact)
            return null;
        var r = new Root(action, p, stack, data);
        active = r;
        return r;
    }

    internal static bool Stage(EntityPlayer p, Action commit)
    {
        var r = active;
        if (r == null
            || !ReferenceEquals(r.Player, p)
            || !ReferenceEquals(p.MinEventContext.ItemValue, r.Value)
            || !r.Current())
            return false;
        r.Commits.Add(commit);
        return true;
    }

    internal static void Complete(Root r, bool success)
    {
        if (r == null
            || !success
            || !r.Current(false))
            return;
        r.Finished = true;
        foreach (var c in r.Commits)
            c();
    }

    internal static Exception Exit(Root r, Exception error)
    {
        if (r != null)
        {
            r.Revoked = true;
            if (ReferenceEquals(active, r))
                active = null;
        }

        return error;
    }
}

[HarmonyPatch(typeof(ItemActionEat), nameof(ItemActionEat.ExecuteInstantAction))]
internal static class RebirthSelfMedicalInstantEffectPatch
{
    static void Prefix(ItemActionEat __instance, EntityAlive ent, ItemStack stack, out RebirthSelfMedicalItemEffectScope.Root __state) => __state = RebirthSelfMedicalItemEffectScope.Enter(__instance, ent, stack, null);
    [HarmonyPriority(Priority.Last)]
    static void Postfix(bool __runOriginal, bool __result, RebirthSelfMedicalItemEffectScope.Root __state) => RebirthSelfMedicalItemEffectScope.Complete(__state, __runOriginal
            && __result);
    static Exception Finalizer(Exception __exception, RebirthSelfMedicalItemEffectScope.Root __state) => RebirthSelfMedicalItemEffectScope.Exit(__state, __exception);
}

[HarmonyPatch(typeof(ItemActionEat), "consume")]
internal static class RebirthSelfMedicalConsumeEffectPatch
{
    static void Prefix(ItemActionEat __instance, ItemActionData _actionData, out RebirthSelfMedicalItemEffectScope.Root __state)
    {
        __state = null;
        if (_actionData is ItemActionEat.MyInventoryData d
            && d.bEatingStarted)
            __state = RebirthSelfMedicalItemEffectScope.Enter(__instance, d.invData.holdingEntity, d.invData.itemStack, d);
    }

    [HarmonyPriority(Priority.Last)]
    static void Postfix(bool __runOriginal, RebirthSelfMedicalItemEffectScope.Root __state) => RebirthSelfMedicalItemEffectScope.Complete(__state, __runOriginal);
    static Exception Finalizer(Exception __exception, RebirthSelfMedicalItemEffectScope.Root __state) => RebirthSelfMedicalItemEffectScope.Exit(__state, __exception);
}


