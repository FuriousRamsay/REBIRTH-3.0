using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

#nullable disable

public static class RebirthBlockPickupPatchInstaller
{
    public const string HarmonyId = "rebirth.fresh.blockpickup";
    private const string LagTestFlagFileName = "rebirth_blockpickup_lagtest_disabled.flag";
    private const string LagTestOneShotPayload = "REBIRTH_BLOCK_PICKUP_ONE_SHOT_V2";

    private static readonly HarmonyLib.Harmony Harmony = new HarmonyLib.Harmony(HarmonyId);
    private static bool installAttempted;
    private static bool installed;
    private static int patchedMethods;
    private static int failedMethods;
    private static bool lagTestDisabled;
    private static bool persistentLagTestDisabled;
    private static bool lagTestScheduled;
    private static bool restartRequired;

    public static bool Installed { get { return installed; } }
    public static bool Active { get { return installed && !lagTestDisabled; } }
    public static bool LagTestDisabled { get { return lagTestDisabled; } }
    public static bool PersistentLagTestDisabled { get { return persistentLagTestDisabled; } }
    public static bool LagTestScheduled { get { return lagTestScheduled; } }
    public static bool RestartRequired { get { return restartRequired; } }
    public static int PatchedMethods { get { return patchedMethods; } }
    public static int FailedMethods { get { return failedMethods; } }

