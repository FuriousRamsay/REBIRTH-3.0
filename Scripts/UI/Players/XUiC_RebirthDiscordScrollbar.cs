using System;
using UnityEngine;
using UnityEngine.Scripting;
[Preserve]
public sealed class XUiC_RebirthDiscordScrollbar : XUiController
{
    private Func<int> count,capacity,index;
    private Action<int> move;
    private XUiV_ScrollBar bar;
    private float last;
    public override void OnOpen() { base.OnOpen(); RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(this); }
    public override void OnClose() { RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(this); base.OnClose(); }
    public override void Init()
    {
        base.Init();
        if(Parent is XUiC_DiscordFriendsList friends)Bind(friends);
        else if(Parent is XUiC_DiscordPendingList pending)Bind(pending);
        else if(Parent is XUiC_DiscordLobbyMemberList members)Bind(members);
    }
    private void Bind<T>(XUiC_List<T> list) where T:XUiListEntry<T>
    {
        list.PagingStepSize=XUiC_List<T>.EPagingStepSize.SingleEntry;
        count=()=>list.EntryCount;capacity=()=>list.PageLength;index=()=>list.CurrentBaseIndex;
        move=value=>{if(list.minIndex==value)return;list.minIndex=value;list.IsDirty=true;};
    }
    private void ScrollbarChanged()
    {
        if (bar?.ScrollBar == null || count == null || move == null
            || Mathf.Approximately(bar.ScrollBar.value,last)) return;
        int maximum=Math.Max(0,count()-capacity());
        if(maximum>0)move((int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.RoundToInt(Mathf.Clamp01(bar.ScrollBar.value)*maximum), maximum, capacity(), RebirthScrollbarPagingPolicy.Enabled));
    }
    public override void Update(float dt)
    {
        base.Update(dt);if(count==null)return;
        if(bar==null)foreach(var child in Children)if(child.ViewComponent is XUiV_ScrollBar candidate&&candidate.ScrollBar!=null){bar=candidate;RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(this);bar.Connect((s,d)=>move(RebirthScrollbarPagingPolicy.Enabled ? (int)RebirthScrollbarPagingPolicy.Step(index(),Math.Max(0,count()-capacity()),capacity(),d>0?-1:d<0?1:0) : Mathf.Clamp(index()+(d>0?-1:1),0,Math.Max(0,count()-capacity()))), ScrollbarChanged);bar.ScrollBar.fillDirection=UIProgressBar.FillDirection.TopToBottom;break;}
        if(bar?.ScrollBar==null)return;
        int maximum=Math.Max(0,count()-capacity());
        if(!Mathf.Approximately(bar.ScrollBar.value,last)&&maximum>0)move((int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.RoundToInt(bar.ScrollBar.value*maximum), maximum, capacity(), RebirthScrollbarPagingPolicy.Enabled));
        int current=(int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(index(),0,maximum),maximum,capacity(),RebirthScrollbarPagingPolicy.Enabled);move(current);
        bar.ScrollBar.barSize=Mathf.Clamp01(capacity()/(float)Math.Max(1,count()));
        bar.ScrollBar.value=last=maximum==0?0:current/(float)maximum;
        bar.ScrollBar.alpha=maximum>0?1:0;
        foreach(var child in Children)child.ViewComponent.IsVisible=maximum>0;
    }
}
