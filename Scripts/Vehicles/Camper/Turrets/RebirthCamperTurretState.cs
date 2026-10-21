using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

internal sealed class RebirthCamperTurretSlot
{
    internal ItemStack Installed=ItemStack.Empty.Clone();
    internal ItemStack[] Ammo=ItemStack.CreateArray(3);
    internal ItemStack Silencer=ItemStack.Empty.Clone();
    internal ItemStack[] Scopes=ItemStack.CreateArray(3);
    internal bool Enabled;
    internal bool SniperMode;
    internal int AmmoType;
    internal int SelectedAmmo=-1;
    internal int LoadedAmmo=>SelectedAmmo>=0&&SelectedAmmo<Ammo.Length&&!Ammo[SelectedAmmo].IsEmpty()?SelectedAmmo:Array.FindIndex(Ammo,a=>!a.IsEmpty());
    internal void ResolveAmmo()=>SelectedAmmo=LoadedAmmo;
    internal bool HasTurret=>!Installed.IsEmpty();
}
internal sealed class RebirthCamperTurretState
{
    internal const string Key="RebirthCamperTurretsV1";
    internal long Revision;
    internal readonly RebirthCamperTurretSlot[] Slots=Enumerable.Range(0,4).Select(_=>new RebirthCamperTurretSlot()).ToArray();
    internal byte[] Encode()
    {
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
        {
            w.Write((byte)5);w.Write(Revision);
            foreach(var slot in Slots)
            {w.Write(slot.Enabled);w.Write(slot.AmmoType);var data=RebirthCamperInventorySnapshot.Capture(new[]{slot.Installed}.Concat(slot.Ammo).ToArray());w.Write(data.Length);w.Write(data);}
            foreach(var slot in Slots)w.Write(slot.SelectedAmmo);
            var attachments=RebirthCamperInventorySnapshot.Capture(Slots.Select(slot=>slot.Silencer).ToArray());w.Write(attachments.Length);w.Write(attachments);
            foreach(var slot in Slots)w.Write(slot.SniperMode);
            var scopes=RebirthCamperInventorySnapshot.Capture(Slots.SelectMany(slot=>slot.Scopes).ToArray());w.Write(scopes.Length);w.Write(scopes);
            if(s.Length>131072)throw new InvalidDataException("Turret state exceeds metadata budget.");return s.ToArray();
        }
    }
    internal static RebirthCamperTurretState Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length>131072)throw new InvalidDataException("Invalid turret state size.");
        using(var s=new MemoryStream(bytes,false))using(var r=new BinaryReader(s))
        {
            byte version=r.ReadByte();if(version<1||version>5)throw new InvalidDataException("Unsupported turret state version.");
            var state=new RebirthCamperTurretState {Revision=r.ReadInt64()};if(state.Revision<0)throw new InvalidDataException();
            foreach(var slot in state.Slots)
            {
                slot.Enabled=r.ReadBoolean();slot.AmmoType=r.ReadInt32();int length=r.ReadInt32();
                if(length<0||length>s.Length-s.Position)throw new InvalidDataException();
                var stacks=RebirthCamperInventorySnapshot.Restore(r.ReadBytes(length));if(stacks.Length!=4)throw new InvalidDataException();
                slot.Installed=stacks[0];slot.Ammo=stacks.Skip(1).ToArray();
                if(slot.HasTurret&&(slot.Installed.count!=1||!RebirthCamperTurretProfiles.IsTurret(slot.Installed.itemValue)))throw new InvalidDataException("Unsupported installed turret.");
                foreach(var ammo in slot.Ammo)if(!ammo.IsEmpty()&&(!slot.HasTurret||!RebirthCamperTurretProfiles.Allowed(slot,ammo.itemValue)))throw new InvalidDataException("Incompatible turret ammunition.");
            }
            foreach(var slot in state.Slots){if(version>=2){slot.SelectedAmmo=r.ReadInt32();if(slot.SelectedAmmo< -1||slot.SelectedAmmo>2)throw new InvalidDataException();}slot.ResolveAmmo();}
            if(version>=3)
            {
                int length=r.ReadInt32();if(length<0||length>s.Length-s.Position)throw new InvalidDataException();
                var attachments=RebirthCamperInventorySnapshot.Restore(r.ReadBytes(length));if(attachments.Length!=4)throw new InvalidDataException();
                for(int i=0;i<4;i++)
                {
                    var stack=attachments[i];
                    if(!stack.IsEmpty()&&(!state.Slots[i].HasTurret||stack.count!=1||!RebirthCamperTurretProfiles.IsSilencer(stack.itemValue)))throw new InvalidDataException("Invalid turret silencer.");
                    state.Slots[i].Silencer=stack;
                }
            }
            if(version>=4)foreach(var slot in state.Slots)slot.SniperMode=r.ReadBoolean();
            if(version>=5)
            {
                int length=r.ReadInt32();if(length<0||length>s.Length-s.Position)throw new InvalidDataException();
                var scopes=RebirthCamperInventorySnapshot.Restore(r.ReadBytes(length));if(scopes.Length!=12)throw new InvalidDataException();
                for(int i=0;i<12;i++)
                {
                    var stack=scopes[i];
                    if(!stack.IsEmpty()&&(!state.Slots[i/3].HasTurret||stack.count!=1||RebirthCamperTurretOptics.ScopeIndex(stack.itemValue)!=i%3))throw new InvalidDataException("Invalid turret scope.");
                    state.Slots[i/3].Scopes[i%3]=stack;
                }
            }
            if(s.Position!=s.Length)throw new InvalidDataException();return state;
        }
    }
    internal RebirthCamperTurretState Copy()=>Decode(Encode());
    internal static RebirthCamperTurretState Read(EntityVehicle v)
    {return v.vehicle.GetUpdatedItemValue().TryGetMetadata(Key,out string data)?Decode(Convert.FromBase64String(data)):new RebirthCamperTurretState();}
    internal void Save(EntityVehicle v,bool sync=true)
    {
        if(v.world.IsRemote())throw new InvalidOperationException("Server turret state required.");
        v.vehicle.GetUpdatedItemValue().SetMetadata(Key,Convert.ToBase64String(Encode()));
        if(sync)v.SendSyncData(4);
    }
}
internal static class RebirthCamperTurretProfiles
{
    internal const string ItemName="rebirthCamperTurret";
    internal static bool IsSilencer(ItemValue item)=>item?.ItemClass?.GetItemName()=="modGunSoundSuppressorSilencer";
    internal static readonly string[] Names={"autoTurret","shotgunTurret","m60Turret"};
    // Legacy installations remain readable/removable. Only the custom item can be installed now.
    internal static bool IsTurret(ItemValue item)=>item?.ItemClass!=null&&(item.ItemClass.GetItemName()==ItemName||Names.Contains(item.ItemClass.GetItemName()));
    internal static BlockRanged Block(RebirthCamperTurretSlot slot)=>global::Block.GetBlockValue("autoTurret").Block as BlockRanged;
    internal static bool Allowed(RebirthCamperTurretSlot slot,ItemValue item)=>IsAmmo(item);
    internal static bool IsAmmo(ItemValue item)
    {
        var type=item?.ItemClass;if(type==null)return false;
        if(type.GetItemName()=="ammoJunkTurretShell")return true;
        if(!type.Properties.Values.TryGetValue("DisplayType",out var display))return false;
        if(display=="ammoJunkTurretRegular"||display=="ammoJunkTurretAP")return true;
        if(display=="ammoBullet"||display=="ammoBulletHP"||display=="ammoBulletAP")return true;
        // Other shotgun-display items still require the actual firearm shell tag.
        return (display=="ammoShotgun"||display=="ammoShotgunSlug"||display=="ammoShotgunBreaching")&&type.ItemTags.Test_AnySet(FastTags<TagGroup.Global>.Parse("ammoShells"));
    }
    internal static float Number(Block block,string key,float fallback)=>block.Properties.Values.TryGetValue(key,out string s)&&float.TryParse(s,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var n)?n:fallback;
    internal static float Range(RebirthCamperTurretSlot slot)
    {
        int loaded=slot?.LoadedAmmo??-1;if(loaded<0)return 0;
        // Most base-game bullets have no MaxRange effect. Use their native
        // weapon-family range as the baseline; explicit ammo effects still win.
        return RebirthCamperTurretAmmoEffects.Range(slot.Ammo[loaded].itemValue,slot);
    }
}
internal enum RebirthCamperTurretAction:byte {Query,Install,Remove,Toggle,AmmoType,Load,Unload,SelectAmmo,InstallSilencer,RemoveSilencer,ToggleSniper,InstallScope,RemoveScope,InstallFromSlot,InstallHeld,LoadHeld,SilencerHeld,ScopeHeld,TakeAmmoHeld,TakeSilencerHeld,TakeScopeHeld}
internal static class RebirthCamperTurretTransfer
{
    internal static string Apply(RebirthCamperTurretState state,ItemStack[] bag,RebirthCamperTurretAction action,int corner,int option)
    {
        if(corner<0||corner>=4)return "xuiCamperTurretInvalid";var slot=state.Slots[corner];
        if(action==RebirthCamperTurretAction.Install||action==RebirthCamperTurretAction.InstallFromSlot)
        {
            if(slot.HasTurret)return "xuiCamperTurretInvalid";
            int source=action==RebirthCamperTurretAction.InstallFromSlot?option:Array.FindIndex(bag,s=>!s.IsEmpty()&&s.itemValue.ItemClass.GetItemName()==RebirthCamperTurretProfiles.ItemName);
            if(source<0||source>=bag.Length||bag[source].IsEmpty()||bag[source].itemValue.ItemClass.GetItemName()!=RebirthCamperTurretProfiles.ItemName)return "xuiCamperTurretMissing";
            slot.Installed=bag[source].Clone();slot.Installed.count=1;bag[source].count--;if(bag[source].count==0)bag[source]=ItemStack.Empty.Clone();slot.Enabled=false;slot.AmmoType=0;
        }
        else
        {
            if(!slot.HasTurret)return "xuiCamperTurretMissing";
            if(action==RebirthCamperTurretAction.Toggle)slot.Enabled=!slot.Enabled;
            else if(action==RebirthCamperTurretAction.ToggleSniper)slot.SniperMode=!slot.SniperMode;
            else if(action==RebirthCamperTurretAction.SelectAmmo)
            {if(option<0||option>=3||slot.Ammo[option].IsEmpty())return "xuiCamperTurretNoAmmo";slot.SelectedAmmo=option;}
            else if(action==RebirthCamperTurretAction.AmmoType)
            {return "xuiCamperTurretInvalid";} // Retired wire value; never repurpose old commands.
            else if(action==RebirthCamperTurretAction.Remove)
            {
                foreach(var stack in new[]{slot.Installed,slot.Silencer}.Concat(slot.Ammo).Concat(slot.Scopes))if(!Return(bag,stack))return "xuiCamperTurretBagFull";
                state.Slots[corner]=new RebirthCamperTurretSlot();
            }
            else if(action==RebirthCamperTurretAction.InstallSilencer)
            {
                if(!slot.Silencer.IsEmpty()||option<0||option>=bag.Length||bag[option].IsEmpty()||!RebirthCamperTurretProfiles.IsSilencer(bag[option].itemValue))return "xuiCamperTurretInvalid";
                slot.Silencer=bag[option].Clone();slot.Silencer.count=1;bag[option].count--;if(bag[option].count==0)bag[option]=ItemStack.Empty.Clone();
            }
            else if(action==RebirthCamperTurretAction.RemoveSilencer)
            {
                if(slot.Silencer.IsEmpty())return "xuiCamperTurretInvalid";
                if(!Return(bag,slot.Silencer))return "xuiCamperTurretBagFull";slot.Silencer=ItemStack.Empty.Clone();
            }
            else if(action==RebirthCamperTurretAction.InstallScope)
            {
                int destination=option&3,source=option>>2;
                if(option<0||destination>=3||source>=bag.Length||!slot.Scopes[destination].IsEmpty()||bag[source].IsEmpty()||RebirthCamperTurretOptics.ScopeIndex(bag[source].itemValue)!=destination)return "xuiCamperTurretInvalid";
                slot.Scopes[destination]=bag[source].Clone();slot.Scopes[destination].count=1;bag[source].count--;if(bag[source].count==0)bag[source]=ItemStack.Empty.Clone();
            }
            else if(action==RebirthCamperTurretAction.RemoveScope)
            {
                if(option<0||option>=3||slot.Scopes[option].IsEmpty())return "xuiCamperTurretInvalid";
                if(!Return(bag,slot.Scopes[option]))return "xuiCamperTurretBagFull";slot.Scopes[option]=ItemStack.Empty.Clone();
            }
            else if(action==RebirthCamperTurretAction.Load)
            {
                int destination=option&3,source=option>>2;
                if(option<0||destination>=3||source>=bag.Length)return "xuiCamperTurretInvalid";
                var dest=slot.Ammo[destination];
                if(bag[source].IsEmpty()||!RebirthCamperTurretProfiles.IsAmmo(bag[source].itemValue))return "xuiCamperTurretNoAmmo";
                if(!dest.IsEmpty()&&!dest.itemValue.Equals(bag[source].itemValue))return "xuiCamperTurretUnloadFirst";
                int cap=bag[source].itemValue.ItemClass.Stacknumber.Value,amount=Math.Min(cap-dest.count,bag[source].count);
                if(amount<=0)return "xuiCamperTurretAmmoFull";
                if(dest.IsEmpty()){dest=bag[source].Clone();dest.count=0;slot.Ammo[destination]=dest;}
                dest.count+=amount;bag[source].count-=amount;if(bag[source].count==0)bag[source]=ItemStack.Empty.Clone();
            }
            else if(action==RebirthCamperTurretAction.Unload)
            {if(option<0||option>=3||!Return(bag,slot.Ammo[option]))return "xuiCamperTurretBagFull";slot.Ammo[option]=ItemStack.Empty.Clone();}
            else return "xuiCamperTurretInvalid";
        }
        state.Slots[corner].ResolveAmmo();state.Revision++;return null;
    }
    private static bool Return(ItemStack[] bag,ItemStack source)
    {
        if(source.IsEmpty())return true;int left=source.count;
        for(int i=0;i<bag.Length&&left>0;i++)
        {
            if(bag[i].IsEmpty())continue;int amount=left;
            if(bag[i].itemValue.Equals(source.itemValue)&&bag[i].CanStackPartly(ref amount)){bag[i].count+=amount;left-=amount;}
        }
        for(int i=0;i<bag.Length&&left>0;i++)if(bag[i].IsEmpty()){int amount=Math.Min(left,source.itemValue.ItemClass.Stacknumber.Value);bag[i]=source.Clone();bag[i].count=amount;left-=amount;}
        return left==0;
    }
}
