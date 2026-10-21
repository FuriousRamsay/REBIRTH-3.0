#if REBIRTH_DEBUG
using System;
using System.Linq;
using System.IO;
using HarmonyLib;

// Fixture reservation DOUBLE, never a durable library receipt/hold.
internal sealed class RebirthProvenanceDetachedReservation:IDisposable
{
    [ThreadStatic] private static RebirthProvenanceDetachedReservation current;
    private readonly object target;private readonly bool bag;private readonly int slot;
    private static bool installed;
    internal static bool IsDisposableSave()
    {
        try
        {
            if(GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")return false;
            string raw=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(raw))return false;
            string save=Path.GetFullPath(raw).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(!string.Equals(Path.GetFileName(save),"CodexTest",StringComparison.Ordinal))return false;
            for(var directory=new DirectoryInfo(save);directory!=null;directory=directory.Parent)
                if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            return Directory.Exists(save);
        }catch{return false;}
    }
    private RebirthProvenanceDetachedReservation(object inventory,bool isBag,int reserved)
    {
        if(current!=null||inventory==null||reserved<0||!ThreadManager.IsMainThread()||!IsDisposableSave()||!RebirthGameBridge.IsRunning)throw new InvalidOperationException("Detached fixture scope required");
        foreach(var player in GameManager.Instance.World.Entities.list.OfType<EntityPlayer>())
            if(ReferenceEquals(inventory,player.bag)||ReferenceEquals(inventory,player.inventory))throw new InvalidOperationException("Actual player inventory cannot be a fixture reservation target");
        target=inventory;bag=isBag;slot=reserved;current=this;
    }
    internal static IDisposable Enter(object inventory,bool bag,int slot)=>new RebirthProvenanceDetachedReservation(inventory,bag,slot);
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.debug.provenance.detached.reservation");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(ReservedLookup));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(ReservedSlot));installed=true;
    }
    public void Dispose(){if(ReferenceEquals(current,this))current=null;else throw new InvalidOperationException("Fixture reservation scope changed");}
    [HarmonyPatch(typeof(RebirthBackpackLibraryReservation),nameof(RebirthBackpackLibraryReservation.TryGetReservedSlot))]
    private static class ReservedLookup
    {
        private static bool Prefix(object inventory,bool bag,ref int slot,ref bool __result)
        {var held=current;if(held==null||held.bag!=bag||!ReferenceEquals(held.target,inventory))return true;slot=held.slot;__result=true;return false;}
    }
    [HarmonyPatch(typeof(RebirthBackpackLibraryReservation),nameof(RebirthBackpackLibraryReservation.IsReservedInventorySlot))]
    private static class ReservedSlot
    {
        private static bool Prefix(object inventory,bool bag,int slot,ref bool __result)
        {var held=current;if(held==null||held.bag!=bag||!ReferenceEquals(held.target,inventory))return true;__result=slot==held.slot;return false;}
    }
}
#endif
