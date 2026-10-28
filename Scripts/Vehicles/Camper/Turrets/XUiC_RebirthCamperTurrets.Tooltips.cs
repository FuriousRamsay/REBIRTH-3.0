using System;

// Read-only adapters into the same popup used by Creative. Never synthesize a
// slot or copy an installed item into the player's bag/cursor for presentation.
public sealed partial class XUiC_RebirthCamperTurrets
{
    internal void BindItemTooltips()
    {
        for (int i=0;i<4;i++)
        {
            int corner=i;
            BindItemTooltip("turretIcon"+i,()=>TooltipSlot(corner)?.Installed);
            BindItemTooltip("turretInstall"+i,()=>TooltipSlot(corner)?.Installed);
            BindItemTooltip("turretSilencer"+i,()=>TooltipSlot(corner)?.Silencer);
            for(int n=0;n<3;n++)
            {
                int index=n;
                BindItemTooltip("turretScope"+i+"_"+n,()=>TooltipSlot(corner)?.Scopes[index]);
                BindItemTooltip("turretAmmo"+i+"_"+n,()=>TooltipSlot(corner)?.Ammo[index]);
            }
        }
    }
    private RebirthCamperTurretSlot TooltipSlot(int corner)
        => opened && cameraCorner<0 && windowGroup?.isShowing==true
            && state!=null && corner>=0 && corner<state.Slots.Length ? state.Slots[corner] : null;
    private void BindItemTooltip(string id,Func<ItemStack> read)
        => RebirthCharacterItemStatsTooltip.BindStackHover(GetChildById(id),read);
}
