using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// A detached original can only be obtained by rereading one of the two known
// domain journals. No transport-supplied image or caller-authored plan is an
// admission capability. Source custody still has to authorize native effects.
internal sealed class RebirthRemoteResourceNativeOriginal
{
    internal RebirthRemoteResourceSourceEffectCodec.Domain Domain { get; }
    internal Guid Operation { get; }
    internal string World { get; }
    internal string Owner { get; }
    internal string Creation { get; }
    internal int Deaths { get; }
    internal string Binding { get; }
    internal string SourceId { get; }
    internal string Incarnation { get; }
    internal RemoteResourceSourceKind Kind { get; }
    private ItemStack[] before,debited,after;
    private bool[] locks;
    private string[] terminalDigests;
    internal bool GraphRetired=>terminalDigests!=null;
    private RebirthRemoteResourceNativeOriginal(RebirthRemoteResourceSourceEffectCodec.Domain domain,Guid operation,string world,string owner,string creation,int deaths,
        string binding,string sourceId,string incarnation,RemoteResourceSourceKind kind,ItemStack[] before,ItemStack[] after,bool[] locks)
    {
        if(operation==Guid.Empty||(int)kind<(int)RemoteResourceSourceKind.StaticContainer||(int)kind>(int)RemoteResourceSourceKind.DroneStorage||before==null||after==null||locks==null||
            before.Length<1||before.Length>short.MaxValue||before.Length!=after.Length||before.Length!=locks.Length||
            !Guid.TryParseExact(incarnation,"N",out var id)||id==Guid.Empty||id.ToString("N")!=incarnation)throw new InvalidDataException();
        Domain=domain;Operation=operation;World=world;Owner=owner;Creation=creation;Deaths=deaths;Binding=binding;SourceId=sourceId;Incarnation=incarnation;Kind=kind;
        this.before=ItemStack.Clone(before);this.after=ItemStack.Clone(after);this.locks=(bool[])locks.Clone();debited=new ItemStack[before.Length];
        for(int index=0;index<before.Length;index++)
        {
            var old=before[index];var next=after[index];
            if(old==null||next==null||old.count<0||next.count<0||old.count>ushort.MaxValue||next.count>ushort.MaxValue)throw new InvalidDataException();
            bool same=!old.IsEmpty()&&!next.IsEmpty()&&RebirthNativeItemCodec.Encode(old.itemValue)==RebirthNativeItemCodec.Encode(next.itemValue);
            if(locks[index]&&!RebirthStationGridIngredients.IsSameStackSnapshot(old,next)&&
                !(domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics&&same&&next.count>=old.count))throw new InvalidDataException();
            debited[index]=same?new ItemStack(old.itemValue.Clone(),Math.Min(old.count,next.count)):ItemStack.Empty.Clone();
            if(debited[index].count==0)debited[index]=ItemStack.Empty.Clone();
            if(domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&!RebirthStationGridIngredients.IsSameStackSnapshot(debited[index],next))throw new InvalidDataException();
        }
    }
    internal ItemStack[] CopyStage(int stage)=>!GraphRetired?ItemStack.Clone(stage==0||stage==3?before:stage==1?debited:stage==2?after:throw new ArgumentOutOfRangeException(nameof(stage))):throw new InvalidOperationException("Archived terminal original has no replayable graph.");
    internal bool[] CopyLocks()=>!GraphRetired?(bool[])locks.Clone():throw new InvalidOperationException("Archived terminal original has no locks.");
    internal string StageDigest(int stage)=>GraphRetired?terminalDigests[stage]:RebirthRemoteResourceDurableJournal.Digest(SourceId+"|"+Incarnation+"|"+stage+"|"+
        Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(CopyStage(stage)))+"|"+new string(locks.Select(value=>value?'1':'0').ToArray()));
    internal bool MatchesTerminalArchive(RebirthRemoteResourcePlacedStationOriginal.TerminalArchive receipt)
    {
        return receipt!=null&&receipt.Current&&MatchesTerminalRoot(receipt.Root);
    }
    private bool MatchesTerminalRoot(RebirthRemoteResourceDurableJournal.Record terminal)
    {
        if(terminal==null||Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||terminal.Operation!=Operation||
            terminal.Binding!=Binding||terminal.World!=World||terminal.Owner!=Owner||terminal.Creation!=Creation)return false;
        if(GraphRetired)return true;
        var root=terminal.Copy();var source=root.Element("sources").Elements("source").SingleOrDefault(value=>(string)value.Attribute("id")==SourceId);
        return (int?)root.Attribute("deaths")==Deaths&&source!=null&&(int?)source.Attribute("kind")== (int)Kind&&
            (string)source.Attribute("incarnation")==Incarnation&&source.Element("before")?.Value==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(before))&&
            source.Element("after")?.Value==Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(after))&&
            source.Element("locks")?.Value==new string(locks.Select(value=>value?'1':'0').ToArray());
    }
    internal bool RetireTerminalGraph(RebirthRemoteResourcePlacedStationOriginal.TerminalArchive receipt)
    {
        if(!MatchesTerminalArchive(receipt))return false;if(GraphRetired)return true;
        // Only compact immutable effect witnesses survive. No ItemStack,
        // ItemValue, original array or replay capability remains on this object.
        var digests=new string[4];for(int stage=0;stage<4;stage++)digests[stage]=StageDigest(stage);
        terminalDigests=digests;before=null;debited=null;after=null;locks=null;return true;
    }
    internal bool MatchesNeverEntered(RebirthRemoteResourcePlacedStationOriginal.NeverEnteredTerminal receipt)=>receipt?.Current==true&&MatchesTerminalRoot(receipt.Root);
    internal bool RetireNeverEnteredGraph(RebirthRemoteResourcePlacedStationOriginal.NeverEnteredTerminal receipt)
    {
        if(!MatchesNeverEntered(receipt))return false;if(GraphRetired)return true;
        var digests=new string[4];for(int stage=0;stage<4;stage++)digests[stage]=StageDigest(stage);
        terminalDigests=digests;before=null;debited=null;after=null;locks=null;return true;
    }
    internal bool ActorCurrent(EntityPlayer actor)
    {
        if(GraphRetired)return false;
        // Named logistics continuation only: the actual original death journal,
        // native backpack marker and saved partition must prove old death+1.
        // This grants no new-life owner write and changes no source/range rule.
        if(ThreadManager.IsMainThread()&&Domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics&&actor?.world?.IsRemote()==false&&
            RebirthLogisticsTransferJournal.WorldKey(actor.world)==World&&RebirthLogisticsTransferJournal.TryRead(Owner,Operation,out var retained)&&
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)==Binding&&
            (RebirthLogisticsDeathSettlement.MatchesOriginalActor(actor,retained)||RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,retained)))return true;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||actor.Died!=Deaths||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            !RebirthOriginalActionScope.Matches(actor,Owner,Creation))return false;
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics)
            return RebirthLogisticsTransferJournal.WorldKey(actor.world)==World&&RebirthLogisticsTransferJournal.TryRead(Owner,Operation,out var livingOriginal)&&
                RebirthLogisticsTransferJournal.ImmutablePlanDigest(livingOriginal)==Binding&&!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(livingOriginal);
        return RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&world==World;
    }
    internal bool SameOriginal(RebirthRemoteResourceNativeOriginal other)=>other!=null&&other.Domain==Domain&&other.Operation==Operation&&other.Binding==Binding&&
        other.World==World&&other.Owner==Owner&&other.Creation==Creation&&other.Deaths==Deaths&&other.SourceId==SourceId&&other.Kind==Kind&&other.Incarnation==Incarnation;
    internal bool Current(out RebirthRemoteResourceNativeOriginal live)
    {live=null;return !GraphRetired&&TryRead(Domain,Owner,Operation,SourceId,out live)&&SameOriginal(live);}
    internal int AttemptedStage()
    {
        try
        {
            if(!Current(out _))return -1;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics)
                return RebirthLogisticsTransferJournal.TryRead(Owner,Operation,out var original)?original.Legs.Single(value=>value.Id==SourceId).Attempted:-1;
            return RebirthRemoteResourceDurableJournal.TryRead(Owner,Operation,out var fetch,out _)&&fetch!=null?
                (int)fetch.Copy().Element("sources").Elements("source").Single(value=>(string)value.Attribute("id")==SourceId).Attribute("attempted"):-1;
        }
        catch{return -1;}
    }

    internal bool MayApply(int stage)
    {
        try
        {
            if(!Current(out _))return false;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics)
            {
                if(!RebirthLogisticsTransferJournal.TryRead(Owner,Operation,out var original)||original.Phase!=1||original.Outcome!=0||stage!=3&&original.Rejecting||string.IsNullOrEmpty(original.OwnerDebitProof)||
                    stage!=3&&original.Legs.Any(leg=>leg.Restored))return false;
                var leg=original.Legs.Single(value=>value.Id==SourceId);
                if(stage==1)return leg.Attempted==1&&leg.Published==0;
                if(stage==2)return leg.Attempted==2&&leg.Published==1&&original.Legs.All(value=>value.Published>=1)&&original.Npcs.All(value=>value.Published==2)&&
                    original.Legs.Where(value=>value.Kind==RemoteResourceSourceKind.NpcStorage).All(value=>value.Published==2);
                if(stage==3)return original.Legs.All(value=>value.Attempted<=1&&(value.Kind!=RemoteResourceSourceKind.NpcStorage||value.Attempted==0))&&original.Npcs.All(value=>value.Attempted==0);
                return false;
            }
            if(!RebirthRemoteResourceDurableJournal.TryRead(Owner,Operation,out var fetch,out _)||fetch==null||fetch.Phase!=1)return false;
            var root=fetch.Copy();var sources=root.Element("sources").Elements("source").ToArray();var source=sources.Single(value=>(string)value.Attribute("id")==SourceId);
            bool aborting=((int?)root.Attribute("aborting")??0)==1;
            if(stage==3)return aborting&&sources.All(value=>(int)value.Attribute("kind")!=5||(int)value.Attribute("attempted")==0)&&
                sources.All(value=>(int)value.Attribute("attempted")<=1);
            return stage==1&&!aborting&&((int?)source.Attribute("restored")??0)==0&&(int)source.Attribute("attempted")==1&&(int)source.Attribute("published")==0&&!string.IsNullOrEmpty((string)source.Attribute("beforeProof"));
        }
        catch{return false;}
    }
    internal bool SavedTerminal()
    {
        if(GraphRetired)return true;
        if(!Current(out _))return false;
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics)return RebirthLogisticsTransferJournal.TryRead(Owner,Operation,out var original)&&original.Phase==4;
        return RebirthRemoteResourceDurableJournal.TryRead(Owner,Operation,out var fetch,out _)&&fetch?.Phase==4;
    }
    internal static bool TryRead(RebirthRemoteResourceSourceEffectCodec.Domain domain,string owner,Guid operation,string sourceId,out RebirthRemoteResourceNativeOriginal result)
    {
        result=null;
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
            if(domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics)
            {
                if(!RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)||original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World))return false;
                var leg=original.Legs.SingleOrDefault(value=>value.Id==sourceId);if(leg==null||leg.Kind==RemoteResourceSourceKind.NpcStorage)return false;
                result=new RebirthRemoteResourceNativeOriginal(domain,operation,original.World,original.Owner,original.Creation,original.Deaths,
                    RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),leg.Id,leg.NativeIdentity,leg.Kind,leg.Before,leg.After,leg.Locks);return true;
            }
            if(domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||!RemoteResourceWorldIdentity.TryGet(GameManager.Instance.World,out string world)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner,operation,out var fetch,out _)||fetch==null||fetch.World!=world)return false;
            var root=fetch.Copy();var source=root.Element("sources").Elements("source").SingleOrDefault(value=>(string)value.Attribute("id")==sourceId);
            if(source==null||(int?)source.Attribute("kind") is not int kind||kind<1||kind>4||source.Element("npc")!=null)return false;
            ItemStack[] Image(string name)=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element(name)?.Value??throw new InvalidDataException()));
            var before=Image("before");var after=Image("after");var paid=Image("paid");string lockText=source.Element("locks")?.Value??throw new InvalidDataException();
            if(lockText.Length!=before.Length||lockText.Any(value=>value!='0'&&value!='1'))return false;
            var value=new RebirthRemoteResourceNativeOriginal(domain,operation,fetch.World,fetch.Owner,fetch.Creation,(int)root.Attribute("deaths"),fetch.Binding,
                sourceId,(string)source.Attribute("incarnation"),(RemoteResourceSourceKind)kind,before,after,lockText.Select(bit=>bit=='1').ToArray());
            var totals=new Dictionary<string,long>(StringComparer.Ordinal);
            void Add(IEnumerable<ItemStack> items,int sign)
            {foreach(var stack in items){if(stack==null||stack.count<0)throw new InvalidDataException();if(stack.IsEmpty())continue;string key=RebirthNativeItemCodec.Encode(stack.itemValue);totals.TryGetValue(key,out long count);totals[key]=checked(count+(long)sign*stack.count);}}
            Add(before,-1);Add(after,1);Add(paid,1);if(totals.Values.Any(count=>count!=0)||!paid.Any(stack=>!stack.IsEmpty()))return false;result=value;return true;
        }
        catch{result=null;return false;}
    }
}
