using System.Collections.Generic;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthMusicLibrary : XUiController
{
    private sealed class PressBinding { internal XUiController Control; internal XUiEvent_OnPressEventHandler Handler; }
    private readonly List<PressBinding> pressBindings=new List<PressBinding>();
    private struct AvailableCassette
    {
        public int Type;
        public ushort Seed;
        public string Name;
        public ItemValue Value;
    }
    // Retain detached native metadata for the rendered cassette. Selection submits
    // its fingerprint, so a same-type/seed replacement cannot become the source.
    private readonly List<AvailableCassette> available=new List<AvailableCassette>();
    private struct Presentation { public ItemClass Item; public string Title; }
    // Valid only within one Render. Resolve shared icon/title data once without
    // allocating ItemValue, and never retain definition/language assumptions across refreshes.
    private readonly Dictionary<string,Presentation> presentation=new Dictionary<string,Presentation>(System.StringComparer.Ordinal);
    private Presentation ResolvePresentation(string id)
    {
        if(string.IsNullOrEmpty(id))return default(Presentation);
        Presentation value;
        if(presentation.TryGetValue(id,out value))return value;
        value.Item=ItemClass.GetItemClass(id,false);
        value.Title=RebirthLegacyMusicPlaybackService.GetSongName(value.Item,id);
        presentation.Add(id,value);
        return value;
    }
    private readonly XUiController[] slots=new XUiController[24];
    private readonly XUiV_Sprite[] icons=new XUiV_Sprite[24];
    private readonly XUiV_Label[] storedTitles=new XUiV_Label[24];
    private readonly XUiV_Sprite[] storedTitlePlates=new XUiV_Sprite[24];
    private readonly XUiV_Sprite[] availableTitlePlates=new XUiV_Sprite[8];
    private readonly XUiV_Sprite[] availableIcons=new XUiV_Sprite[8];
    private XUiV_Sprite storedPreview,availablePreview,storedPreviewPlate,availablePreviewPlate;
    private XUiV_Label storedPreviewTitle,availablePreviewTitle;
    private readonly XUiV_Sprite[] selections=new XUiV_Sprite[24];
    private readonly XUiController[] availableControls=new XUiController[8];
    private readonly XUiV_Label[] availableLabels=new XUiV_Label[8];
    private XUiV_Label selectedLabel, modeLabel, pageLabel, availableDetail, storedEmpty, backpackEmpty;
    private AvailableCassette? inspectedAvailable;
    private bool libraryDirty=true;
    private int page,selected=-1;
    private long displayedGeneration=-1;
    private float refresh;
    private int dragStored=-1;
    private AvailableCassette? dragCarried;
    private long dragGeneration,dragRevision;
    private EntityPlayerLocal dragOwner;
    private void CancelDrag(){dragStored=-1;dragCarried=null;dragOwner=null;}
    private void CassetteDrag(XUiController sender,EDragType type,UnityEngine.Vector2 delta)
    {
        var player=xui?.playerUI?.entityPlayer;
        if(!IsOpen||player==null||RebirthConsoleInputGuardRuntime.BlocksGameplayInput()||
            !RebirthMusicLibraryClient.EnsureCurrent(player)||RebirthMusicLibraryClient.PendingTransfer){CancelDrag();return;}
        if(type==EDragType.DragStart)
        {
            // The visible slot belongs to the rendered generation, not necessarily the
            // latest received playlist. Never reinterpret a stale card as a new item.
            if(displayedGeneration!=RebirthMusicLibraryClient.Generation)
            {CancelDrag();Render();return;}
            CancelDrag();dragOwner=player;dragGeneration=displayedGeneration;dragRevision=RebirthMusicLibraryClient.Revision;
            int stored=System.Array.IndexOf(slots,sender),carried=System.Array.IndexOf(availableControls,sender);
            if(stored>=0&&stored<RebirthMusicLibraryClient.Items.Count)dragStored=stored;
            else if(carried>=0&&page*8+carried<available.Count)dragCarried=available[page*8+carried];
        }
        if(type!=EDragType.DragEnd)return;
        int source=dragStored;var item=dragCarried;var owner=dragOwner;
        long generation=dragGeneration,revision=dragRevision;CancelDrag();
        if(!ReferenceEquals(owner,player)||generation!=RebirthMusicLibraryClient.Generation||revision!=RebirthMusicLibraryClient.Revision)return;
        for(int target=0;target<slots.Length;target++)if(IsDropOver(slots[target]))
        {
            if(source>=0)RebirthMusicLibraryClient.RequestReorder(player,source,target,generation,revision);
            else if(item.HasValue)
            {
                var choice=item.Value;
                if(available.Exists(a=>SameAvailable(a,choice)))
                    RebirthMusicLibraryClient.Dispatch(player,1,0,choice.Value);
            }
            return;
        }
        if(source>=0)foreach(var target in availableControls)if(IsDropOver(target))
        {RebirthMusicLibraryClient.Dispatch(player,2,source);return;}
    }
    private static bool IsDropOver(XUiController control)
    {
        var view=control?.ViewComponent;var t=view?.UiTransform;var camera=UICamera.currentCamera;
        if(t==null||camera==null||!view.IsVisible||!t.gameObject.activeInHierarchy)return false;
        UnityEngine.Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(UnityEngine.Vector2)UnityEngine.Input.mousePosition;
        var ray=camera.ScreenPointToRay(mouse);float distance;
        if(!new UnityEngine.Plane(t.forward,t.position).Raycast(ray,out distance))return false;
        UnityEngine.Vector2 point=t.InverseTransformPoint(ray.GetPoint(distance));
        return point.x>=0&&point.x<view.Size.x&&point.y<=0&&point.y>-view.Size.y;
    }
    public override void OnClose(){CancelDrag();base.OnClose();}
    public override void Cleanup()
    {
        foreach(var slot in slots)if(slot!=null)slot.OnDrag-=CassetteDrag;
        foreach(var slot in availableControls)if(slot!=null)slot.OnDrag-=CassetteDrag;
        presentation.Clear();UnbindPressHandlers();CancelDrag();base.Cleanup();
    }
    public override void Init()
    {
        foreach(var slot in slots)if(slot!=null)slot.OnDrag-=CassetteDrag;
        foreach(var slot in availableControls)if(slot!=null)slot.OnDrag-=CassetteDrag;
        presentation.Clear();UnbindPressHandlers();CancelDrag();
        base.Init();
        for(int i=0;i<24;i++)
        {
            int slot=i;
            slots[i]=GetChildById("musicSlot"+i);
            icons[i]=GetChildById("musicIcon"+i)?.ViewComponent as XUiV_Sprite;
            storedTitles[i]=GetChildById("musicTitle"+i)?.ViewComponent as XUiV_Label;
            storedTitlePlates[i]=GetChildById("musicTitlePlate"+i)?.ViewComponent as XUiV_Sprite;
            selections[i]=GetChildById("musicSelection"+i)?.ViewComponent as XUiV_Sprite;
            slots[i].OnDrag+=CassetteDrag;
            BindControl(slots[i],(s,b)=>{selected=slot;if(ShiftHeld()&&SelectionIsCurrent())RebirthMusicLibraryClient.Dispatch(xui.playerUI.entityPlayer,2,selected);Render();});
        }
        for(int i=0;i<8;i++)
        {
            int row=i;
            availableIcons[i]=GetChildById("musicAvailableIcon"+i)?.ViewComponent as XUiV_Sprite;
            availableLabels[i]=GetChildById("musicAvailableLabel"+i)?.ViewComponent as XUiV_Label;
            availableTitlePlates[i]=GetChildById("musicAvailableTitlePlate"+i)?.ViewComponent as XUiV_Sprite;
            availableControls[i]=GetChildById("musicAvailable"+i);
            availableControls[i].OnDrag+=CassetteDrag;
            BindControl(availableControls[i],(s,b)=>
            {
                int index=page*8+row;
                if(index>=available.Count)return;
                var choice=available[index];
                inspectedAvailable=choice;
                if(ShiftHeld())RebirthMusicLibraryClient.Dispatch(xui.playerUI.entityPlayer,1,0,choice.Value);
                Render();
            });
        }
        Bind("musicPlay",(s,b)=>{if(SelectionIsCurrent())RebirthLegacyMusicPlaybackService.PlayLibrary(xui.playerUI.entityPlayer,selected);});
        Bind("musicPause",(s,b)=>RebirthLegacyMusicPlaybackService.TogglePause(xui.playerUI.entityPlayer));
        Bind("musicNext",(s,b)=>RebirthLegacyMusicPlaybackService.StepLibrary(xui.playerUI.entityPlayer,1));
        Bind("musicRemove",(s,b)=>{if(SelectionIsCurrent())RebirthMusicLibraryClient.Dispatch(xui.playerUI.entityPlayer,2,selected);});
        Bind("musicShuffle",(s,b)=>RebirthMusicLibraryClient.Dispatch(xui.playerUI.entityPlayer,3,RebirthMusicLibraryClient.Shuffle?0:1));
        Bind("musicPagePrev",(s,b)=>{page=System.Math.Max(0,page-1);Render();});
        Bind("musicPageNext",(s,b)=>{if((page+1)*8<available.Count)page++;Render();});
        Bind("musicAudiobooks",(s,b)=>{xui.playerUI.windowManager.Close("rebirthMusicLibrary");xui.playerUI.windowManager.Open("rebirthAudiobookLibrary",true);});
        Bind("musicClose",(s,b)=>xui.playerUI.windowManager.Close("rebirthMusicLibrary"));
        storedPreview=GetChildById("musicStoredPreview")?.ViewComponent as XUiV_Sprite;
        availablePreview=GetChildById("musicCarriedPreview")?.ViewComponent as XUiV_Sprite;
        storedPreviewPlate=GetChildById("musicStoredPreviewPlate")?.ViewComponent as XUiV_Sprite;
        availablePreviewPlate=GetChildById("musicCarriedPreviewPlate")?.ViewComponent as XUiV_Sprite;
        storedPreviewTitle=GetChildById("musicStoredPreviewTitle")?.ViewComponent as XUiV_Label;
        availablePreviewTitle=GetChildById("musicCarriedPreviewTitle")?.ViewComponent as XUiV_Label;
        selectedLabel=GetChildById("musicSelected")?.ViewComponent as XUiV_Label;
        availableDetail=GetChildById("musicAvailableDetail")?.ViewComponent as XUiV_Label;
        storedEmpty=GetChildById("musicStoredEmpty")?.ViewComponent as XUiV_Label;
        backpackEmpty=GetChildById("musicBackpackEmpty")?.ViewComponent as XUiV_Label;
        modeLabel=GetChildById("musicMode")?.ViewComponent as XUiV_Label;
        pageLabel=GetChildById("musicPage")?.ViewComponent as XUiV_Label;
    }
    private void SetPreview(XUiV_Sprite sprite,string id)
    {
        if(sprite==null)return;
        var item=ResolvePresentation(id).Item;
        sprite.IsVisible=item!=null;if(item!=null)sprite.SpriteName=item.GetIconName();
    }
    private void SetInspectionTitle(XUiV_Sprite preview,XUiV_Sprite plate,XUiV_Label title,string id)
    {
        bool visible=preview!=null&&preview.IsVisible&&!string.IsNullOrEmpty(id);
        if(plate!=null)plate.IsVisible=visible;
        Label(title,visible?SongTitle(id):string.Empty);
        if(title!=null)title.IsVisible=visible;
    }
    private static bool ShiftHeld() => UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)||UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightShift);
    private string SongTitle(string id)
    {
        return ResolvePresentation(id).Title;
    }
    private string InspectionText(string id)
    {
        var value=ResolvePresentation(id);
        if(value.Item==null)return value.Title??string.Empty;
        string key=value.Item.GetItemDescriptionKey();
        if(string.IsNullOrEmpty(key)||!Localization.Exists(key))return value.Title;
        string description=Localization.Get(key);
        return string.IsNullOrEmpty(description)||description==value.Title?value.Title:value.Title+"\n"+description;
    }
    private void Bind(string id, XUiEvent_OnPressEventHandler handler)
    {
        var control=GetChildById(id);
        BindControl(control,handler);
    }
    private void BindControl(XUiController control,XUiEvent_OnPressEventHandler handler)
    {
        if(control==null)return;
        // Native press callbacks also report right-click. Only left/main activation
        // selects or transfers; console input and closed windows cannot dispatch.
        XUiEvent_OnPressEventHandler guarded=(sender,button)=>
        {
            if(!IsOpen||(button!=0&&button!=-1)||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;
            handler(sender,button);
        };
        // Retain the guarded delegates so cleanup/reinitialization unwires exactly.
        if(control is XUiC_SimpleButton simple) simple.OnPressed+=guarded;
        else control.OnPress+=guarded;
        pressBindings.Add(new PressBinding{Control=control,Handler=guarded});
    }
    private void UnbindPressHandlers()
    {
        foreach(var binding in pressBindings)
            if(binding.Control is XUiC_SimpleButton simple) simple.OnPressed-=binding.Handler;
            else binding.Control.OnPress-=binding.Handler;
        pressBindings.Clear();
    }
    public override void OnOpen(){base.OnOpen();selected=-1;inspectedAvailable=null;page=0;refresh=0;libraryDirty=true;RebirthMusicLibraryClient.Dispatch(xui.playerUI.entityPlayer,0);Render();}
    public override void Update(float dt)
    {base.Update(dt);if(!IsOpen)return;refresh+=dt;if(refresh<0.5f)return;refresh=0;Render();}
    private static void Label(XUiV_Label label,string text){if(label!=null && label.Text!=text)label.Text=text;}
    private bool SelectionIsCurrent()
    {
        if(!RebirthMusicLibraryClient.EnsureCurrent(xui?.playerUI?.entityPlayer)||RebirthMusicLibraryClient.PendingTransfer)return false;
        if(displayedGeneration!=RebirthMusicLibraryClient.Generation){Render();return false;}
        return selected>=0 && selected<RebirthMusicLibraryClient.Items.Count;
    }
    private void Render()
    {
        presentation.Clear();
        RebirthMusicLibraryClient.EnsureCurrent(xui?.playerUI?.entityPlayer);
        if(displayedGeneration!=RebirthMusicLibraryClient.Generation)
        {selected=-1;inspectedAvailable=null;displayedGeneration=RebirthMusicLibraryClient.Generation;libraryDirty=true;}
        available.Clear();var player=xui?.playerUI?.entityPlayer;if(player==null)return;
        Collect(player.bag?.ItemGrid.items,int.MaxValue);
        var belt=player.inventory;
        if(belt!=null)
        {
            int count=System.Math.Min(belt.Length,RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length));
            for(int i=0;i<count;i++)Collect(belt.GetItem(i));
        }
        page=System.Math.Min(page,System.Math.Max(0,(available.Count-1)/8));
        if(libraryDirty) for(int i=0;i<24;i++)
        {
            var icon=icons[i];
            var item=i<RebirthMusicLibraryClient.Items.Count?ResolvePresentation(RebirthMusicLibraryClient.Items[i]).Item:null;
            if(icon!=null){icon.IsVisible=item!=null;if(item!=null)icon.SpriteName=item.GetIconName();}
            Label(storedTitles[i],item!=null?SongTitle(RebirthMusicLibraryClient.Items[i]):string.Empty);
            if(storedTitlePlates[i]!=null)storedTitlePlates[i].IsVisible=item!=null;
            slots[i].ViewComponent.ToolTip=item!=null?Localization.Get(item.GetItemName()):Localization.Get("xuiRebirthMusicEmptySlot");
        }
        libraryDirty=false;
        for(int i=0;i<selections.Length;i++)
            if(selections[i]!=null)selections[i].IsVisible=i==selected && i<RebirthMusicLibraryClient.Items.Count;
        for(int i=0;i<8;i++)
        {
            int index=page*8+i;
            string title=index<available.Count?SongTitle(available[index].Name):string.Empty;
            Label(availableLabels[i],title);
            if(availableTitlePlates[i]!=null)availableTitlePlates[i].IsVisible=index<available.Count;
            SetPreview(availableIcons[i],index<available.Count?available[index].Name:null);
            if(availableControls[i]?.ViewComponent!=null)availableControls[i].ViewComponent.ToolTip=title;
        }
        string selectedText=RebirthMusicLibraryClient.PendingTransfer?Localization.Get("xuiRebirthMusicTransferPending"):
            selected>=0&&selected<RebirthMusicLibraryClient.Items.Count?InspectionText(RebirthMusicLibraryClient.Items[selected]):Localization.Get("xuiRebirthMusicSelect");
        if(inspectedAvailable.HasValue)
        {
            var inspected=inspectedAvailable.Value;
            if(!available.Exists(a=>SameAvailable(a,inspected)))inspectedAvailable=null;
        }
        SetPreview(storedPreview,selected>=0&&selected<RebirthMusicLibraryClient.Items.Count?RebirthMusicLibraryClient.Items[selected]:null);
        SetPreview(availablePreview,inspectedAvailable.HasValue?inspectedAvailable.Value.Name:null);
        SetInspectionTitle(storedPreview,storedPreviewPlate,storedPreviewTitle,selected>=0&&selected<RebirthMusicLibraryClient.Items.Count?RebirthMusicLibraryClient.Items[selected]:null);
        SetInspectionTitle(availablePreview,availablePreviewPlate,availablePreviewTitle,inspectedAvailable.HasValue?inspectedAvailable.Value.Name:null);
        string availableText=inspectedAvailable.HasValue?InspectionText(inspectedAvailable.Value.Name):Localization.Get("xuiRebirthMusicInspectAvailable");
        Label(availableDetail,availableText);
        if(availableDetail!=null && availableDetail.ToolTip!=availableText)availableDetail.ToolTip=availableText;
        if(storedEmpty!=null)storedEmpty.IsVisible=RebirthMusicLibraryClient.Items.Count==0;
        if(backpackEmpty!=null)backpackEmpty.IsVisible=available.Count==0;
        Label(selectedLabel,selectedText);
        if(selectedLabel!=null && selectedLabel.ToolTip!=selectedText)selectedLabel.ToolTip=selectedText;
        Label(modeLabel,Localization.Get(RebirthMusicLibraryClient.Shuffle?"xuiRebirthMusicShuffle":"xuiRebirthMusicOrdered"));
        Label(pageLabel,(page+1)+" / "+System.Math.Max(1,(available.Count+7)/8));
    }
    private static bool SameAvailable(AvailableCassette current,AvailableCassette selected)
    {
        if(current.Type!=selected.Type||current.Seed!=selected.Seed||current.Name!=selected.Name)return false;
        try{return RebirthMusicLibraryService.Encode(current.Value)==RebirthMusicLibraryService.Encode(selected.Value);}
        catch{return false;}
    }
    private void Collect(ItemStack[] slots,int limit)
    {
        if(slots==null)return;
        for(int i=0;i<System.Math.Min(limit,slots.Length);i++)
        {
            Collect(slots[i]);
        }
    }
    private void Collect(ItemStack stack)
    {
        if(stack==null || stack.IsEmpty())return;
        string name=stack.itemValue.ItemClass?.GetItemName();
        if(RebirthMusicLibraryService.IsMusicCassette(name))
            available.Add(new AvailableCassette{Type=stack.itemValue.type,Seed=stack.itemValue.Seed,Name=name,Value=stack.itemValue.Clone()});
    }

}
