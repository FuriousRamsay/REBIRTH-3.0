using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Existing character/native-owner scope only. This creates no identity token,
// character record or mutation capability. Detached validation is not authority.
internal static class RebirthOriginalActionScope
{
    private const string NativePrefix="native-mode:";
    internal static bool TryNormalize(string value,out string normalized)
    {
        if(RebirthSurvivorRequestScope.TryNormalize(value,out normalized))return true;
        normalized=null;
        if(value==null||value.Length>512||!value.StartsWith(NativePrefix,StringComparison.Ordinal))return false;
        string canonical=value.Substring(NativePrefix.Length);
        if(canonical.Length<1||canonical!=canonical.Trim()||canonical.Any(char.IsControl))return false;
        normalized=value;return true;
    }
    private static string Key(string canonical)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-","").ToLowerInvariant();}
    internal static bool Validate(string ownerStorageKey,string creation)
    {
        if(ownerStorageKey?.Length!=64||ownerStorageKey.Any(value=>!(value>='0'&&value<='9'||value>='a'&&value<='f'))||
            !TryNormalize(creation,out var normalized)||normalized!=creation)return false;
        return !creation.StartsWith(NativePrefix,StringComparison.Ordinal)||Key(creation.Substring(NativePrefix.Length))==ownerStorageKey;
    }
    // Authority-side runtime comparison. Remote owner clients retain their own
    // existing local native/projection check; they cannot authorize host effects.
    internal static bool Matches(EntityPlayer actor,string ownerStorageKey,string creation)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||actor==null||actor.IsDead()||!ReferenceEquals(actor.world,world)||
            !ReferenceEquals(world.GetEntity(actor.entityId),actor)||!Validate(ownerStorageKey,creation)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=ownerStorageKey)return false;
        string current=RebirthCamperCharacterAccess.CurrentCreation(actor);
        if(!creation.StartsWith(NativePrefix,StringComparison.Ordinal))return RebirthSurvivorRequestScope.Matches(creation,current);
        if(RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        var native=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
        if(native==null&&ReferenceEquals(world.GetPrimaryPlayer(),actor))native=GameManager.Instance.getPersistentPlayerID(null);
        string canonical=native?.CombinedString;
        return !string.IsNullOrWhiteSpace(canonical)&&canonical==canonical.Trim()&&Key(canonical)==ownerStorageKey&&
            creation==NativePrefix+canonical&&current==creation;
    }
}
