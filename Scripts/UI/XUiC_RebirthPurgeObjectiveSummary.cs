using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthPurgeObjectiveSummary:XUiController
{
    private object previous;
    private int previousTarget=-1;
    private float next;
    private string title="",details="";
    public override void OnOpen(){base.OnOpen();previous=null;previousTarget=-1;next=0;Refresh();}
    public override void Update(float dt)
    {
        base.Update(dt);
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.5f;Refresh();
    }
    private void Refresh()
    {
        bool visible=RebirthPurgeReleasePolicy.Enabled && RebirthSandboxOptionManager.Current.IsPurge;
        if(ViewComponent!=null)ViewComponent.IsVisible=visible;
        if(!visible)return;
        object source=null;int target=75;RebirthPurgeObjectiveFrame.Biome[] biomes=null;
        var world=GameManager.Instance?.World;
        if(world!=null && world.IsRemote())
        {
            var frame=RebirthPoiMapSync.LocalObjectives;
            if(frame!=null && frame.Known){source=frame;target=frame.TargetPercentage;if(!ReferenceEquals(previous,source) || previousTarget!=target)biomes=frame.Biomes.Values.ToArray();}
        }
        else
        {
            RebirthPoiWorldStore store;var progress=RebirthPurgeObjectiveProgress.Instance;
            if(RebirthPoiWorldLifecycle.Instance.TryGetStore(out store) && store.Published!=null && progress.Published!=null && progress.WorldId==store.Published.WorldId && progress.Revision==store.Published.Revision)
            {
                source=progress.Published;target=progress.TargetPercentage;
                if(!ReferenceEquals(previous,source) || previousTarget!=target)biomes=progress.Published.Values.Select(b=>new RebirthPurgeObjectiveFrame.Biome(b.Biome,b.Eligible,b.Discovered,b.Cleared,new System.Collections.Generic.Dictionary<int,int>(b.EligibleByTier),new System.Collections.Generic.Dictionary<int,int>(b.ClearedByTier))).ToArray();
            }
        }
        if(ReferenceEquals(previous,source) && previousTarget==target)return;
        previous=source;previousTarget=target;
        title=source==null?Localization.Get("xuiRebirthPurgeObjectivePending"):string.Format(Localization.Get("xuiRebirthPurgeObjectiveHeader"),target);
        details=source==null?Localization.Get("xuiRebirthPurgeObjectivePendingDesc"):RebirthPurgeObjectiveText.Format(biomes,target,key=>Localization.Get(key));
        RefreshBindings();
    }
    public override bool GetBindingValueInternal(ref string value,string bindingName)
    {
        if(bindingName=="purgeobjectivetitle"){value=title;return true;}
        if(bindingName=="purgeobjectivedetails"){value=details;return true;}
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}