    public static string Install()
    {
        if (!installAttempted)
        {
            persistentLagTestDisabled = ConsumeOneShotLagTestFlag();
            lagTestScheduled = false;
            if (persistentLagTestDisabled)
            {
                lagTestDisabled = true;
                restartRequired = false;
                installAttempted = true;
                installed = false;
                patchedMethods = 0;
                failedMethods = 0;
                Log.Out("[REBIRTH BlockPickup] One-shot lag comparison session active. "
                    + "The flag has already been consumed; restart after testing to restore Block Pickup automatically.");
                Log.Out(Status());
                return Status();
            }
        }

        if (lagTestDisabled || installed)
            return Status();

        installAttempted = true;
        restartRequired = false;
        patchedMethods = 0;
        failedMethods = 0;

        // Fixed 3.1 targets are explicit Harmony classes. Runtime discovery is
        // retained only for concrete Block subclass overrides, because Harmony
        // does not propagate a base virtual patch into overridden methods.
        InstallExplicitBindings();

        MethodInfo hasPostfix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Postfix_HasBlockActivationCommands));
        MethodInfo getPostfix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Postfix_GetBlockActivationCommands));
        MethodInfo activatedPrefix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Prefix_OnBlockActivated));
        MethodInfo damagedPrefix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Prefix_OnBlockDamaged));
        MethodInfo compositeActivationTextPostfix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Postfix_BlockCompositeTileEntity_GetActivationText));
        MethodInfo workstationActivationTextPostfix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Postfix_BlockWorkstation_GetActivationText));
        MethodInfo generalActivationTextPostfix = AccessTools.Method(typeof(Harmony_RebirthBlockPickupPatches), nameof(Harmony_RebirthBlockPickupPatches.Postfix_Block_GetActivationText));

        List<Type> allTypes = new List<Type>();
        HashSet<Type> uniqueTypes = new HashSet<Type>();
        AddBlockTypes(typeof(Block).Assembly, allTypes, uniqueTypes);
        AddBlockTypes(typeof(RebirthBlockPickupPatchInstaller).Assembly, allTypes, uniqueTypes);

        HashSet<MethodBase> seen = new HashSet<MethodBase>();

        for (int i = 0; i < allTypes.Count; i++)
        {
            Type type = allTypes[i];
            if (type == null || type.IsAbstract || !typeof(Block).IsAssignableFrom(type))
                continue;

            // Block, BlockCompositeTileEntity and BlockWorkstation are fixed 3.1 targets
            // already covered by explicit Harmony classes above. Only patch declared
            // overrides on concrete subclasses here so a base method never receives the
            // same postfix twice.
            if (type != typeof(Block) &&
                type != typeof(BlockCompositeTileEntity) &&
                type != typeof(BlockWorkstation))
            {
                MethodInfo activationTextPostfix = typeof(BlockWorkstation).IsAssignableFrom(type)
                    ? workstationActivationTextPostfix
                    : typeof(BlockCompositeTileEntity).IsAssignableFrom(type)
                        ? compositeActivationTextPostfix
                        : generalActivationTextPostfix;
                PatchDeclared(type, "GetActivationText",
                    new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) },
                    null, activationTextPostfix, seen);
            }

            PatchDeclared(type, "HasBlockActivationCommands",
                new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) },
                null, hasPostfix, seen);

            PatchDeclared(type, "GetBlockActivationCommands",
                new[] { typeof(WorldBase), typeof(BlockValue), typeof(Vector3i), typeof(EntityAlive) },
                null, getPostfix, seen);

            PatchDeclared(type, "OnBlockActivated",
                new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) },
                activatedPrefix, null, seen);

            PatchDeclared(type, "OnBlockDamaged",
                new[]
                {
                    typeof(WorldBase), typeof(BlockValueRef), typeof(BlockValue), typeof(int), typeof(int),
                    typeof(ItemActionAttack.AttackHitInfo), typeof(bool), typeof(bool), typeof(int)
                },
                damagedPrefix, null, seen);
        }

        installed = patchedMethods > 0 && failedMethods == 0;
        if (!installed && patchedMethods > 0)
        {
            int successfulBeforeRollback = patchedMethods;
            Harmony.UnpatchSelf();
            patchedMethods = 0;
            installAttempted = false;
            Log.Error("[REBIRTH BlockPickup] Installation was incomplete; rolled back "
                + successfulBeforeRollback + " Block Pickup patches so no partial feature remains active.");
        }

        if (!installed) Log.Warning(Status());
        else if (RebirthLogSettings.BlockPickupLoggingEnabled) Log.Out(Status());
        return Status();
    }


    private static void InstallExplicitBindings()
    {
        Type[] patchClasses =
        {
            typeof(RebirthBlockPickupBlocksCreateBinding),
            typeof(RebirthBlockPickupBaseActivationTextBinding),
            typeof(RebirthBlockPickupCompositeInitBinding),
            typeof(RebirthBlockPickupWorkstationPlaceBinding),
            typeof(RebirthBlockPickupWorkstationRemovedBinding),
            typeof(RebirthBlockPickupWorkstationActivationTextBinding),
            typeof(RebirthBlockPickupTileEntityCanLockBinding),
            typeof(RebirthBlockPickupSaveWorldBinding),
            typeof(RebirthBlockPickupClientBinding),
            typeof(RebirthBlockPickupStorageDestroyBinding),
            typeof(RebirthBlockPickupStorageAddedBinding),
            typeof(RebirthBlockPickupCompositeActivationTextBinding),
            typeof(RebirthBlockPickupItemBindingValueBinding),
            typeof(RebirthBlockPickupItemCanStackBinding)
        };

        for (int i = 0; i < patchClasses.Length; i++)
        {
            try
            {
                Harmony.CreateClassProcessor(patchClasses[i]).Patch();
                patchedMethods++;
            }
            catch (Exception ex)
            {
                failedMethods++;
                Log.Error("[REBIRTH BlockPickup] Failed explicit 3.1 patch class "
                    + patchClasses[i].FullName + ": " + ex);
            }
        }
    }


    private static void AddBlockTypes(
        Assembly assembly,
        List<Type> destination,
        HashSet<Type> uniqueTypes)
    {
        if (assembly == null)
            return;

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types ?? Type.EmptyTypes;
        }
        catch (Exception ex)
        {
            failedMethods++;
            Log.Error("[REBIRTH BlockPickup] Could not inspect block types in assembly "
                + assembly.FullName + ": " + ex.GetType().Name + ": " + ex.Message);
            return;
        }

        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type != null && uniqueTypes.Add(type))
                destination.Add(type);
        }
    }

    private static void PatchDeclared(
        Type type,
        string methodName,
        Type[] parameterTypes,
        MethodInfo prefix,
        MethodInfo postfix,
        HashSet<MethodBase> seen)
    {
        MethodInfo original = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null,
            parameterTypes,
            null);

        if (original == null || !seen.Add(original))
            return;

        try
        {
            Harmony.Patch(
                original,
                prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(postfix) : null);
            patchedMethods++;
        }
        catch (Exception ex)
        {
            failedMethods++;
            Log.Error("[REBIRTH BlockPickup] Failed to patch " + type.FullName + "." + methodName
                + ": " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static string SetLagTestDisabled(bool disabled)
    {
        if (disabled)
        {
            lagTestScheduled = WriteOneShotLagTestFlag();
            restartRequired = lagTestScheduled;
            return Status();
        }

        DeleteLagTestFlag();
        lagTestScheduled = false;

        // A one-shot disabled startup intentionally has no Block Pickup patches.
        // Re-enabling within that process would miss early block-definition work,
        // so a clean restart remains required.
        restartRequired = lagTestDisabled || !installed;
        return Status();
    }

    private static bool ConsumeOneShotLagTestFlag()
    {
        try
        {
            string path = GetLagTestFlagPath();
            if (!SdFile.Exists(path))
                return false;

            string payload = SdFile.ReadAllText(path);
            SdFile.Delete(path);

            if (string.Equals((payload ?? string.Empty).Trim(),
                LagTestOneShotPayload, StringComparison.Ordinal))
            {
                return true;
            }

            // Older patches created a persistent disable file. It caused the XML
            // CanPickup removal to remain active while all runtime hooks were absent.
            // Clear that legacy state and restore Block Pickup immediately.
            Log.Warning("[REBIRTH BlockPickup] Cleared legacy persistent lag-test disable flag. "
                + "Block Pickup will install normally; future lag tests are one-shot only.");
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH BlockPickup] Could not consume lag-test flag: " +
                ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static bool WriteOneShotLagTestFlag()
    {
        try
        {
            string path = GetLagTestFlagPath();
            SdFile.WriteAllText(path, LagTestOneShotPayload + "\n");
            return SdFile.Exists(path) && string.Equals((SdFile.ReadAllText(path) ?? string.Empty).Trim(), LagTestOneShotPayload, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH BlockPickup] Could not schedule one-shot lag test: " +
                ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static void DeleteLagTestFlag()
    {
        try
        {
            string path = GetLagTestFlagPath();
            if (SdFile.Exists(path))
                SdFile.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH BlockPickup] Could not clear lag-test flag: " +
                ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string GetLagTestFlagPath()
    {
        return Path.Combine(GameIO.GetUserGameDataDir(), LagTestFlagFileName);
    }

    public static string Status()
    {
        return "[REBIRTH BlockPickup] harmonyId=" + HarmonyId
            + " installAttempted=" + installAttempted
            + " installed=" + installed
            + " active=" + Active
            + " lagTestDisabled=" + lagTestDisabled
            + " persistentLagTestDisabled=" + persistentLagTestDisabled
            + " lagTestScheduled=" + lagTestScheduled
            + " restartRequired=" + restartRequired
            + " patchedMethods=" + patchedMethods
            + " failedMethods=" + failedMethods;
    }
}
