using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

// One cassette workspace; physical custody continues through the existing saved journals.
[Preserve]
public sealed class XUiC_RebirthMusicLibrary : XUiController
{
    private sealed class Cassette
    {
        internal string Id, SlotId, Fingerprint;
        internal int Index, Count;
        internal bool Audio, Stored;
        internal ItemValue Value;
    }
    private sealed class Cell
    {
        internal XUiController Button;
        internal XUiC_RebirthReadonlyItemSlot Display;
        internal XUiV_Sprite Icon, Selection;
        internal XUiV_Label Title, Count;
        internal Cassette Item;
    }
    private readonly List<Cassette> library=new List<Cassette>(), carried=new List<Cassette>();
    private readonly List<Action> unbind=new List<Action>();
    private readonly Cell[] libraryCells=new Cell[24], bagCells=new Cell[60];
    private XUiC_RebirthDiscordScrollbar libraryScroll,bagScroll;
    private XUiV_Label detail,title,nowPlaying,capacity,bagCount;
    private XUiV_Sprite playingIcon;
    private XUiC_RebirthReadonlyItemSlot selectedDisplay,dragDisplay;
    private XUiController insert,remove,play;
    private Cassette selected,drag;
    private bool audioTab,sort,audioPlaying;
    private XUiC_TextInput searchInput;
    private string search=string.Empty;
    private int libraryRow,bagRow;
    private float refresh;
    private Vector2i layoutBounds=new Vector2i(-1,-1);
    private long generation=-1,audioRevision=-1,dragGeneration,dragAudioRevision;
    private EntityPlayerLocal dragOwner;
    private EntityPlayerLocal Player=>xui?.playerUI?.entityPlayer;
    private bool Ready=>IsOpen&&Player!=null&&!RebirthConsoleInputGuardRuntime.BlocksGameplayInput()&&RebirthMusicLibraryClient.EnsureCurrent(Player)&&!RebirthMusicLibraryClient.PendingTransfer;
    public override void Init()
    {
        Unbind();base.Init();
        BindCells(libraryCells,"library",6);BindCells(bagCells,"cassetteBag",20);
        libraryScroll=GetChildById("libraryScrollbar") as XUiC_RebirthDiscordScrollbar;
        bagScroll=GetChildById("cassetteBagScrollbar") as XUiC_RebirthDiscordScrollbar;
        libraryScroll?.Configure(()=>Math.Max(4,((search.Length>0?library.Count:audioTab?RebirthAudiobookLibraryPersistence.Capacity:RebirthMusicLibraryService.Capacity)+5)/6),()=>4,()=>libraryRow,n=>{if(n!=libraryRow){libraryRow=n;DrawCells();}});
        bagScroll?.Configure(()=>Math.Max(3,(carried.Count+19)/20),()=>3,()=>bagRow,n=>{if(n!=bagRow){bagRow=n;DrawCells();}});
        searchInput=GetChildById("cassetteSearch") as XUiC_TextInput;
        Bind("musicTab",()=>SetTab(false));Bind("audioTab",()=>SetTab(true));
        Bind("cassetteSort",()=>{sort=true;Refresh();});
        Bind("cassetteInsert",Transfer);Bind("cassetteRemove",Transfer);
        Bind("musicPlay",Play);Bind("musicPrevious",()=>RebirthLegacyMusicPlaybackService.StepLibrary(Player,-1));
        Bind("musicNext",()=>RebirthLegacyMusicPlaybackService.StepLibrary(Player,1));
        Bind("musicPause",()=>{if(audioPlaying)RebirthAudiobookLibraryClient.RequestPlayback(Player,false);else RebirthLegacyMusicPlaybackService.TogglePause(Player);});
        Bind("musicResume",()=>RebirthAudiobookLibraryClient.RequestPlayback(Player,true));
        Bind("musicShuffle",()=>RebirthMusicLibraryClient.Dispatch(Player,3,RebirthMusicLibraryClient.Shuffle?0:1));
        Bind("musicClose",()=>xui.playerUI.windowManager.Close("rebirthMusicLibrary"));
        detail=GetChildById("cassetteDescription")?.ViewComponent as XUiV_Label;
        title=GetChildById("cassetteTitle")?.ViewComponent as XUiV_Label;
        nowPlaying=GetChildById("nowPlaying")?.ViewComponent as XUiV_Label;
        capacity=GetChildById("libraryCapacity")?.ViewComponent as XUiV_Label;
        bagCount=GetChildById("cassetteBagCount")?.ViewComponent as XUiV_Label;
        selectedDisplay=GetChildById("cassetteSelectedDisplay") as XUiC_RebirthReadonlyItemSlot;
        dragDisplay=GetChildById("cassetteDragDisplay") as XUiC_RebirthReadonlyItemSlot;
        playingIcon=GetChildById("playingPreview")?.ViewComponent as XUiV_Sprite;
        insert=GetChildById("cassetteInsert");remove=GetChildById("cassetteRemove");play=GetChildById("musicPlay");
    }
    private void SetTab(bool audio){audioTab=audio;libraryRow=0;selected=null;Refresh();}
    private void Bind(string id,Action action)
    {
        var control=GetChildById(id);if(control==null)return;
        XUiEvent_OnPressEventHandler handler=(s,b)=>{if((id=="musicClose"||Ready)&&(b==0||b==-1))action();};
        if(control is XUiC_SimpleButton button){button.OnPressed+=handler;unbind.Add(()=>button.OnPressed-=handler);}
        else {control.OnPress+=handler;unbind.Add(()=>control.OnPress-=handler);}
    }
    private void BindCells(Cell[] cells,string prefix,int columns)
    {
        for(int i=0;i<cells.Length;i++)
        {
            var cell=new Cell{Display=GetChildById(prefix+"Display"+i) as XUiC_RebirthReadonlyItemSlot,Button=GetChildById(prefix+i),Icon=GetChildById(prefix+"Icon"+i)?.ViewComponent as XUiV_Sprite,
                Title=GetChildById(prefix+"Title"+i)?.ViewComponent as XUiV_Label,Count=GetChildById(prefix+"Count"+i)?.ViewComponent as XUiV_Label,
                Selection=GetChildById(prefix+"Selection"+i)?.ViewComponent as XUiV_Sprite};
            cells[i]=cell;
            Bind(prefix+i,()=>{selected=cell.Item;DrawDetails();if(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))Transfer();});
            if(cell.Button==null)throw new InvalidOperationException("Walkman layout is missing "+prefix+i);
            cell.Button.OnDrag+=Drag;unbind.Add(()=>cell.Button.OnDrag-=Drag);
            XUiEvent_OnScrollEventHandler wheel=(s,d)=>{if(cells==libraryCells)libraryScroll?.Wheel(d);else bagScroll?.Wheel(d);};
            cell.Button.OnScroll+=wheel;unbind.Add(()=>cell.Button.OnScroll-=wheel);
        }
    }
    private void Unbind(){foreach(var action in unbind)action();unbind.Clear();}
    public override void Cleanup(){Unbind();base.Cleanup();}
    public override void OnOpen(){base.OnOpen();layoutBounds=new Vector2i(-1,-1);selected=drag=null;libraryRow=bagRow=0;generation=audioRevision=-1;RebirthMusicLibraryClient.Dispatch(Player,0);Refresh();}
    public override void OnClose(){selected=drag=null;dragOwner=null;base.OnClose();}
    public override void Update(float dt)
    {
        base.Update(dt);if(!IsOpen)return;ApplyLayout();UpdateDragDisplay();
        if(Ready&&UIInput.selection==null&&Input.GetKeyDown(KeyCode.W))Transfer();
        refresh+=dt;if(refresh<.25f)return;refresh=0;Refresh();
    }
    private void ApplyLayout()
    {
        Vector2i p,s;RebirthScreenLayout.GetScreenBounds(xui,out p,out s);
        if(layoutBounds.x==s.x&&layoutBounds.y==s.y)return;layoutBounds=s;
        float scale=Math.Min((s.x-16)/1872f,Math.Max(1,s.y-78)/830f);
        ViewComponent.Position=new Vector2i(p.x+8,p.y-70);ViewComponent.TryUpdatePosition();
        if(ViewComponent.UiTransform!=null)ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);
    }
    public void SelectAudiobooks(){SetTab(true);}
    private static string Name(string id)=>RebirthMusicLibraryService.IsMusicCassette(id)?RebirthLegacyMusicPlaybackService.GetSongName(ItemClass.GetItemClass(id,false),id):Localization.Get(id);
    private static void Text(XUiV_Label label,string text){if(label!=null&&label.Text!=text)label.Text=text;}
    private void Refresh()
    {
        var player=Player;if(player?.world==null)return;
        if(!player.world.IsRemote())RebirthAudiobookLibraryClient.RefreshLocal(player);
        if(!RebirthMusicLibraryClient.EnsureCurrent(player))return;
        if(generation!=RebirthMusicLibraryClient.Generation||audioRevision!=RebirthAudiobookLibraryClient.Revision)
        {selected=drag=null;generation=RebirthMusicLibraryClient.Generation;audioRevision=RebirthAudiobookLibraryClient.Revision;}
        string nextSearch=searchInput?.Text??string.Empty;if(search!=nextSearch){search=nextSearch;libraryRow=0;}
        library.Clear();carried.Clear();
        var ids=audioTab?RebirthAudiobookLibraryClient.Items:RebirthMusicLibraryClient.Items;
        for(int i=0;i<ids.Count;i++)library.Add(new Cassette{Id=ids[i],Index=i,Count=1,Stored=true,Audio=audioTab,SlotId=audioTab?RebirthAudiobookLibraryClient.SlotIds[i]:null});
        if(search.Length>0)library.RemoveAll(c=>Name(c.Id).IndexOf(search,StringComparison.CurrentCultureIgnoreCase)<0);
        var bag=player.bag?.ItemGrid?.items;if(bag!=null)foreach(var item in bag)
        {
            if(item==null||item.IsEmpty()||item.itemValue?.ItemClass==null)continue;
            string id=item.itemValue.ItemClass.GetItemName();bool audio=RebirthProgressionRuntimeConfig.TryGetAudiobook(id,out _);
            if(!audio&&!RebirthMusicLibraryService.IsMusicCassette(id))continue;
            carried.Add(new Cassette{Id=id,Audio=audio,Value=item.itemValue.Clone(),Count=item.count,Fingerprint=RebirthMusicLibraryService.Encode(item.itemValue)});
        }
        if(sort)carried.Sort((a,b)=>StringComparer.CurrentCultureIgnoreCase.Compare(Name(a.Id),Name(b.Id)));
        bagRow=Math.Min(bagRow,Math.Max(0,(carried.Count+19)/20-3));
        if(selected!=null&&!selected.Stored)selected=carried.Find(c=>c.Fingerprint==selected.Fingerprint);
        Text(capacity,library.Count+" / "+(audioTab?RebirthAudiobookLibraryPersistence.Capacity:RebirthMusicLibraryService.Capacity));
        Text(bagCount,carried.Count.ToString());DrawDetails();
        string song=RebirthLegacyMusicPlaybackService.NowPlaying;
        string source="",cassette=RebirthLegacyMusicPlaybackService.CurrentCassetteId;float fraction=0,remaining=0;
        bool listening;
        if(player.world.IsRemote()){RebirthStudyHudMode mode;bool slow;listening=RebirthStudyHudClientState.TryGet(out mode,out source,out fraction,out remaining,out slow)&&mode==RebirthStudyHudMode.Audiobook;}
        else listening=RebirthAudiobookListeningSessionService.TryGetUiState(player,out cassette,out source,out fraction,out remaining);
        audioPlaying=listening;if(!listening)cassette=RebirthLegacyMusicPlaybackService.CurrentCassetteId;
        Text(nowPlaying,listening?Localization.Get(source)+"\n"+Mathf.RoundToInt(fraction*100)+"%":string.IsNullOrEmpty(song)?Localization.Get("xuiRebirthAudioNotListening"):song);
        if(playingIcon!=null){var item=string.IsNullOrEmpty(cassette)?null:ItemClass.GetItemClass(cassette,false);playingIcon.IsVisible=true;playingIcon.SpriteName=item!=null?item.GetIconName():"rb_music_cassette";}
    }
    private void DrawCells(){Draw(libraryCells,library,libraryRow*6);Draw(bagCells,carried,bagRow*20);}
    private void Draw(Cell[] cells,List<Cassette> items,int start)
    {
        for(int i=0;i<cells.Length;i++)
        {
            var cell=cells[i];cell.Item=start+i<items.Count?items[start+i]:null;var item=cell.Item;
            cell.Display?.Show(Stack(item));
            var definition=item==null?null:ItemClass.GetItemClass(item.Id,false);
            if(cell.Icon!=null){cell.Icon.IsVisible=definition!=null;if(definition!=null)cell.Icon.SpriteName=definition.GetIconName();}
            Text(cell.Title,item==null?"":Name(item.Id));Text(cell.Count,item==null||item.Count<2?"":item.Count.ToString());
            if(cell.Selection!=null)cell.Selection.IsVisible=Same(selected,item);
            cell.Button.ViewComponent.ToolTip=item==null?"":Name(item.Id);
        }
    }
    private static bool Same(Cassette a,Cassette b)=>a!=null&&b!=null&&a.Stored==b.Stored&&a.Audio==b.Audio&&(a.Stored?a.Index==b.Index&&a.Id==b.Id:a.Fingerprint==b.Fingerprint);
    private void DrawDetails()
    {
        var item=selected;var definition=item==null?null:ItemClass.GetItemClass(item.Id,false);
        Text(title,item==null?"":Name(item.Id));Text(detail,definition==null?"":Localization.Get(definition.GetItemDescriptionKey()));
        selectedDisplay?.Show(Stack(item));
        if(selectedDisplay!=null)selectedDisplay.ViewComponent.IsVisible=item!=null;
        if(insert!=null)insert.ViewComponent.IsVisible=item!=null&&!item.Stored;
        if(remove!=null)remove.ViewComponent.IsVisible=item!=null&&item.Stored;
        if(play!=null)play.ViewComponent.IsVisible=item!=null&&item.Stored;
        DrawCells();
    }
    private static ItemStack Stack(Cassette item)=>item==null?global::ItemStack.Empty:new ItemStack(item.Value??ItemClass.GetItem(item.Id,false),item.Count);
    private void UpdateDragDisplay()
    {
        if(dragDisplay==null)return;dragDisplay.ViewComponent.IsVisible=drag!=null;
        if(drag==null)return;dragDisplay.Show(Stack(drag));
        var t=ViewComponent.UiTransform;var camera=UICamera.currentCamera;if(t==null||camera==null)return;
        Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(Vector2)Input.mousePosition;
        var ray=camera.ScreenPointToRay(mouse);float distance;
        if(!new Plane(t.forward,t.position).Raycast(ray,out distance))return;
        var p=t.InverseTransformPoint(ray.GetPoint(distance));dragDisplay.ViewComponent.Position=new Vector2i(Mathf.RoundToInt(p.x)+8,Mathf.RoundToInt(p.y)-8);dragDisplay.ViewComponent.TryUpdatePosition();
    }
    private bool Current(Cassette item)
    {
        if(!Ready||item==null||generation!=RebirthMusicLibraryClient.Generation||audioRevision!=RebirthAudiobookLibraryClient.Revision)return false;
        if(!item.Stored)return carried.Exists(c=>c.Fingerprint==item.Fingerprint);
        var ids=item.Audio?RebirthAudiobookLibraryClient.Items:RebirthMusicLibraryClient.Items;
        return item.Index>=0&&item.Index<ids.Count&&ids[item.Index]==item.Id&&(!item.Audio||RebirthAudiobookLibraryClient.SlotIds[item.Index]==item.SlotId);
    }
    private void Transfer()
    {
        var item=selected;if(!Current(item))return;
        if(item.Audio)RebirthAudiobookLibraryClient.RequestTransfer(Player,!item.Stored,item.Index,item.Value);
        else RebirthMusicLibraryClient.Dispatch(Player,item.Stored?2:1,item.Index,item.Value);
        selected=null;Refresh();
    }
    private void Play()
    {
        var item=selected;if(!Current(item)||!item.Stored)return;
        if(item.Audio)RebirthAudiobookLibraryClient.RequestListen(Player,item.Index,item.SlotId);
        else RebirthLegacyMusicPlaybackService.PlayLibrary(Player,item.Index);
    }
    private void Drag(XUiController sender,EDragType type,Vector2 delta)
    {
        if(!Ready){drag=null;return;}
        if(type==EDragType.DragStart)
        {
            drag=null;foreach(var cell in libraryCells)if(cell.Button==sender)drag=cell.Item;
            foreach(var cell in bagCells)if(cell.Button==sender)drag=cell.Item;
            if(!Current(drag)){drag=null;return;}
            dragOwner=Player;dragGeneration=generation;dragAudioRevision=audioRevision;
        }
        if(type!=EDragType.DragEnd||drag==null)return;
        var source=drag;drag=null;
        if(!ReferenceEquals(Player,dragOwner)||dragGeneration!=RebirthMusicLibraryClient.Generation||dragAudioRevision!=RebirthAudiobookLibraryClient.Revision||!Current(source))return;
        if(source.Stored&&!source.Audio)foreach(var cell in libraryCells)if(cell.Item!=null&&IsDropOver(cell.Button)){RebirthMusicLibraryClient.RequestReorder(Player,source.Index,cell.Item.Index,dragGeneration,RebirthMusicLibraryClient.Revision);return;}
        var targets=source.Stored?bagCells:libraryCells;
        foreach(var cell in targets)if(IsDropOver(cell.Button)){selected=source;Transfer();return;}
    }
    private static bool IsDropOver(XUiController control)
    {
        var view=control?.ViewComponent;var t=view?.UiTransform;var camera=UICamera.currentCamera;
        if(t==null||camera==null||!view.IsVisible||!t.gameObject.activeInHierarchy)return false;
        Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(Vector2)Input.mousePosition;
        var ray=camera.ScreenPointToRay(mouse);float distance;
        if(!new Plane(t.forward,t.position).Raycast(ray,out distance))return false;
        Vector2 point=t.InverseTransformPoint(ray.GetPoint(distance));
        return point.x>=0&&point.x<view.Size.x&&point.y<=0&&point.y>-view.Size.y;
    }
}
