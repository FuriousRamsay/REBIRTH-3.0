using System;
using System.Collections.Generic;

internal static class RebirthCamperTurretOptics
{
    internal static readonly int[] Magnifications={2,4,8};
    internal static readonly string[] Items={"modGunScopeSmall","modGunScopeMedium","modGunScopeLarge"};
    internal static int ScopeIndex(ItemValue value)=>value?.ItemClass==null?-1:Array.IndexOf(Items,value.ItemClass.GetItemName());
    internal static int Cap(RebirthCamperTurretSlot slot)
    {
        int loaded=slot?.LoadedAmmo??-1;
        return loaded<0?1:(int)RebirthCamperTurretFiringProfiles.Zoom(slot.Ammo[loaded].itemValue,slot.SniperMode);
    }
    internal static List<int> Available(RebirthCamperTurretSlot slot)
    {
        var result=new List<int>{1};int cap=Cap(slot);
        for(int i=0;i<3;i++)if(Magnifications[i]<=cap&&slot?.Scopes[i]?.IsEmpty()==false)result.Add(Magnifications[i]);
        return result;
    }
    internal static int Clamp(RebirthCamperTurretSlot slot,int requested)
    {int result=1;foreach(int zoom in Available(slot))if(zoom<=requested)result=zoom;return result;}
    internal static int Step(RebirthCamperTurretSlot slot,int current,int direction)
    {
        var choices=Available(slot);int index=choices.IndexOf(Clamp(slot,current));
        return choices[Math.Max(0,Math.Min(choices.Count-1,index+Math.Sign(direction)))];
    }
    // Keep roughly the same apparent target size. A dead band prevents scope
    // changes chattering while a target moves around a distance boundary.
    internal static int Auto(RebirthCamperTurretSlot slot,float distance,int current)
    {
        if(distance<0)return 1;
        float range=RebirthCamperTurretProfiles.Range(slot);
        float two=Math.Min(20,range*.55f),four=Math.Min(45,range*.75f),eight=Math.Min(85,range*.9f);
        int requested=distance<two?1:distance<four?2:distance<eight?4:8;
        int candidate=Clamp(slot,requested);current=Clamp(slot,current);
        float boundary=candidate>current?(candidate==2?two:candidate==4?four:eight):(current==2?two:current==4?four:eight);
        if(candidate>current&&distance<boundary*1.08f)return current;
        if(candidate<current&&distance>boundary*.92f)return current;
        return candidate;
    }
}
