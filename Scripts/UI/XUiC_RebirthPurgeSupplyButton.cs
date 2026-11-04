using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthPurgeSupplyButton:XUiController
{
    private static XUiC_RebirthPurgeSupplyButton receiver;
    private XUiC_RebirthMapArea map;
    private XUiController button;
    internal const int ControlWidth=300,ControlHeight=48,Inset=16,TooltipWidth=400,TooltipHeight=78;
    private XUiV_Sprite icon,border;
    private XUiV_Label counter;
    private XUiController tooltip;
    private UISprite selectedRing;
    private int releasedFrame=-2;
    internal bool BlocksMapInput=>Time.frameCount<=releasedFrame+1||ContainsPointer();
    private bool ContainsPointer()
    {
        if(map==null||!map.isOpen||ViewComponent?.IsVisible!=true)return false;
        var camera=map.xui?.playerUI?.camera;var transform=ViewComponent.UiTransform;
        if(camera==null||transform==null)return false;
        var ray=camera.ScreenPointToRay(map.xui.playerUI.CursorController.GetScreenPosition());
        if(!new Plane(transform.forward,transform.position).Raycast(ray,out float distance))return false;
        var point=transform.InverseTransformPoint(ray.GetPoint(distance));
        return point.x>=0&&point.x<=ControlWidth&&point.y<=0&&point.y>=-ControlHeight;
    }
    internal static string CounterText(IList<RebirthPurgeSupplyTarget> values,int selected)
    {
        for(int i=0;i<values.Count;i++)if(values[i].Id==selected)return (i+1)+" / "+values.Count;
        return "— / "+values.Count;
    }
    private World scope;
    private string owner,saveName,gameWorld;
    private Guid nativeId,request;
    private float next,requestedAt,validUntil;
    private bool advance;
    private int previous;
    private List<RebirthPurgeSupplyTarget> targets=new List<RebirthPurgeSupplyTarget>();
    public override void Init(){base.Init();button=GetChildById("supplyCycleButton");icon=GetChildById("supplyCycleIcon")?.ViewComponent as XUiV_Sprite;counter=GetChildById("supplyCycleCounter")?.ViewComponent as XUiV_Label;border=GetChildById("supplyCycleBorder")?.ViewComponent as XUiV_Sprite;tooltip=GetChildById("supplyCycleTooltip");if(button!=null)button.OnPress+=Pressed;}
    public override void OnOpen(){base.OnOpen();Clear();receiver=this;map=GetParentByType<XUiC_RebirthMapArea>();}
    public override void OnClose(){if(ReferenceEquals(receiver,this))receiver=null;Clear();map=null;base.OnClose();}
    private void Clear(){if(selectedRing!=null)UnityEngine.Object.Destroy(selectedRing.gameObject);selectedRing=null;if(ViewComponent!=null)ViewComponent.IsVisible=false;scope=null;owner=saveName=gameWorld=null;request=Guid.Empty;nativeId=Guid.Empty;targets.Clear();previous=0;next=validUntil=0;advance=false;releasedFrame=-2;if(tooltip?.ViewComponent!=null)tooltip.ViewComponent.IsVisible=false;if(counter!=null)counter.Text="";}
    private bool Context()
    {
        if(!ThreadManager.IsMainThread())return false;
        var world=GameManager.Instance?.World;RebirthStablePlayerIdentity identity;Guid id;
        if(map==null||!map.isOpen||!map.bMapInitialized||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||world==null||!Guid.TryParseExact(world.worldState?.Guid,"N",out id)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity)){Clear();return false;}
        string save=GamePrefs.GetString(EnumGamePrefs.GameName),name=GamePrefs.GetString(EnumGamePrefs.GameWorld);
        if(!ReferenceEquals(scope,world)||owner!=identity.StorageKey||nativeId!=id||saveName!=save||gameWorld!=name){Clear();scope=world;owner=identity.StorageKey;nativeId=id;saveName=save;gameWorld=name;}
        return true;
    }
    public override void Update(float dt)
    {
        base.Update(dt);if(!Context()){if(ViewComponent!=null)ViewComponent.IsVisible=false;return;}
        bool laidOut=Layout();
        if(request!=Guid.Empty&&Time.realtimeSinceStartup-requestedAt>3f){request=Guid.Empty;advance=false;targets.Clear();}
        if(Time.realtimeSinceStartup>=next&&request==Guid.Empty){next=Time.realtimeSinceStartup+1f;Refresh(false);}
        bool hasDrops=targets.Count>0&&Time.realtimeSinceStartup<=validUntil;
        if(!hasDrops&&selectedRing!=null)selectedRing.gameObject.SetActive(false);
        if(ViewComponent!=null)ViewComponent.IsVisible=laidOut&&hasDrops;
        bool enabled=hasDrops&&request==Guid.Empty;
        if(button?.ViewComponent!=null)button.ViewComponent.Enabled=enabled;
        bool hovered=laidOut&&hasDrops&&ContainsPointer();
        if(icon!=null)icon.Color=Color.white;
        if(border!=null)border.Color=hovered?new Color32(219,175,250,255):new Color32(190,149,225,255);
        if(tooltip?.ViewComponent!=null)tooltip.ViewComponent.IsVisible=hovered;
        if(counter!=null){string text=CounterText(targets,previous);if(counter.Text!=text)counter.Text=text;}
    }
    internal void RefreshMapMarkers()
    {
        if(map==null||!map.isOpen)return;
        PaintMapMarkers(ReferenceEquals(scope,GameManager.Instance?.World)&&targets.Count>0&&Time.realtimeSinceStartup<=validUntil);
    }
    private void PaintMapMarkers(bool hasDrops)
    {
        if(selectedRing!=null)selectedRing.gameObject.SetActive(false);
        if(!hasDrops||!NavObjectManager.HasInstance)return;
        // Decorate only the freshly authenticated owned targets in this map.
        // Native nav ownership, other players' crates and compass colors remain native.
        foreach(var target in targets)
        {
            var nav=NavObjectManager.Instance.GetNavObjectByEntityID(target.Id);
            GameObject view;
            if(nav==null||!map.keyToNavSprite.TryGetValue(nav.Key,out view)||view==null)continue;
            var sprite=view.transform.Find("Sprite")?.GetComponent<UISprite>();
            if(sprite==null)continue;
            if(sprite.spriteName!="rb_supply_drop_outline")
            {sprite.atlas=map.xui.GetAtlasByName("UIAtlas","rb_supply_drop_outline");sprite.spriteName="rb_supply_drop_outline";}
            sprite.color=Color.white;sprite.width=28;sprite.height=28;
            var name=view.transform.Find("Name")?.GetComponent<UILabel>();
            if(name!=null)name.color=Color.white;
            if(target.Id!=previous)continue;
            if(selectedRing==null)
            {
                var ring=new GameObject("rebirthSupplyDropSelection",typeof(UISprite));
                selectedRing=ring.GetComponent<UISprite>();
                selectedRing.atlas=map.xui.GetAtlasByName("UIAtlas","rb_supply_drop_ring");
                selectedRing.spriteName="rb_supply_drop_ring";selectedRing.width=44;selectedRing.height=44;
                selectedRing.color=new Color32(190,149,225,255);
            }
            selectedRing.transform.SetParent(view.transform,false);selectedRing.gameObject.layer=sprite.gameObject.layer;
            selectedRing.transform.localPosition=sprite.transform.localPosition;
            selectedRing.transform.localScale=Vector3.one;selectedRing.depth=sprite.depth-1;
            selectedRing.gameObject.SetActive(true);
        }
    }
    private bool Layout()
    {
        var view=map.GetChildById("mapView")?.ViewComponent;var parent=Parent?.ViewComponent?.UiTransform;
        if(view?.UiTransform==null||parent==null||ViewComponent==null)return false;
        var point=parent.InverseTransformPoint(view.UiTransform.TransformPoint(new Vector3(view.Size.x,0,0)));
        // Both button and left-opening tooltip share the map's right inset.
        // Derive from the live map edge rather than a fixed window coordinate.
        if(view.Size.x<TooltipWidth+Inset*2||view.Size.y<ControlHeight+TooltipHeight+Inset*2+10)return false;
        ViewComponent.Position=new Vector2i(Mathf.RoundToInt(point.x)-ControlWidth-Inset,Mathf.RoundToInt(point.y)-Inset);return true;
    }
    private void Pressed(XUiController sender,int mouse)
    {if((mouse==0||mouse==-1)&&Context()&&targets.Count>0&&Time.realtimeSinceStartup<=validUntil&&request==Guid.Empty){releasedFrame=Time.frameCount;if(tooltip?.ViewComponent!=null)tooltip.ViewComponent.IsVisible=false;Refresh(true);}}
    private void Refresh(bool cycle)
    {
        request=Guid.NewGuid();requestedAt=Time.realtimeSinceStartup;advance=cycle;
        if(!scope.IsRemote())
        {List<RebirthPurgeSupplyTarget> fresh;bool ready=RebirthPurgeSupplyTargets.Collect(scope,owner,out fresh);Accept(scope,nativeId,request,ready,fresh);}
        else ConnectionManager.Instance?.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPurgeSupplyTargetsRequest>().Setup(nativeId,request));
    }
    internal static void Receive(World world,Guid id,Guid nonce,bool complete,List<RebirthPurgeSupplyTarget> values)
    {receiver?.Accept(world,id,nonce,complete,values);}
    private void Accept(World world,Guid id,Guid nonce,bool complete,List<RebirthPurgeSupplyTarget> values)
    {
        if(!Context()||!ReferenceEquals(scope,world)||nativeId!=id||request==Guid.Empty||request!=nonce||Time.realtimeSinceStartup-requestedAt>3f)return;
        request=Guid.Empty;targets=complete?values:new List<RebirthPurgeSupplyTarget>();validUntil=complete?Time.realtimeSinceStartup+2f:0;
        bool focus=advance;advance=false;if(!focus)return;
        // Click response is freshly authenticated server state, never a retained
        // NavObject pointer or an old owner/name from the client's compass.
        int index=RebirthPurgeSupplyTarget.Next(targets,previous);if(index<0)return;
        var target=targets[index];previous=target.Id;map.closeAllPopups();map.ClearMapHover();map.Interaction.Pan();
        // A whole-world fitted view cannot visibly center on an individual crate.
        // Keep closer zooms; cap distant views when explicitly focusing a drop.
        map.zoomScale=Math.Min(map.zoomScale,2f);map.targetZoomScale=map.zoomScale;
        map.PositionMapAt(target.Position);map.FitPlayableView();
    }
}
