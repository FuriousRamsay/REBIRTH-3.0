const fs=require('fs');const p='Scripts/Survivor/Progression/TheorySolo/RebirthTheorySoloLockpickService.cs';let s=fs.readFileSync(p,'utf8');function edit(a,b){if(!s.includes(a))throw Error(a);s=s.replace(a,b);}
edit('internal bool remote;internal bool finished;','internal bool remote;internal bool finished;internal bool applied;internal PlatformUserIdentifierAbs user;internal List<BlockChangeInfo> changes;');
edit('    private static readonly Dictionary<int,Binding> Bindings','    [ThreadStatic] private static Transition activeTransition;'+String.fromCharCode(10)+'    private static readonly Dictionary<int,Binding> Bindings');
edit('return matching!=null&&BeforeImage(b)?new Transition(b,package,true):null;','return matching!=null&&BeforeImage(b)?Open(b,package,true):null;');
edit('return new Transition(b,timer,false);','return Open(b,timer,false);');
edit('        var b=transition.binding;','        if(ReferenceEquals(activeTransition,transition))activeTransition=null;'+String.fromCharCode(10)+'        var b=transition.binding;');
edit('        if(!ran||!Current(b,true)||!b.Released)return;','        if(!ran||!transition.applied||!Current(b,true)||!b.Released)return;');
edit('    internal static void Unknown(Transition transition,Exception failure)'+String.fromCharCode(10)+'    {if(transition!=null&&failure!=null&&!transition.binding.Completed)transition.binding.Held=true;}',`    internal static void Unknown(Transition transition,Exception failure)
    {if(transition!=null&&failure!=null&&!transition.binding.Completed){transition.binding.Held=true;transition.finished=true;if(ReferenceEquals(activeTransition,transition))activeTransition=null;}}
    private static Transition Open(Binding b,object source,bool remote)
    {
        if(activeTransition!=null)return null;var t=new Transition(b,source,remote);
        if(remote){var package=(NetPackageSetBlock)source;t.user=package.persistentPlayerId;t.changes=package.blockChanges;}
        activeTransition=t;return t;
    }
    internal static Transition BeforeCommit(GameManager callbacks,PlatformUserIdentifierAbs user,List<BlockChangeInfo> changes)
    {
        var t=activeTransition;if(t==null||t.finished||t.applied||!ReferenceEquals(callbacks,GameManager.Instance)||!Current(t.binding,true)||!BeforeImage(t.binding)||changes==null||changes.Count<1||changes.Count>128)return null;
        if(t.remote&&(!ReferenceEquals(changes,t.changes)||!object.Equals(user,t.user)))return null;
        if(!t.remote&&(t.source is not TimerEventData timer||!ReferenceEquals(timer.Data,t.binding.Player)))return null;
        BlockChangeInfo matching=null;
        foreach(var row in changes){if(row==null)return null;if(row.blockValueRef.TryGetBlockPos(out var pos)&&pos.Equals(Position(t.binding))){if(matching!=null||!row.bChangeBlockValue||row.blockValue.rawData!=t.binding.Original.After||row.blockValue.damage!=t.binding.Original.AfterDamage)return null;matching=row;}}
        return matching==null?null:t;
    }
    internal static void AfterCommit(Transition transition,bool ran)
    {
        if(transition==null||!ran||!ReferenceEquals(activeTransition,transition)||transition.finished||!Current(transition.binding,true))return;
        var after=transition.binding.World.GetBlock(Position(transition.binding));
        // A queued/later change has no matching post-image here and cannot qualify this original callback.
        if(after.rawData==transition.binding.Original.After&&after.damage==transition.binding.Original.AfterDamage)transition.applied=true;
    }`);
edit('Bindings.Clear();currentWorld=world;','Bindings.Clear();activeTransition=null;currentWorld=world;');edit('Bindings.Clear();currentWorld=null;','Bindings.Clear();activeTransition=null;currentWorld=null;');
fs.writeFileSync(p,s);