using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthAudiobookLibrary : XUiController
{
    private struct Cassette { public int StoredIndex; public string SlotId; public int Type; public ushort Seed; public string Id; public RebirthLiteratureDefinition Source; public ItemValue Value; }
    private readonly List<Cassette> items=new List<Cassette>();
    private readonly XUiV_Label[] titles=new XUiV_Label[8];
    private readonly XUiV_Sprite[] icons=new XUiV_Sprite[8];
    private readonly XUiV_Sprite[] readMarkers=new XUiV_Sprite[8];
    private readonly XUiV_Label[] statuses=new XUiV_Label[8];
    private readonly XUiController[] rows=new XUiController[8];
    private readonly List<KeyValuePair<XUiC_SimpleButton,XUiEvent_OnPressEventHandler>> bindings=new List<KeyValuePair<XUiC_SimpleButton,XUiEvent_OnPressEventHandler>>();
    private XUiV_Label progressLabel,pageLabel,emptyLabel;
    private int page;
    private float refresh;
    public override void Init()
    {
        Unbind();base.Init();
        for(int i=0;i<8;i++)
        {
            int index=i;
            rows[i]=GetChildById("audioRow"+i);
            titles[i]=GetChildById("audioTitle"+i)?.ViewComponent as XUiV_Label;
            statuses[i]=GetChildById("audioStatus"+i)?.ViewComponent as XUiV_Label;
            icons[i]=GetChildById("audioIcon"+i)?.ViewComponent as XUiV_Sprite;
            readMarkers[i]=GetChildById("audioReadMarker"+i)?.ViewComponent as XUiV_Sprite;
            Bind("audioListen"+i,(s,b)=>Listen(index));
            Bind("audioInsert"+i,(s,b)=>Listen(index));
            Bind("audioReturn"+i,(s,b)=>Return(index));
        }
        progressLabel=GetChildById("audioListeningProgress")?.ViewComponent as XUiV_Label;
        pageLabel=GetChildById("audioPage")?.ViewComponent as XUiV_Label;
        emptyLabel=GetChildById("audioEmpty")?.ViewComponent as XUiV_Label;
        Bind("audioPrevious",(s,b)=>{page=Math.Max(0,page-1);Render();});
        Bind("audioNext",(s,b)=>{if((page+1)*8<items.Count)page++;Render();});
        Bind("audioMusic",(s,b)=>{xui.playerUI.windowManager.Close("rebirthAudiobookLibrary");xui.playerUI.windowManager.Open("rebirthMusicLibrary",true);});
        Bind("audioPause",(s,b)=>RebirthAudiobookLibraryClient.RequestPlayback(xui?.playerUI?.entityPlayer,false));
        Bind("audioResume",(s,b)=>RebirthAudiobookLibraryClient.RequestPlayback(xui?.playerUI?.entityPlayer,true));
        Bind("audioClose",(s,b)=>xui.playerUI.windowManager.Close("rebirthAudiobookLibrary"));
    }
    private void Bind(string id,XUiEvent_OnPressEventHandler handler)
    {
        var button=GetChildById(id) as XUiC_SimpleButton;
        if(button==null)return;
        button.OnPressed+=handler;bindings.Add(new KeyValuePair<XUiC_SimpleButton,XUiEvent_OnPressEventHandler>(button,handler));
    }
    private void Unbind(){foreach(var binding in bindings)binding.Key.OnPressed-=binding.Value;bindings.Clear();}
    public override void Cleanup(){Unbind();items.Clear();base.Cleanup();}
    public override void OnOpen(){base.OnOpen();page=0;refresh=0;var player=xui?.playerUI?.entityPlayer;if(player?.world!=null){if(player.world.IsRemote())RebirthMusicLibraryClient.Dispatch(player,0);else RebirthAudiobookLibraryClient.RefreshLocal(player);}Render();}
    public override void Update(float dt){base.Update(dt);if(!IsOpen)return;refresh+=dt;if(refresh<0.5f)return;refresh=0;Render();}
    private void Collect(ItemStack stack)
    {
        var value=stack?.itemValue;
        if(stack==null||stack.IsEmpty()||value?.ItemClass==null)return;
        string id=value.ItemClass.GetItemName();
        RebirthAudiobookDefinition audio;RebirthLiteratureDefinition source;
        if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(id,out audio)||audio==null||!RebirthProgressionRuntimeConfig.TryGetLiterature(audio.SourceLiteratureId,out source)||source==null)return;
        items.Add(new Cassette{Type=value.type,Seed=value.Seed,Id=id,Source=source,Value=value.Clone()});
    }
    private void Listen(int row)
    {
        int index=page*8+row;if(index<0||index>=items.Count)return;
        var item=items[index];var player=xui?.playerUI?.entityPlayer;
        // The authoritative dispatcher revalidates possession, identity, device and completion.
        if(!string.IsNullOrEmpty(item.SlotId))RebirthAudiobookLibraryClient.RequestListen(player,item.StoredIndex,item.SlotId);
        else ItemActionListenAudiobookRebirth.Dispatch(player,item.Value);
    }
    private void Return(int row)
    {
        int index=page*8+row;if(index<0||index>=items.Count)return;
        var item=items[index];if(string.IsNullOrEmpty(item.SlotId))return;
        // Do not silently refresh an old row index into a different stored tape.
        if(item.StoredIndex<0||item.StoredIndex>=RebirthAudiobookLibraryClient.SlotIds.Count||
            RebirthAudiobookLibraryClient.SlotIds[item.StoredIndex]!=item.SlotId)return;
        RebirthAudiobookLibraryClient.RequestTransfer(xui?.playerUI?.entityPlayer,false,item.StoredIndex,null);
    }
    private static void Text(XUiV_Label label,string text){if(label!=null&&label.Text!=text)label.Text=text;}
    private void Render()
    {
        var player=xui?.playerUI?.entityPlayer;if(player==null)return;
        items.Clear();
        if(player.world!=null&&!player.world.IsRemote())RebirthAudiobookLibraryClient.RefreshLocal(player);
        if(RebirthMusicLibraryClient.EnsureCurrent(player))
        for(int i=0;i<RebirthAudiobookLibraryClient.Items.Count;i++)
        {
            string id=RebirthAudiobookLibraryClient.Items[i];RebirthAudiobookDefinition audio;RebirthLiteratureDefinition source=null;
            if(RebirthProgressionRuntimeConfig.TryGetAudiobook(id,out audio)&&audio!=null)
                RebirthProgressionRuntimeConfig.TryGetLiterature(audio.SourceLiteratureId,out source);
            items.Add(new Cassette {Id=id,Source=source,SlotId=RebirthAudiobookLibraryClient.SlotIds[i],StoredIndex=i});
        }
        var bag=player.bag?.ItemGrid.items;if(bag!=null)foreach(var stack in bag)Collect(stack);
        var belt=player.inventory;if(belt!=null)for(int i=0;i<Math.Min(belt.Length,RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length));i++)Collect(belt.GetItem(i));
        page=Math.Min(page,Math.Max(0,(items.Count-1)/8));
        for(int i=0;i<8;i++)
        {
            int index=page*8+i;bool visible=index<items.Count;
            if(rows[i]?.ViewComponent!=null)rows[i].ViewComponent.IsVisible=visible;
            if(!visible)continue;
            var item=items[index];var definition=ItemClass.GetItemClass(item.Id,false);
            Text(titles[i],Localization.Get(item.Id));
            bool completed=item.Source!=null&&RebirthLiteratureService.IsAlreadyCompleted(player,item.Source);
            Text(statuses[i],Localization.Get(!string.IsNullOrEmpty(item.SlotId)?"xuiRebirthAudioStored":"xuiRebirthAudioCarried")+" - "+Localization.Get(completed?"xuiRebirthAudioCompleted":"xuiRebirthAudioUnprocessed"));
            if(readMarkers[i]!=null){readMarkers[i].IsVisible=item.Source!=null;readMarkers[i].SpriteName=completed?"ui_game_symbol_book_read":"ui_game_symbol_book";}
            bool stored=!string.IsNullOrEmpty(item.SlotId);
            var listen=GetChildById("audioListen"+i)?.ViewComponent;if(listen!=null)listen.IsVisible=stored&&item.Source!=null;
            var insert=GetChildById("audioInsert"+i)?.ViewComponent;if(insert!=null)insert.IsVisible=!stored;
            var remove=GetChildById("audioReturn"+i)?.ViewComponent;if(remove!=null)remove.IsVisible=stored;
            if(icons[i]!=null){icons[i].IsVisible=definition!=null;if(definition!=null)icons[i].SpriteName=definition.GetIconName();}
        }
        if(emptyLabel!=null)emptyLabel.IsVisible=items.Count==0;
        Text(pageLabel,(page+1)+" / "+Math.Max(1,(items.Count+7)/8));
        string sourceId=string.Empty;float fraction=0,remaining=0;bool listening=false;
        if(player.world!=null&&player.world.IsRemote())
        {
            RebirthStudyHudMode mode;bool slow;
            listening=RebirthStudyHudClientState.TryGet(out mode,out sourceId,out fraction,out remaining,out slow)&&mode==RebirthStudyHudMode.Audiobook;
        }
        else {string cassette;listening=RebirthAudiobookListeningSessionService.TryGetUiState(player,out cassette,out sourceId,out fraction,out remaining);}
        Text(progressLabel,listening?Localization.Get(sourceId)+"  -  "+Mathf.RoundToInt(fraction*100f)+"%  -  "+Mathf.CeilToInt(remaining)+"s":Localization.Get("xuiRebirthAudioNotListening"));
    }
}