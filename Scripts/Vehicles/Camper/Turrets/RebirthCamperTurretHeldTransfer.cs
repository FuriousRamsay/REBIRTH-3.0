using System;
using System.Linq;

// A held stack participates in the same acknowledged actor image as bag/belt.
// It remains in native cursor custody until the authoritative reply is applied.
internal static class RebirthCamperTurretHeldTransfer
{
    internal static bool IsHeld(RebirthCamperTurretAction action)=>action==RebirthCamperTurretAction.InstallHeld||
        action==RebirthCamperTurretAction.LoadHeld||action==RebirthCamperTurretAction.SilencerHeld||action==RebirthCamperTurretAction.ScopeHeld||IsTake(action);
    internal static bool IsTake(RebirthCamperTurretAction action)=>action==RebirthCamperTurretAction.TakeAmmoHeld||action==RebirthCamperTurretAction.TakeSilencerHeld||action==RebirthCamperTurretAction.TakeScopeHeld;
    internal static ItemStack[] Capture(EntityPlayerLocal player,RebirthCamperTurretAction action)
    {
        var items=RebirthCamperTurretInventory.Local(player);
        if(!IsHeld(action))return items;
        var cursor=player.PlayerUI.xui.DragAndDropWindow.CurrentStack;
        var native=player.DragAndDropItem;
        // Native 3.4 permits a null player cursor until its first stack change.
        // The UI grid already has an empty stack then. Match native empty
        // semantics without weakening exact custody for any nonempty cursor.
        bool bothEmpty=cursor!=null&&cursor.IsEmpty()&&(native==null||native.IsEmpty());
        if(cursor==null||!bothEmpty&&!RebirthCamperEnergyItems.Same(cursor,native))throw new InvalidOperationException("Native turret cursor changed.");
        return items.Concat(new[]{cursor.Clone()}).ToArray();
    }
    internal static string Apply(RebirthCamperTurretState state,ItemStack[] actor,RebirthCamperTurretAction action,int corner,int option)
    {
        if(!IsHeld(action)||actor==null||actor.Length<1)return "xuiCamperTurretInvalid";
        int source=actor.Length-1;
        if(IsTake(action))
        {
            if(!actor[source].IsEmpty()||corner<0||corner>3||!state.Slots[corner].HasTurret)return "xuiCamperTurretInvalid";
            var slot=state.Slots[corner];
            if(action!=RebirthCamperTurretAction.TakeSilencerHeld&&(option<0||option>2))return "xuiCamperTurretInvalid";
            var item=action==RebirthCamperTurretAction.TakeAmmoHeld?slot.Ammo[option]:action==RebirthCamperTurretAction.TakeSilencerHeld?slot.Silencer:slot.Scopes[option];
            if(item.IsEmpty())return "xuiCamperTurretInvalid";
            actor[source]=item.Clone();
            if(action==RebirthCamperTurretAction.TakeAmmoHeld){slot.Ammo[option]=ItemStack.Empty.Clone();slot.ResolveAmmo();}
            else if(action==RebirthCamperTurretAction.TakeSilencerHeld)slot.Silencer=ItemStack.Empty.Clone();
            else slot.Scopes[option]=ItemStack.Empty.Clone();
            return null;
        }
        if(actor[source].IsEmpty())return "xuiCamperTurretInvalid";
        var translated=action==RebirthCamperTurretAction.InstallHeld?RebirthCamperTurretAction.InstallFromSlot:
            action==RebirthCamperTurretAction.LoadHeld?RebirthCamperTurretAction.Load:
            action==RebirthCamperTurretAction.SilencerHeld?RebirthCamperTurretAction.InstallSilencer:RebirthCamperTurretAction.InstallScope;
        if((action==RebirthCamperTurretAction.LoadHeld||action==RebirthCamperTurretAction.ScopeHeld)&&(option<0||option>=3))return "xuiCamperTurretInvalid";
        int argument=action==RebirthCamperTurretAction.LoadHeld||action==RebirthCamperTurretAction.ScopeHeld?(source<<2)|option:source;
        return RebirthCamperTurretTransfer.Apply(state,actor,translated,corner,argument);
    }
}
