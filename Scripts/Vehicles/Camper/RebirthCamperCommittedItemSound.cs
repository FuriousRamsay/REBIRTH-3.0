using System;
using System.Linq;

// Presentation only, invoked after authoritative item publication. The native
// item-stack controller owns item-specific SoundPlace and default place_click.
internal static class RebirthCamperCommittedItemSound
{
    internal static void Play(EntityPlayerLocal player,ItemStack[] before,ItemStack[] after,string window)
    {
        if(!ThreadManager.IsMainThread()||player==null||before==null||after==null||before.Length!=after.Length
            ||!ReferenceEquals(player.world,GameManager.Instance?.World))return;
        try
        {
            var moved=before.Where((stack,i)=>stack!=null&&!stack.IsEmpty()&&
                (after[i]==null||after[i].IsEmpty()||!stack.itemValue.Equals(after[i].itemValue)||after[i].count<stack.count)).FirstOrDefault();
            if(moved==null)return; // Query, toggle, refresh and empty no-op have no item placement.
            if(player.PlayerUI.windowManager.TryGetWindow(window,out GUIWindow native)&&native is XUiWindowGroup group)
            {
                var slot=group.Controller.GetChildByType<XUiC_ItemStack>();
                if(slot!=null){slot.PlayPlaceSound(moved);return;}
            }
            // A completed operation may retire just after its window closed.
            string sound=moved.itemValue.ItemClass?.SoundPlace;
            if(!string.IsNullOrEmpty(sound))Audio.Manager.PlayInsidePlayerHead(sound);
        }
        catch(Exception error){Log.Warning("[CamperItemSound] Committed item sound unavailable: "+error.Message);}
    }
}
