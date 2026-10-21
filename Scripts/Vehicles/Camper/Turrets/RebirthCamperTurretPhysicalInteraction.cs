using System;
using HarmonyLib;
using UnityEngine;

internal sealed class RebirthCamperTurretPhysicalTarget:MonoBehaviour
{
    internal EntityVehicle Vehicle;
    internal int Corner;
    internal static void Attach(EntityVehicle vehicle,int corner,Transform pitch)
    {
        if(!pitch||pitch.GetComponentInChildren<RebirthCamperTurretPhysicalTarget>(true))return;
        var renderers=pitch.GetComponentsInChildren<Renderer>(true);
        if(renderers.Length==0)return;
        var bounds=new Bounds(pitch.InverseTransformPoint(renderers[0].bounds.center),Vector3.zero);
        foreach(var renderer in renderers)
        {
            var b=renderer.bounds;
            for(int i=0;i<8;i++)bounds.Encapsulate(pitch.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        }
        var go=new GameObject("Camper turret CCTV interaction");go.transform.SetParent(pitch,false);
        var target=go.AddComponent<RebirthCamperTurretPhysicalTarget>();target.Vehicle=vehicle;target.Corner=corner;
        var collider=go.AddComponent<BoxCollider>();collider.isTrigger=true;collider.center=bounds.center;collider.size=bounds.size+Vector3.one*.025f;
        RebirthCamperWireTarget.Attach(go,vehicle,1,corner);
    }
    internal static bool Focus(EntityPlayerLocal player,out RebirthCamperTurretPhysicalTarget target)
    {
        target=null;
        if(player==null||player.IsDead()||player.AttachedToEntity!=null||player.PlayerUI?.windowManager?.IsInputActive()!=false)return false;
        var ray=player.GetLookRay();ray.origin-=Origin.position;
        var hits=Physics.RaycastAll(ray,Constants.cCollectItemDistance,Physics.AllLayers,QueryTriggerInteraction.Collide);
        Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            var c=hit.collider;if(!c||c.transform.IsChildOf(player.RootTransform))continue;
            var candidate=c.GetComponent<RebirthCamperTurretPhysicalTarget>();
            if(candidate&&RebirthCamperTurretAccess.CanUse(candidate.Vehicle,player)
                &&RebirthCamperTurretRuntime.For(candidate.Vehicle).Observe().Slots[candidate.Corner].HasTurret)
            {target=candidate;return true;}
            if(!c.isTrigger)return false;
        }
        return false;
    }
}
[HarmonyPatch(typeof(PlayerMoveController),"HandleInteraction")]
internal static class RebirthCamperTurretPhysicalInteraction
{
    private static void Prefix(PlayerMoveController __instance,ref bool __7,out string __state)
    {
        __state=null;var player=__instance.entityPlayerLocal;
        if(RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||!RebirthCamperTurretPhysicalTarget.Focus(player,out var target))return;
        __state=string.Format(Localization.Get("xuiCamperTurretPressInteract"),player.playerInput.PermanentActions.Activate.GetBindingXuiMarkupString(),Localization.Get("xuiCamperTurretCctv"));
        if(__7){__7=false;RebirthCamperTurretAccess.OpenCamera(target.Vehicle,player,target.Corner);}
    }
    private static void Postfix(PlayerMoveController __instance,string __state)
    {if(__state!=null){XUiC_InteractionPrompt.SetText(__instance.playerUI,__state);__instance.strTextLabelPointingTo=__state;}}
}
