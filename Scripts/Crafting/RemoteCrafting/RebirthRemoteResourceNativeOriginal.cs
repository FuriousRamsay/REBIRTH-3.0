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
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork)
        {
            var server=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||
                !ReferenceEquals(actor.world,server)||!ReferenceEquals(server.GetEntity(actor.entityId),actor)||
                actor.Died!=Deaths||RebirthLogisticsTransferJournal.WorldKey(server)!=World||
                !RebirthOriginalActionScope.Matches(actor,Owner,Creation)||
                !RebirthNpcWorkNativeTransferOriginal.TryRead(Owner,Operation,SourceId,out var work)||
                work.Binding!=Binding||work.Phase>=4||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(work.Npc,out int entity)||
                server.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
                npc.RebirthRuntimeState?.OwnerId!=RebirthStablePlayerIdentityOwner(actor))return false;
            return true;
        }
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit)
        {
            var server=GameManager.Instance?.World;
            return ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&!actor.IsDead()&&
                ReferenceEquals(actor.world,server)&&ReferenceEquals(server.GetEntity(actor.entityId),actor)&&
                actor.Died==Deaths&&RebirthLogisticsTransferJournal.WorldKey(server)==World&&
                RebirthOriginalActionScope.Matches(actor,Owner,Creation)&&
                RebirthNpcWorkNativeCreditOriginal.TryRead(Owner,Operation,SourceId,out var credit)&&
                credit.Binding==Binding&&credit.Phase<6&&
                RebirthNpcRuntimeRegistry.TryGetEntityId(credit.Npc,out int entity)&&
                server.GetEntity(entity) is EntityRebirthHumanoidNPC npc&&!npc.IsDead()&&
                npc.RebirthRuntimeState?.OwnerId==RebirthStablePlayerIdentityOwner(actor);
        }
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting)
        {
            var server=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||
                !ReferenceEquals(actor.world,server)||!ReferenceEquals(server.GetEntity(actor.entityId),actor)||
                actor.Died!=Deaths||RebirthLogisticsTransferJournal.WorldKey(server)!=World||
                !RebirthOriginalActionScope.Matches(actor,Owner,Creation)||
                !RebirthSmartLootingHumanOriginal.TryRead(Owner,Operation,SourceId,out var loot)||
                loot.Binding!=Binding||loot.Phase>=4||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(loot.Npc,out int entity)||
                server.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
                npc.RebirthRuntimeState?.OwnerId!=RebirthStablePlayerIdentityOwner(actor))return false;
            return true;
        }
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)
        {
            // A dog leg has no NPC entity: the row is an aggregate record and the credit an aggregate
            // publication. The owner may have respawned since the row was saved (Died only grows); the
            // saved row keeps its own owner/creation scope check.
            var server=GameManager.Instance?.World;
            return ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&!actor.IsDead()&&
                ReferenceEquals(actor.world,server)&&ReferenceEquals(server.GetEntity(actor.entityId),actor)&&
                actor.Died>=Deaths&&RebirthLogisticsTransferJournal.WorldKey(server)==World&&
                RebirthOriginalActionScope.Matches(actor,Owner,Creation)&&
                RebirthDogSmartLootOriginal.TryRead(Owner,Operation,SourceId,out var dogLoot)&&
                dogLoot.Binding==Binding&&dogLoot.Phase<4;
        }
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
    private static string RebirthStablePlayerIdentityOwner(EntityPlayer actor)=>
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var id)?id.CanonicalId:null;
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
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork)
                return RebirthNpcWorkNativeTransferOriginal.TryRead(Owner,Operation,SourceId,out var work)?work.Attempted:-1;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit)
                return RebirthNpcWorkNativeCreditOriginal.TryRead(Owner,Operation,SourceId,out var credit)?credit.Attempted:-1;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting)
                return RebirthSmartLootingHumanOriginal.TryRead(Owner,Operation,SourceId,out var loot)?loot.Attempted:-1;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)
                return RebirthDogSmartLootOriginal.TryRead(Owner,Operation,SourceId,out var dogLoot)?dogLoot.Attempted:-1;
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
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork)
                return stage==1&&RebirthNpcWorkNativeTransferOriginal.TryRead(Owner,Operation,SourceId,out var work)&&
                    work.Phase==1&&work.Attempted==1&&work.Published==0;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit)
                return RebirthNpcWorkNativeCreditOriginal.TryRead(Owner,Operation,SourceId,out var credit)&&
                    (stage==1&&credit.Phase==1&&credit.Attempted==1&&credit.Published==0||
                     stage==2&&credit.Phase==3&&credit.Attempted==2&&credit.Published==1);
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting)
                return stage==1&&RebirthSmartLootingHumanOriginal.TryRead(Owner,Operation,SourceId,out var loot)&&
                    loot.Phase==1&&loot.Attempted==1&&loot.Published==0;
            if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)
                return stage==1&&RebirthDogSmartLootOriginal.TryRead(Owner,Operation,SourceId,out var dogLoot)&&
                    dogLoot.Phase==1&&dogLoot.Attempted==1&&dogLoot.Published==0;
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
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork)
            return RebirthNpcWorkNativeTransferOriginal.TryRead(Owner,Operation,SourceId,out var work)&&work.Phase==4;
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit)
            return RebirthNpcWorkNativeCreditOriginal.TryRead(Owner,Operation,SourceId,out var credit)&&credit.Phase==6;
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting)
            return RebirthSmartLootingHumanOriginal.TryRead(Owner,Operation,SourceId,out var loot)&&loot.Phase==4;
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)
            return RebirthDogSmartLootOriginal.TryRead(Owner,Operation,SourceId,out var dogLoot)&&dogLoot.Phase==4;
        if(Domain==RebirthRemoteResourceSourceEffectCodec.Domain.Logistics)return RebirthLogisticsTransferJournal.TryRead(Owner,Operation,out var original)&&original.Phase==4;
        return RebirthRemoteResourceDurableJournal.TryRead(Owner,Operation,out var fetch,out _)&&fetch?.Phase==4;
    }
    internal static bool TryRead(RebirthRemoteResourceSourceEffectCodec.Domain domain,string owner,Guid operation,string sourceId,out RebirthRemoteResourceNativeOriginal result)
    {
        result=null;
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
            if(domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork)
            {
                if(!RebirthNpcWorkNativeTransferOriginal.TryRead(owner,operation,sourceId,out var work)||
                    work.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World))return false;
                result=new RebirthRemoteResourceNativeOriginal(domain,operation,work.World,work.Owner,work.Creation,work.Deaths,
                    work.Binding,work.Source,work.Incarnation,RemoteResourceSourceKind.StaticContainer,work.Before,work.After,work.Locks);
                return true;
            }
            if(domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit)
            {
                if(!RebirthNpcWorkNativeCreditOriginal.TryRead(owner,operation,sourceId,out var credit)||
                    credit.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World))return false;
                result=new RebirthRemoteResourceNativeOriginal(domain,operation,credit.World,credit.Owner,
                    credit.Creation,credit.Deaths,credit.Binding,credit.Destination,credit.Incarnation,
                    RemoteResourceSourceKind.StaticContainer,credit.Before,credit.After,credit.Locks);
                return true;
            }
            if(domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting)
            {
                if(!RebirthSmartLootingHumanOriginal.TryRead(owner,operation,sourceId,out var loot)||
                    loot.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World))return false;
                result=new RebirthRemoteResourceNativeOriginal(domain,operation,loot.World,loot.Owner,loot.Creation,loot.Deaths,
                    loot.Binding,sourceId,loot.SourceIncarnation,RemoteResourceSourceKind.StaticContainer,
                    loot.SourceBefore,loot.SourceAfter,loot.SourceLocks);
                return true;
            }
            if(domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)
            {
                if(!RebirthDogSmartLootOriginal.TryRead(owner,operation,sourceId,out var dogLoot)||
                    dogLoot.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World))return false;
                result=new RebirthRemoteResourceNativeOriginal(domain,operation,dogLoot.World,dogLoot.Owner,dogLoot.Creation,dogLoot.Deaths,
                    dogLoot.Binding,sourceId,dogLoot.SourceIncarnation,RemoteResourceSourceKind.StaticContainer,
                    dogLoot.SourceBefore,dogLoot.SourceAfter,dogLoot.SourceLocks);
                return true;
            }
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
