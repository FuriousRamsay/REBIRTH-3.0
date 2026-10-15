using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Read the existing native radial only. Never open it, rebuild commands,
// change selection, invoke admission callbacks or write icon presentation.
public static class RebirthGameBridgeRadialObservation
{
    public static void Handle(BridgeRequest req)
    {
        if(req.Method!="GET"){req.Fail("Radial observation is GET only.",405);return;}
        var world=GameManager.Instance?.World;var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||player?.PlayerUI?.xui==null)
        {req.Fail("Current local native player UI required.",409);return;}
        var radial=player.PlayerUI.xui.RadialWindow;
        if(radial==null){req.Complete(new JObject{["schema"]=1,["observationOnly"]=true,["available"]=false,["entries"]=new JArray()});return;}
        var context=radial.context;var entityContext=context as XUiC_Radial.RadialContextEntity;
        var blockContext=context as XUiC_Radial.RadialContextBlock;
        var focused=entityContext?.EntityFocused;
        var entries=new JArray();string selectedId=null;
        int count=radial.menuItem==null?0:Math.Min(64,radial.menuItem.Length);
        for(int i=0;i<count;i++)
        {
            var entry=radial.menuItem[i];
            if(entry==null||radial.menuItemState==null||i>=radial.menuItemState.Length||!radial.menuItemState[i])continue;
            string commandId=Command(context,entry.CommandIndex);
            var icon=entry.GetChildById("icon")?.ViewComponent as XUiV_Sprite;
            var native=icon?.UiTransform?.GetComponent<UISprite>();
            bool selected=i==radial.selectedIndex;if(selected)selectedId=commandId;
            entries.Add(new JObject{["menuIndex"]=i,["nativeMenuItemIndex"]=entry.MenuItemIndex,["commandIndex"]=entry.CommandIndex,
                ["commandId"]=commandId,["selectionText"]=entry.SelectionText,["selected"]=selected,
                ["uiHovered"]=entry.ViewComponent?.UiTransformIsHovered==true,["visible"]=entry.ViewComponent?.IsVisible==true,
                ["activeInHierarchy"]=entry.ViewComponent?.UiTransform?.gameObject.activeInHierarchy==true,
                ["iconSprite"]=icon?.SpriteName,["nativeIconSprite"]=native?.spriteName,
                ["iconColor"]=icon==null?null:ColorValue(icon.Color),["nativeIconColor"]=native==null?null:ColorValue(native.color)});
        }
        req.Complete(new JObject{["schema"]=1,["observationOnly"]=true,["available"]=true,
            ["windowShowing"]=radial.WindowGroup?.isShowing==true,["radialVisible"]=radial.ViewComponent?.IsVisible==true,
            ["activeInHierarchy"]=radial.ViewComponent?.UiTransform?.gameObject.activeInHierarchy==true,
            ["openRequested"]=radial.isOpenRequested,["contextType"]=context?.GetType().FullName,
            ["focusedEntityId"]=focused==null?JValue.CreateNull():new JValue(focused.entityId),
            ["focusedEntityClass"]=focused==null?null:focused.EntityClass?.entityClassName??focused.GetType().Name,
            ["focusedEntityCurrent"]=focused!=null&&ReferenceEquals(world.GetEntity(focused.entityId),focused),
            ["isCamperContext"]=RebirthCamperRadial.IsCamper(focused as EntityVehicle),
            ["focusedBlock"]=blockContext==null?null:new JObject{["x"]=blockContext.BlockPos.x,["y"]=blockContext.BlockPos.y,["z"]=blockContext.BlockPos.z,
                ["type"]=blockContext.BlockValue.type,["name"]=blockContext.BlockValue.Block?.GetBlockName()},
            ["selectedIndex"]=radial.selectedIndex,["selectedCommandId"]=selectedId,["entries"]=entries,
            ["truncated"]=radial.menuItem!=null&&radial.menuItem.Length>64,
            ["qualificationScope"]="Native current radial context and already-filtered active entries; hidden/stale context alone does not prove an open camper menu."});
    }
    private static string Command(XUiC_Radial.RadialContextAbs context,int index)
    {
        if(index<0)return null;
        if(context is XUiC_Radial.RadialContextEntity entity)
            return entity.Commands!=null&&index<entity.Commands.Length?entity.Commands[index].commandId:null;
        if(context is XUiC_Radial.RadialContextBlock block)
        {
            int nativeCount=block.Commands?.Length??0;
            if(index<nativeCount)return block.Commands[index].text;
            int custom=index-nativeCount;return block.CustomCommands!=null&&custom<block.CustomCommands.Length?block.CustomCommands[custom].text:null;
        }
        // Ordinary quick menus have other/null contexts. Do not invent an entity
        // command identity from their label or their numeric menu index.
        return null;
    }
    private static JObject ColorValue(Color color)
    {
        Color32 rgb=color;
        return new JObject{["r"]=color.r,["g"]=color.g,["b"]=color.b,["a"]=color.a,
            ["rgba255"]=new JArray(rgb.r,rgb.g,rgb.b,rgb.a)};
    }
}
