using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// A cooking return is a new deposit into the current companion inventory.
// The adapter retained by withdrawal is identity, never a writable postimage.
internal static partial class RebirthCookingNpcReturn
{
    internal readonly struct Result
    {
        internal readonly int Count;internal readonly bool Pending;
        internal Result(int count,bool pending){Count=count;Pending=pending;}
    }
    internal sealed class Attempt
    {
        internal Guid Operation;internal EntityPlayer Actor;internal World World;internal RemoteNpcResourceSource Source;
        internal string Owner,Creation,Save,NativeWorld,Intent,Directory;internal int Deaths,Count;
        internal uint Revision;internal ItemStack[] DogBefore,DogAfter;internal bool[] Locks;
        internal int DogSlot=-1;
        internal ulong Recovery;
        internal RebirthNpcInventorySnapshot LogicalBefore,LogicalAfter;
        internal IDisposable Memory;internal bool Invoked,Saved,Retired,Completed;internal OutcomeState State;
    }
    // Kept separate from NPC transaction enum values: only an observed owning
    // call and full readback can issue one of these states.
    internal enum OutcomeState { Refused, Published, Unknown }
    private static readonly List<Attempt> Active=new List<Attempt>();
    private const int MaximumJournal=4*1024*1024;

    private static bool Current(Attempt attempt)=>attempt!=null&&!attempt.Retired&&ThreadManager.IsMainThread()&&
        ReferenceEquals(GameManager.Instance?.World,attempt.World)&&attempt.World?.IsRemote()==false&&
        ReferenceEquals(attempt.Actor.world,attempt.World)&&ReferenceEquals(attempt.World.GetPrimaryPlayer(),attempt.Actor)&&
        ReferenceEquals(attempt.World.GetEntity(attempt.Actor.entityId),attempt.Actor)&&!attempt.Actor.IsDead()&&attempt.Actor.Died==attempt.Deaths&&
        RebirthCamperCharacterAccess.CurrentCreation(attempt.Actor)==attempt.Creation&&attempt.World.worldState.Guid==attempt.NativeWorld&&
        RebirthNpcRecoveryGeneration.RequireCurrent()==attempt.Recovery&&
        Path.GetFullPath(GameIO.GetSaveGameDir())==attempt.Save&&RebirthStablePlayerIdentity.TryResolveServerEntity(attempt.Actor,out var owner)&&owner.CanonicalId==attempt.Owner&&
        ReferenceEquals(attempt.Source.NativeActor.world,attempt.World)&&ReferenceEquals(attempt.World.GetEntity(attempt.Source.NativeActor.entityId),attempt.Source.NativeActor)&&
        attempt.Source.NativeActor.RebirthRuntimeState?.StableId==attempt.Source.NativeIdentity&&!attempt.Source.NativeActor.IsDead();

