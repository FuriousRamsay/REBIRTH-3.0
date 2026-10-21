using System;
using HarmonyLib;

/// <summary>Suppress only the replaced native Duke introduction in REBIRTH progression.</summary>
public static class RebirthDukeNoteNotification
{
    public const string MessageKey = "buffDukeNoteToolTip";

    public static bool ShouldSuppress(string messageKey)
        => RebirthToolbeltCapacity.Enabled && string.Equals(messageKey, MessageKey, StringComparison.Ordinal);

    public static void Clear(EntityPlayer player)
    {
        if (RebirthToolbeltCapacity.Enabled && player is EntityPlayerLocal local)
            GameManager.RemovePinnedTooltip(local, MessageKey);
    }
}

// Both the buff-start and entered-game XML effects reach this native action.
// Gate the key rather than localized text; unrelated pinned warnings remain native.
[HarmonyPatch(typeof(MinEventActionPinToolbeltMessage), nameof(MinEventActionPinToolbeltMessage.Execute))]
public static class RebirthDukeNotePinPatch
{
    public static bool Prefix(MinEventActionPinToolbeltMessage __instance)
        => !RebirthDukeNoteNotification.ShouldSuppress(__instance.messageKey);
}
