using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed partial class XUiC_RebirthCamperTurrets:XUiController
{
    private Func<bool> current;private Action close;private Action<RebirthCamperTurretAction,int,int> command;
    private RebirthCamperTurretState state;private bool opened,waiting;private float nextRefresh;
    private int pickerCorner=-1,pickerSlot,pickerPage;
    private int pickerKind=-1; // -1 ammo, -2 silencer, 0..2 scope
    private readonly List<int> choices=new List<int>();
    private readonly RebirthWindowHudScope hud=new RebirthWindowHudScope();
    internal long Revision=>state?.Revision??-1;
    private int selectedCorner;
    private XUiC_RebirthCamperTurretBackpack Backpack=>windowGroup?.Controller?.GetChildByType<XUiC_RebirthCamperTurretBackpack>();
    public override void Init()
    {
        base.Init();
        // Shared clipped lists sleep unless their owning workspace is live.
        // Bind this workspace explicitly so native slot bindings and input run.
        foreach(var list in GetChildrenByType<XUiC_RebirthCharacterOverviewList>())
            list.BindOwner(()=>opened&&windowGroup.isShowing&&cameraCorner<0);
        for(int i=0;i<4;i++)
        {
            int corner=i;
            DragSource("turretSilencer"+i,RebirthCamperTurretAction.TakeSilencerHeld,corner,0);
            for(int s=0;s<3;s++){DragSource("turretScope"+i+"_"+s,RebirthCamperTurretAction.TakeScopeHeld,corner,s);DragSource("turretAmmo"+i+"_"+s,RebirthCamperTurretAction.TakeAmmoHeld,corner,s);}
            GetChildById("turretCorner"+i).OnPress+=(s,b)=>{if(b==0||b==-1){selectedCorner=corner;Paint();}};
            Button("turretInstall"+i,()=>Send(state?.Slots[corner].HasTurret==true?RebirthCamperTurretAction.Remove:Held()?RebirthCamperTurretAction.InstallHeld:RebirthCamperTurretAction.Install,corner,0));
            Button("turretEnabled"+i,()=>Send(RebirthCamperTurretAction.Toggle,corner,0));
            Button("turretCctv"+i,()=>StartCamera(corner));
            Button("turretMode"+i,()=>Send(RebirthCamperTurretAction.ToggleSniper,corner,0));
            Button("turretSilencer"+i,()=>OpenAttachmentPicker(corner,-2));
            for(int s=0;s<3;s++){int scope=s;Button("turretScope"+i+"_"+s,()=>OpenAttachmentPicker(corner,scope));}
            for(int a=0;a<3;a++){int ammo=a;Button("turretAmmoNext"+i+"_"+a,()=>Send(RebirthCamperTurretAction.SelectAmmo,corner,ammo));Button("turretAmmo"+i+"_"+a,()=>{if(state?.Slots[corner].HasTurret!=true)return;if(Held()){Send(RebirthCamperTurretAction.LoadHeld,corner,ammo);return;}if(state.Slots[corner].Ammo[ammo].IsEmpty())return;Send(InputUtils.ShiftKeyPressed?RebirthCamperTurretAction.Unload:RebirthCamperTurretAction.SelectAmmo,corner,ammo);});}
        }
        Button("turretClose",()=>xui.playerUI.windowManager.Close((GUIWindow)windowGroup));
        Button("ammoPickerClose",()=>{pickerCorner=-1;PaintPicker();});
        Button("ammoPickerUnload",()=>{int c=pickerCorner;pickerCorner=-1;PaintPicker();Send(pickerKind==-1?RebirthCamperTurretAction.Unload:pickerKind==-2?RebirthCamperTurretAction.RemoveSilencer:RebirthCamperTurretAction.RemoveScope,c,pickerKind>=0?pickerKind:pickerSlot);});
        Button("ammoPickerPrevious",()=>{pickerPage=Math.Max(0,pickerPage-1);PaintPicker();});
        Button("ammoPickerNext",()=>{if((pickerPage+1)*12<choices.Count)pickerPage++;PaintPicker();});
        for(int i=0;i<12;i++){int cell=i;Button("ammoChoice"+i,()=>{int at=pickerPage*12+cell;if(at>=choices.Count)return;int source=choices[at],c=pickerCorner;pickerCorner=-1;PaintPicker();Send(pickerKind==-1?RebirthCamperTurretAction.Load:pickerKind==-2?RebirthCamperTurretAction.InstallSilencer:RebirthCamperTurretAction.InstallScope,c,pickerKind==-2?source:(source<<2)|(pickerKind>=0?pickerKind:pickerSlot));});}
        Button("turretZoomAuto",()=>{automaticZoom=!automaticZoom;nextZoomFocus=0;});
        windowGroup.Controller.GetChildById("filterTurrets").OnPress+=(s,b)=>{if(!waiting)Backpack?.ToggleFilter(1);};
        windowGroup.Controller.GetChildById("filterAmmo").OnPress+=(s,b)=>{if(!waiting)Backpack?.ToggleFilter(2);};
        ConfigureFilter("filterTurrets",RebirthCamperTurretProfiles.ItemName,"Camper turrets");
        ConfigureFilter("filterAmmo","ammo9mmBulletBall","Compatible turret ammunition");
        foreach(int z in new[]{1,2,4,8}){int zoom=z;Button("turretZoom"+z,()=>{if(cameraCorner<0)return;automaticZoom=false;selectedZoom=RebirthCamperTurretOptics.Clamp(state.Slots[cameraCorner],zoom);});}

    }
    private void Button(string id,Action act){var node=GetChildById(id);if(node==null)throw new InvalidOperationException("Turret UI element missing: "+id);node.OnPress+=(s,b)=>{if((b==-1||b==0)&&!waiting)act();};}
    private void ConfigureFilter(string id,string item,string tooltip)
    {
        if(!(windowGroup.Controller.GetChildById(id)?.ViewComponent is XUiV_Button button))return;
        button.UIAtlas="UIAtlas";button.DefaultSpriteName=id=="filterTurrets"?"ui_game_symbol_turret":"ui_game_symbol_ammo";
        button.HoverSpriteName=button.SelectedSpriteName=button.DefaultSpriteName;button.ToolTip=tooltip;
        button.SelectedSpriteColor=new Color32(181,140,255,255);
    }
    internal void Bind(Func<bool> valid,Action release,Action<RebirthCamperTurretAction,int,int> send){current=valid;close=release;command=send;state=null;waiting=true;}
    public override void OnOpen(){base.OnOpen();ResetHeldDrag();opened=true;pickerCorner=-1;Visible("turretCameraPanel",false);xui.playerUI.windowManager.Open("toolbelt",false);xui.playerUI.windowManager.Open("dragAndDrop",false);ApplyLayout();Paint();Inspect((XUiC_ItemStack)null);hud.Maintain(xui);}
    public override void OnClose(){StopCamera();ResetHeldDrag();opened=false;pickerCorner=-1;hud.Restore();close?.Invoke();base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);if(!opened)return;
        if(current?.Invoke()!=true){xui.playerUI.windowManager.Close((GUIWindow)windowGroup);return;}
        UpdateCamera();
        UpdateHeldDrag();
        if(Time.realtimeSinceStartup>nextRefresh){nextRefresh=Time.realtimeSinceStartup+.5f;ApplyLayout();hud.Maintain(xui);if(!waiting)Send(RebirthCamperTurretAction.Query,0,0);}
    }
    private void ApplyLayout()
    {
        RebirthScreenLayout.GetScreenBounds(xui,out var origin,out var size);
        ViewComponent.Position=origin;ViewComponent.Size=size;ViewComponent.TryUpdatePosition();
        var bg=GetChildById("turretBackground").ViewComponent;bg.Size=size;
        var body=GetChildById("turretBody").ViewComponent;float scale=Math.Min(Math.Max(1,size.x-16)/1856f,Math.Max(1,size.y-78)/813f);
        body.Position=new Vector2i(8,-70);body.TryUpdatePosition();if(body.UiTransform!=null)body.UiTransform.localScale=new Vector3(scale,scale,1);
        var header=GetChildById("turretHeader")?.ViewComponent;
        if(header!=null){header.Position=new Vector2i(8,-8);header.Size=new Vector2i(size.x-16,52);header.TryUpdatePosition();}
        Visible("brand",false);Visible("turretClose",false);HeaderNode("title",12,-8,Math.Max(180,size.x-40),36);
        HeaderNode("turretClose",size.x-120,-6,88,40);HeaderNode("headerRule",0,-51,size.x-16,1);
    }
    private void HeaderNode(string id,int x,int y,int width,int height)
    {var view=GetChildById(id)?.ViewComponent;if(view==null)return;view.Position=new Vector2i(x,y);view.Size=new Vector2i(width,height);view.TryUpdatePosition();}
    internal void Wait(bool value){waiting=value;Status(value?"xuiCamperTurretWaiting":null);}
    internal void Status(string key){Text("turretStatus",string.IsNullOrEmpty(key)?"":Localization.Get(key));}
    internal void Present(RebirthCamperTurretState value,string status){state=value;waiting=false;Status(status);Paint();Backpack?.SetStacks(xui.playerUI.entityPlayer.bag.ItemGrid.items);}
    private void Send(RebirthCamperTurretAction action,int corner,int option){if(!waiting&&current?.Invoke()==true)command?.Invoke(action,corner,option);}
    private void Paint()
    {
        string[] corners={"xuiCamperTurretFrontLeft","xuiCamperTurretFrontRight","xuiCamperTurretRearLeft","xuiCamperTurretRearRight"};
        var item=ItemClass.GetItem(RebirthCamperTurretProfiles.ItemName,false);int installedCount=0;
        for(int i=0;i<4;i++)
        {
            var slot=state?.Slots[i];bool installed=slot?.HasTurret==true;if(installed)installedCount++;
            if(GetChildById("cardBorder"+i)?.ViewComponent is XUiV_Sprite border)border.Color=i==selectedCorner?new Color32(181,140,255,255):new Color32(64,64,72,255);
            Text("turretCorner"+i,Localization.Get(corners[i]));Text("turretName"+i,Localization.Get(installed?RebirthCamperTurretProfiles.ItemName:"xuiCamperTurretEmpty"));
            Visible("turretName"+i,false);Icon("turretIcon"+i,item);Text("turretInstallText"+i,Localization.Get(installed?"xuiCamperTurretToBackpack":"xuiCamperTurretInstall"));
            Color color=installed?new Color(.30f,.94f,.77f):new Color(.71f,.55f,1f);
            Visible("turretAmmoGroup"+i,installed);Visible("turretEmptyGroup"+i,!installed);Visible("turretEnabled"+i,installed);
            Visible("turretCctv"+i,installed);
            Visible("attachmentHeading"+i,installed);
            Visible("turretSilencer"+i,installed);
            Text("turretSilencerText"+i,Localization.Get("xuiCamperTurretSilencer"));
            PaintAttachment("turretSilencer"+i,slot?.Silencer,"modGunSoundSuppressorSilencer");
            for(int s=0;s<3;s++)
            {
                Visible("turretScope"+i+"_"+s,installed);
                Text("turretScopeText"+i+"_"+s,RebirthCamperTurretOptics.Magnifications[s]+"×");
                PaintAttachment("turretScope"+i+"_"+s,slot?.Scopes[s],RebirthCamperTurretOptics.Items[s]);
            }
            Text("turretEnabledText"+i,Localization.Get(slot?.Enabled==true?"xuiCamperTurretActiveShort":"xuiCamperTurretOffShort"));
            if(GetChildById("turretEnabled"+i) is XUiC_RebirthCookingAction power)
                power.StateFillColor=slot?.Enabled==true?new Color32(20,145,55,255):new Color32(180,140,0,255);
            int first=slot?.LoadedAmmo??-1;
            bool rifle=first>=0&&RebirthCamperTurretFiringProfiles.IsRifle(slot.Ammo[first].itemValue);
            Visible("turretMode"+i,installed&&rifle);Visible("slotOrder"+i,false);
            Text("turretModeText"+i,Localization.Get(slot?.SniperMode==true?"xuiCamperTurretSniper":"xuiCamperTurretAssault"));
            if(GetChildById("turretModeIcon"+i)?.ViewComponent is XUiV_Sprite modeIcon)
                modeIcon.SpriteName=slot?.SniperMode==true?"ui_game_symbol_hard_target":"ui_game_symbol_shape_ammo";
            Text("ammoHeading"+i,Localization.Get("xuiCamperTurretAmmunition"));Text("turretRange"+i,RangeText(slot));
            for(int a=0;a<3;a++)
            {
                string suffix=i+"_"+a;var stack=slot?.Ammo[a];bool full=stack!=null&&!stack.IsEmpty();
                Icon("turretAmmoIcon"+suffix,full?stack.itemValue:null);Text("turretAmmoCount"+suffix,full?stack.count.ToString():"+");
                Text("turretAmmoText"+suffix,full?stack.itemValue.ItemClass.GetLocalizedItemName():Localization.Get("xuiCamperTurretEmptySlot"));
                Text("turretAmmoNext"+suffix,full?Localization.Get(first==a?"xuiCamperTurretLoaded":"xuiCamperTurretSelectAmmo"):"");
                if(GetChildById("turretAmmo"+suffix)?.ViewComponent is XUiV_Sprite cell)cell.Color=first==a?new Color(.36f,.20f,.56f):new Color(.188f,.165f,.231f);
            }
        }
        PaintPicker();
    }
    private void PaintPicker()
    {
        Visible("ammoPicker",pickerCorner>=0);if(pickerCorner<0||state==null)return;
        var player=xui.playerUI.entityPlayer;var items=RebirthCamperTurretInventory.Local(player);int bag=player.bag.ItemGrid.items.Length;
        int limit=bag+RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length);choices.Clear();
        var slot=state.Slots[pickerCorner];var dest=pickerKind==-1?slot.Ammo[pickerSlot]:pickerKind==-2?slot.Silencer:slot.Scopes[pickerKind];
        for(int i=0;i<limit;i++)if(!items[i].IsEmpty()&&
            (pickerKind==-1?RebirthCamperTurretProfiles.IsAmmo(items[i].itemValue)&&(dest.IsEmpty()||dest.itemValue.Equals(items[i].itemValue)):
             dest.IsEmpty()&&(pickerKind==-2?RebirthCamperTurretProfiles.IsSilencer(items[i].itemValue):RebirthCamperTurretOptics.ScopeIndex(items[i].itemValue)==pickerKind)))choices.Add(i);
        pickerPage=Math.Min(pickerPage,Math.Max(0,(choices.Count-1)/12));
        Text("ammoPickerTitle",pickerKind==-1?Localization.Get("xuiCamperTurretSelectStack")+"  "+(pickerSlot+1):Localization.Get(pickerKind==-2?"modGunSoundSuppressorSilencer":RebirthCamperTurretOptics.Items[pickerKind]));
        Text("ammoPickerPage",(pickerPage+1)+" / "+Math.Max(1,(choices.Count+11)/12));Visible("ammoPickerUnload",!dest.IsEmpty());
        Text("ammoPickerHint",Localization.Get(choices.Count==0?"xuiCamperTurretNoAmmo":"xuiCamperTurretPickerHint"));
        for(int i=0;i<12;i++)
        {
            int at=pickerPage*12+i;Visible("ammoChoice"+i,at<choices.Count);if(at>=choices.Count)continue;
            int source=choices[at];var stack=items[source];Icon("ammoChoiceIcon"+i,stack.itemValue);
            Text("ammoChoiceName"+i,stack.itemValue.ItemClass.GetLocalizedItemName());Text("ammoChoiceCount"+i,stack.count.ToString());
            Text("ammoChoiceSource"+i,Localization.Get(source<bag?"xuiCamperTurretBackpack":"xuiCamperTurretToolbelt"));
        }
    }
    private void OpenAttachmentPicker(int corner,int kind)
    {if(state?.Slots[corner].HasTurret!=true)return;if(Held()){Send(kind==-2?RebirthCamperTurretAction.SilencerHeld:RebirthCamperTurretAction.ScopeHeld,corner,kind<0?0:kind);return;}pickerCorner=corner;pickerKind=kind;pickerPage=0;PaintPicker();}
    private bool Held()=>xui.DragAndDropWindow?.IsEmpty()==false;
    internal void QuickSource(XUiC_ItemStack source)
    {
        if(waiting||state==null||source==null||source.UserLockedSlot||source.StackLock||source.IsLocked||source.ItemStack.IsEmpty()||xui.DragAndDropWindow?.IsEmpty()!=true)return;
        int corner=selectedCorner;var item=source.ItemStack.itemValue;
        if(item.ItemClass.GetItemName()==RebirthCamperTurretProfiles.ItemName)
        {
            if(state.Slots[corner].HasTurret)corner=Array.FindIndex(state.Slots,s=>!s.HasTurret);
            int from=source.SlotNumber+(source.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt?xui.playerUI.entityPlayer.bag.ItemGrid.items.Length:0);
            if(corner>=0)Send(RebirthCamperTurretAction.InstallFromSlot,corner,from);return;
        }
        if(!state.Slots[corner].HasTurret)corner=Array.FindIndex(state.Slots,s=>s.HasTurret);
        if(corner<0)return;var slot=state.Slots[corner];int index=source.SlotNumber+(source.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt?xui.playerUI.entityPlayer.bag.ItemGrid.items.Length:0);
        if(RebirthCamperTurretProfiles.IsSilencer(item)){if(slot.Silencer.IsEmpty())Send(RebirthCamperTurretAction.InstallSilencer,corner,index);return;}
        int scope=RebirthCamperTurretOptics.ScopeIndex(item);
        if(scope>=0){if(slot.Scopes[scope].IsEmpty())Send(RebirthCamperTurretAction.InstallScope,corner,(index<<2)|scope);return;}
        if(!RebirthCamperTurretProfiles.IsAmmo(item))return;
        // Prefer the selected turret, then wrap through the other installed
        // turrets when its three slots cannot accept this ammunition.
        for(int offset=0;offset<state.Slots.Length;offset++)
        {
            int target=(corner+offset)%state.Slots.Length;
            var candidate=state.Slots[target];if(!candidate.HasTurret)continue;
            int destination=Array.FindIndex(candidate.Ammo,s=>!s.IsEmpty()&&s.itemValue.Equals(item)&&s.count<s.itemValue.ItemClass.Stacknumber.Value);
            if(destination<0)destination=Array.FindIndex(candidate.Ammo,s=>s.IsEmpty());
            if(destination<0)continue;
            Send(RebirthCamperTurretAction.Load,target,(index<<2)|destination);
            return;
        }
    }
    private void PaintAttachment(string id,ItemStack stack,string expectedItem)
    {
        var control=GetChildById(id);
        if(control is XUiC_RebirthCookingAction action)action.PreserveIconColor=true;
        if(control?.GetChildById("actionIcon")?.ViewComponent is XUiV_Sprite icon)
        {
            bool installed=stack!=null&&!stack.IsEmpty();
            var item=installed?stack.itemValue:ItemClass.GetItem(expectedItem,false);
            icon.UIAtlas="ItemIconAtlas";
            icon.SpriteName=item.ItemClass?.GetIconName()??string.Empty;
            icon.Color=installed?Color.white:new Color(1,1,1,.28f);
        }
    }
    private void Icon(string id,ItemValue item){if(GetChildById(id)?.ViewComponent is XUiV_Sprite s){s.UIAtlas="ItemIconAtlas";s.SpriteName=item?.ItemClass?.GetIconName()??"";s.IsVisible=item?.ItemClass!=null;}}
    private void Visible(string id,bool value){var v=GetChildById(id)?.ViewComponent;if(v!=null)v.IsVisible=value;}
    private void Text(string id,string text){if(GetChildById(id)?.ViewComponent is XUiV_Label label)label.Text=text;}
    private static string RangeText(RebirthCamperTurretSlot slot)=>Localization.Get("xuiCamperTurretRange")+": "+
        (slot?.LoadedAmmo>=0?RebirthCamperTurretProfiles.Range(slot).ToString("0.#",System.Globalization.CultureInfo.CurrentCulture)+" m":"—");
}

