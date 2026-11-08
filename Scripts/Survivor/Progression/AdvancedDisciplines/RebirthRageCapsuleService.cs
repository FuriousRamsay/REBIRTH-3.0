using System;
#nullable disable
/// <summary>Server side of a capsule use or rage key press: the 2.6 rage rules live in RebirthBerserkerRage.</summary>
public static class RebirthRageCapsuleService
{
    public static bool Consume(EntityPlayer p,int type,ushort seed,out string message)
    {
        message=string.Empty;
        if(p==null||!RebirthWorldCharacterRepository.IsServerAuthority){message="Not authoritative.";return false;}
        ItemClass ic=ItemClass.GetForId(type);
        var kind=ic!=null&&string.Equals(ic.Name,RebirthBerserkerRage.CapsuleOtherItem,StringComparison.OrdinalIgnoreCase)?RebirthBerserkerRage.Kind.Other:RebirthBerserkerRage.Kind.Offensive;
        return RebirthBerserkerRage.ServerStart(p,kind,type,seed,out message);
    }

    /// <summary>Page Down / Page Up from a remote owner: slot is rage.defensive, rage.offensive or rage.other.</summary>
    public static bool Start(EntityPlayer p,string slot,out string message)
    {
        message=string.Empty;
        if(p==null||!RebirthWorldCharacterRepository.IsServerAuthority){message="Not authoritative.";return false;}
        return RebirthBerserkerRage.ServerStart(p,RebirthBerserkerRage.KindFromSlot(slot),0,0,out message);
    }
}