    internal static Result Restore(EntityPlayer actor,RemoteNpcResourceSource source,ItemStack item,int slot,int requested,ref Attempt retained)
        =>Restore(actor,source,item,slot,requested,ref retained,Guid.Empty,null);
    private static Result Restore(EntityPlayer actor,RemoteNpcResourceSource source,ItemStack item,int slot,int requested,ref Attempt retained,Guid boundOperation,uint? boundRevision)
    {
        if(retained!=null)
        {
            if(!ReferenceEquals(retained.Actor,actor)||!ReferenceEquals(retained.Source,source)||requested<retained.Count)return new Result(0,true);
            return Finish(retained);
        }
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||source==null||item==null||item.IsEmpty()||requested<1)return new Result(0,false);
        RetireReplaced();
        if(Active.Count>=16||!source.IsLoaded||source.IsBusy||!source.IsAuthorized(actor,false,out _)||
            RebirthNpcMetabolismCustody.Blocks(source.NativeIdentity)||RemoteResourceTransactions.ActionBlocksNpc(source.NativeIdentity)||
            Active.Any(original=>!original.Completed&&!original.Retired&&ReferenceEquals(original.World,actor.world)&&original.Source.NativeIdentity==source.NativeIdentity)||
            RebirthRemoteResourceSourceCustody.BlocksSource(source)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return new Result(0,false);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return new Result(0,false);
        Attempt attempt=null;
        try
        {
            attempt=new Attempt{Operation=boundOperation==Guid.Empty?Guid.NewGuid():boundOperation,Actor=actor,World=actor.world,Source=source,Owner=owner.CanonicalId,
                Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),
                NativeWorld=actor.world.worldState.Guid,Memory=memory,Recovery=RebirthNpcRecoveryGeneration.RequireCurrent(),State=OutcomeState.Refused};
            if(string.IsNullOrEmpty(attempt.Creation)||!Current(attempt))return new Result(0,false);
            attempt.Directory=Path.Combine(attempt.Save,"RebirthData","CookingIngredientReturns");
            string key=null;
            if(source.IsDogStorage)
            {
                var fresh=new RemoteNpcResourceSource(source.NativeActor);attempt.Revision=fresh.OriginalRevision;
                if(!fresh.IsDogStorage||fresh.NativeIdentity!=source.NativeIdentity||attempt.Revision==uint.MaxValue||slot<0||slot>=fresh.Slots.Length||fresh.SlotLocks[slot])return new Result(0,false);
                attempt.DogSlot=slot;
                attempt.Locks=Enumerable.Range(0,fresh.Slots.Length).Select(index=>fresh.SlotLocks[index]).ToArray();
                var live=fresh.Slots[slot];
                if(!live.IsEmpty()&&!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(live.itemValue.Clone(),1),new ItemStack(item.itemValue.Clone(),1)))return new Result(0,false);
                attempt.Count=Math.Min(requested,Math.Max(0,item.itemValue.ItemClass.Stacknumber.Value-(live.IsEmpty()?0:live.count)));
                if(boundRevision.HasValue&&(attempt.Revision!=boundRevision.Value||attempt.Count!=requested))return new Result(0,false);
                if(attempt.Count<1)return new Result(0,false);
                var after=live.IsEmpty()?new ItemStack(item.itemValue.Clone(),attempt.Count):live.Clone();if(!live.IsEmpty())after.count+=attempt.Count;
                var desired=ItemStack.Clone(fresh.Slots);desired[slot]=after;
                long remaining=2L*1024*1024;
                byte[] beforeImage=RemoteResourceTransactions.CaptureOriginalNativeFrame(fresh.Slots,ref remaining),afterImage=RemoteResourceTransactions.CaptureOriginalNativeFrame(desired,ref remaining);
                long graphBefore=RemoteResourceTransactions.MeasureOriginalNativeFrame(beforeImage),graphAfter=RemoteResourceTransactions.MeasureOriginalNativeFrame(afterImage);
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(attempt.Memory,checked(64L*1024*1024+graphBefore+2*graphAfter+4L*(beforeImage.Length+afterImage.Length))))return new Result(0,false);
                attempt.DogBefore=RebirthCamperInventorySnapshot.Restore(beforeImage);attempt.DogAfter=RebirthCamperInventorySnapshot.Restore(afterImage);
                // The setter argument is hydrated separately from the expected
                // image, including mutable native metadata and cosmetic arrays.
                var argument=RebirthCamperInventorySnapshot.Restore(afterImage)[slot];
                attempt.Intent=Intent(attempt,item,slot);
                retained=attempt;Active.Add(attempt);memory=null;
                if(!Write(attempt,"intent",attempt.Intent))return new Result(0,true);
                if(!Current(attempt)||!SameDog(RebirthDogInventoryService.GetSnapshot(source.NativeIdentity),attempt.DogBefore,attempt.Locks,attempt.Revision))return Finish(attempt);
                fresh.SetSlot(slot,argument);attempt.Invoked=true;attempt.State=OutcomeState.Unknown;
                if(dogMutation!=null)throw new InvalidOperationException("Nested cooking dog return mutation");
                dogMutation=attempt;
                try{fresh.MarkModifiedOriginal(attempt.Operation);}
                finally{dogMutation=null;}
                var observed=fresh.LastCommitEvent;
                if(observed!=null&&ReferenceEquals(observed.Source,fresh)&&observed.Mutation==attempt.Operation&&observed.BeforeRevision==attempt.Revision)
                {
                    if(observed.Disposition==RemoteNpcResourceSource.CommitDisposition.Published&&observed.AfterRevision==attempt.Revision+1&&
                        SameDog(RebirthDogInventoryService.GetSnapshot(source.NativeIdentity),attempt.DogAfter,attempt.Locks,attempt.Revision+1))attempt.State=OutcomeState.Published;
                    else if(observed.Disposition==RemoteNpcResourceSource.CommitDisposition.Refused&&
                        SameDog(RebirthDogInventoryService.GetSnapshot(source.NativeIdentity),attempt.DogBefore,attempt.Locks,attempt.Revision))attempt.State=OutcomeState.Refused;
                }
            }
            else
            {
                // A logical snapshot may contain no row after a full withdrawal.
                // Deposit by item key against its fresh revision, not old slot.
                key=item.itemValue.ItemClass.GetItemName();var baseline=ItemClass.GetItem(key,false);
                if(baseline==null||!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(baseline,1),new ItemStack(item.itemValue.Clone(),1)))return new Result(0,false);
                var before=RebirthNpcInventoryTransactionService.GetSnapshot(source.NativeIdentity);if(before==null||before.Revision==uint.MaxValue)return new Result(0,false);
                before.Quantities.TryGetValue(key,out int existing);
                if(existing<0||(long)existing+requested>int.MaxValue||RebirthCompanionInventoryLockService.IsNpcItemLocked(source.NativeIdentity,key))return new Result(0,false);
                attempt.Revision=before.Revision;attempt.Count=requested;attempt.LogicalBefore=before;
                if(boundRevision.HasValue&&attempt.Revision!=boundRevision.Value)return new Result(0,false);
                var quantities=new Dictionary<string,int>(before.Quantities,StringComparer.OrdinalIgnoreCase);quantities[key]=existing+requested;
                attempt.LogicalAfter=new RebirthNpcInventorySnapshot{NpcId=before.NpcId,Revision=before.Revision+1,Quantities=quantities,
                    Reservations=new Dictionary<string,int>(before.Reservations,StringComparer.OrdinalIgnoreCase),NativeStacks=before.NativeStacks?.WithRevision(before.Revision+1),
                    LogisticsEffects=new Dictionary<Guid,string>(before.LogisticsEffects)};
                attempt.Intent=Intent(attempt,item,slot);
                retained=attempt;Active.Add(attempt);memory=null;
                if(!Write(attempt,"intent",attempt.Intent))return new Result(0,true);
                if(!Current(attempt)||!RemoteResourceTransactions.ActionSameNpc(before,RebirthNpcInventoryTransactionService.GetSnapshot(source.NativeIdentity)))return Finish(attempt);
                using(var lease=RebirthNpcInventoryAuthorityService.Issue("cooking.ingredient.return",RebirthNpcInventoryAuthorityOperations.Mutate,TimeSpan.FromSeconds(10),source.NativeIdentity))
                {
                    attempt.Invoked=true;attempt.State=OutcomeState.Unknown;
                    var result=RebirthNpcInventoryTransactionService.Apply(new RebirthNpcInventoryTransaction(attempt.Operation,source.NativeIdentity,attempt.Revision,lease.AuthorityKey,
                        new[]{new RebirthNpcInventoryMutation(key,attempt.Count)}),out uint revision);
                    var after=RebirthNpcInventoryTransactionService.GetSnapshot(source.NativeIdentity);
                    if((result==RebirthNpcInventoryTransactionResult.Applied||result==RebirthNpcInventoryTransactionResult.Replayed)&&revision==attempt.Revision+1&&RemoteResourceTransactions.ActionSameNpc(after,attempt.LogicalAfter))attempt.State=OutcomeState.Published;
                    else if(Enum.IsDefined(typeof(RebirthNpcInventoryTransactionResult),result)&&result!=RebirthNpcInventoryTransactionResult.Applied&&result!=RebirthNpcInventoryTransactionResult.Replayed&&
                        revision==attempt.Revision&&RemoteResourceTransactions.ActionSameNpc(after,before))attempt.State=OutcomeState.Refused;
                }
            }
            return Finish(attempt);
        }
        catch(Exception error)
        {
            if(attempt?.Invoked==true)attempt.State=OutcomeState.Unknown;
            RemoteResourceDiagnostics.Write("Cooking companion return retained: "+error.GetType().Name);
            return new Result(0,retained!=null);
        }
        finally{memory?.Dispose();}
    }

    private static Result Finish(Attempt attempt)
    {
        if(attempt.Completed)return new Result(0,false);
        if(!Current(attempt))return new Result(0,true);
        try
        {
            if(attempt.State==OutcomeState.Unknown)
            {
                // A positive saved original receipt can settle a lost return.
                // Neither absence nor an equal unmarked inventory permits it.
                if(!ObserveSaved(attempt))return new Result(0,true);
                attempt.State=OutcomeState.Published;attempt.Saved=true;
            }
            if(attempt.State==OutcomeState.Published&&!attempt.Saved)
            {
                if(!ObserveSaved(attempt))return new Result(0,true);
                attempt.Saved=true;
            }
            string stage=attempt.State==OutcomeState.Published?"published":"refused";
            var image=new XElement("cookingIngredientReturnOutcome",new XAttribute("version",1),new XAttribute("operation",attempt.Operation.ToString("N")),
                new XAttribute("stage",stage),new XAttribute("invoked",attempt.Invoked),new XAttribute("intent",RebirthRemoteResourceDurableJournal.Digest(attempt.Intent)));
            if(!Write(attempt,stage,image.ToString(SaveOptions.DisableFormatting)))return new Result(0,true);
            int count=attempt.State==OutcomeState.Published?attempt.Count:0;
            attempt.Completed=true;Active.Remove(attempt);attempt.Memory?.Dispose();attempt.Memory=null;
            attempt.DogBefore=attempt.DogAfter=null;attempt.LogicalBefore=attempt.LogicalAfter=null;attempt.Locks=null;attempt.Intent=null;
            return new Result(count,false);
        }
        catch(Exception error){RemoteResourceDiagnostics.Write("Cooking return publication retained: "+error.GetType().Name);return new Result(0,true);}
    }
    private static bool ObserveSaved(Attempt attempt)
    {
        if(!attempt.Invoked||!Current(attempt))return false;
        if(attempt.Source.IsDogStorage)
        {
            RebirthNpcAggregatePersistenceStore.Save();
            return Current(attempt)&&RebirthNpcAggregatePersistenceStore.ObserveSavedLogisticsDog(attempt.Source.NativeIdentity,saved=>HasSavedDogReceipt(attempt,saved)&&Current(attempt));
        }
        RebirthNpcInventoryPersistenceStore.Save();
        return Current(attempt)&&RebirthNpcInventoryPersistenceStore.ObserveSavedTransferSnapshot(attempt.Source.NativeIdentity,logical=>
            logical.ReplayJournal?.Contains(attempt.Operation)==true&&logical.Revision==attempt.LogicalAfter.Revision&&
            Equal(logical.Quantities,attempt.LogicalAfter.Quantities)&&Equal(logical.Reservations,attempt.LogicalAfter.Reservations)&&
            Equal(logical.LogisticsEffects,attempt.LogicalAfter.LogisticsEffects)&&XNode.DeepEquals(logical.NativeStacks?.Write(),attempt.LogicalAfter.NativeStacks?.Write())&&Current(attempt));
    }
    private static bool Equal<K,V>(IDictionary<K,V> left,IDictionary<K,V> right)=>left==null?right==null:right!=null&&left.Count==right.Count&&left.All(pair=>right.TryGetValue(pair.Key,out var value)&&EqualityComparer<V>.Default.Equals(pair.Value,value));
    private static bool SameDog(RebirthDogInventorySnapshot actual,ItemStack[] expected,bool[] locks,uint revision)=>actual!=null&&actual.Revision==revision&&actual.Slots.Length==expected.Length&&actual.Locks.Length==locks.Length&&
        expected.Select((stack,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(stack,actual.Slots[index])&&actual.Locks[index]==locks[index]).All(value=>value);
    private static XElement Logical(RebirthNpcInventorySnapshot snapshot)=>snapshot==null?null:new XElement("logical",new XAttribute("revision",snapshot.Revision),
        new XElement("quantities",snapshot.Quantities.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("item",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value)))),
        new XElement("reservations",snapshot.Reservations.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("item",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value)))),
        snapshot.NativeStacks?.Write(),new XElement("effects",snapshot.LogisticsEffects.OrderBy(pair=>pair.Key).Select(pair=>new XElement("effect",new XAttribute("operation",pair.Key),pair.Value))));
    private static string Intent(Attempt attempt,ItemStack item,int slot)
    {
        var image=new XElement("cookingIngredientReturn",new XAttribute("version",1),new XAttribute("operation",attempt.Operation.ToString("N")),
            new XAttribute("world",attempt.NativeWorld),new XAttribute("save",attempt.Save),new XAttribute("owner",attempt.Owner),new XAttribute("creation",attempt.Creation),new XAttribute("deaths",attempt.Deaths),
            new XAttribute("npc",attempt.Source.NativeIdentity),new XAttribute("dog",attempt.Source.IsDogStorage),new XAttribute("revision",attempt.Revision),new XAttribute("slot",slot),new XAttribute("count",attempt.Count),new XAttribute("recovery",attempt.Recovery),
            new XElement("item",RebirthNativeItemCodec.Encode(item.itemValue)),
            attempt.Source.IsDogStorage?new XElement("dogImages",new XElement("before",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(attempt.DogBefore))),
                new XElement("after",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(attempt.DogAfter))),new XElement("locks",string.Concat(attempt.Locks.Select(value=>value?'1':'0')))):
                new XElement("logicalImages",new XElement("before",Logical(attempt.LogicalBefore)),new XElement("after",Logical(attempt.LogicalAfter))));
        string text=image.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(text)>MaximumJournal)throw new InvalidDataException("Cooking return image exceeds bound");return text;
    }
    private static void PathCheck(string path)
    {if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Cooking return path is a reparse point");for(var directory=new DirectoryInfo(Path.GetFullPath(path));directory!=null;directory=directory.Parent)if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Cooking return path is a reparse point");}
    private static bool Write(Attempt attempt,string stage,string text)
    {
        if(!Current(attempt)||text==null)return false;
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(text);if(bytes.Length<1||bytes.Length>MaximumJournal)return false;
        PathCheck(attempt.Directory);Directory.CreateDirectory(attempt.Directory);
        string path=Path.Combine(attempt.Directory,attempt.Operation.ToString("N")+"."+stage+".xml");
        if(File.Exists(path))return ReadExact(path,bytes);
        string temporary=path+".tmp";if(File.Exists(temporary))return false;
        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!ReadExact(temporary,bytes))return false;
        File.Move(temporary,path);return ReadExact(path,bytes)&&Current(attempt);
    }
    private static bool ReadExact(string path,byte[] expected)
    {
        PathCheck(path);if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(input.Length!=expected.Length)return false;var buffer=new byte[32768];int offset=0;while(offset<expected.Length){int read=input.Read(buffer,0,Math.Min(buffer.Length,expected.Length-offset));if(read<1)return false;for(int index=0;index<read;index++)if(buffer[index]!=expected[offset+index])return false;offset+=read;}return input.ReadByte()==-1;}
    }
    private static void RetireReplaced()
    {
        foreach(var attempt in Active.ToArray())
            if(!ReferenceEquals(GameManager.Instance?.World,attempt.World)||!ReferenceEquals(attempt.World.GetEntity(attempt.Actor.entityId),attempt.Actor)||attempt.Actor.Died!=attempt.Deaths||RebirthCamperCharacterAccess.CurrentCreation(attempt.Actor)!=attempt.Creation||RebirthNpcRecoveryGeneration.RequireCurrent()!=attempt.Recovery)
            {attempt.Retired=true;Active.Remove(attempt);attempt.Memory?.Dispose();attempt.Memory=null;attempt.DogBefore=attempt.DogAfter=null;attempt.LogicalBefore=attempt.LogicalAfter=null;attempt.Locks=null;attempt.Intent=null;}
    }
}
