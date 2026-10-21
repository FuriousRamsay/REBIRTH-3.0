using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    // Issued only by successful Add, original sync, actual Setup, original
    // transport enqueue and authenticated matching host attestation together.
    internal sealed class RemoteInsertion:IDisposable
    {
        internal EntityPlayerLocal Actor;internal World World;internal XUiC_CraftingQueue Queue;
        internal XUiC_WorkstationWindowGroup Window;internal XUiC_RecipeStack Card;internal TileEntityWorkstation Station;
        internal Recipe Recipe;internal Guid Operation=Guid.NewGuid();internal int Index,UploadIndex=-1,Count,Deaths,Block;
        internal string Owner,Creation,Save,WorldKey,FrozenBinding,NativeRow,Digest,Header,Purpose;
        internal NetConnectionAbs Connection;internal byte[] Before,Body;internal IDisposable Allocation,BodyAllocation;
        internal bool Added,SyncEntered,SyncReturned,Sent,Attested,Closed,CardRetired;internal double QueryAt;
        internal bool SourceCurrent()
        {
            if(Closed||!Added||!ThreadManager.IsMainThread()||Actor==null||Actor.IsDead()||Actor.Died!=Deaths||
                !ReferenceEquals(World,GameManager.Instance?.World)||World?.IsRemote()!=true||!ReferenceEquals(Actor.world,World)||
                !ReferenceEquals(World.GetPrimaryPlayer(),Actor)||RebirthCamperCharacterAccess.CurrentCreation(Actor)!=Creation||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=Owner||
                Path.GetFullPath(GameIO.GetSaveGameDir())!=Save||RebirthProductiveEarnedArtifact.WorldIdentity(World)!=WorldKey||
                !ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)||World.GetBlock(Station.ToWorldPos()).type!=Block||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection))return false;
            return true;
        }
        internal bool CardCurrent()
        {
            if(CardRetired||!SourceCurrent()||!ReferenceEquals(Window?.WorkstationData?.TileEntity,Station)||
                !ReferenceEquals(Window.craftingQueue,Queue)||!ReferenceEquals(Card?.xui?.playerUI?.entityPlayer,Actor))return false;
            var cards=Queue.GetRecipesToCraft();return Index>=0&&Index<cards.Length&&ReferenceEquals(cards[Index],Card)&&
                cards.Count(value=>ReferenceEquals(value,Card))==1&&ReferenceEquals(Card.GetRecipe(),Recipe)&&
                Card.GetRecipeCount()>0&&Card.GetRecipeCount()<=Count&&Card.StartingEntityId==Actor.entityId&&
                Binding(InsertionRow(Card))==FrozenBinding;
        }
        internal bool PublishedCurrent()=>Attested&&SyncReturned&&Sent&&Body!=null&&SourceCurrent();
        public void Dispose(){if(Closed)return;Closed=true;Before=Body=null;Allocation?.Dispose();Allocation=null;BodyAllocation?.Dispose();BodyAllocation=null;}
    }
    private static readonly List<RemoteInsertion> remoteInsertions=new List<RemoteInsertion>();
    [ThreadStatic]private static RemoteSync remoteSync;
    private sealed class RemoteSync {internal RemoteInsertion[] Members;internal RemoteSync Previous;}
    private sealed class RemoteMove {internal RemoteInsertion Source;internal XUiC_RecipeStack From,To;internal int Index;}
    internal static void BeforeRemoteInsertion(XUiC_CraftingQueue queue,int index,Recipe recipe,int count,bool modification,int startingEntityId,bool run,out object state)
    {
        state=null;
        try
        {
            if(!installed||!run||modification||startingEntityId!=-1||recipe==null||count<1||count>short.MaxValue)return;
            ObserveWorld();var actor=queue.xui?.playerUI?.entityPlayer;var window=queue.GetParentByType<XUiC_WorkstationWindowGroup>();var station=window?.WorkstationData?.TileEntity;
            var cards=queue.GetRecipesToCraft();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0] as NetConnectionAbs;
            if(actor?.world?.IsRemote()!=true||actor.IsDead()||station==null||station is RebirthCamperStationState||connection==null||
                remoteInsertions.Count>=32||index<0||index>=cards.Length||cards[index].GetRecipe()!=null||
                !ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(!RebirthOriginalActionScope.Validate(owner.StorageKey,creation))return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!Capture(station,out var before,out _)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*before.Length+1024*1024),out var allocation))return;
                state=new RemoteInsertion{Actor=actor,World=actor.world,Window=window,Queue=queue,Station=station,Card=cards[index],Recipe=recipe,
                    Index=index,Count=count,Deaths=actor.Died,Owner=owner.StorageKey,Creation=creation,Connection=connection,Before=before,Allocation=allocation,
                    Save=Path.GetFullPath(GameIO.GetSaveGameDir()),WorldKey=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),Block=actor.world.GetBlock(station.ToWorldPos()).type};
            }
        }
        catch{if(state is RemoteInsertion value)value.Dispose();state=null;}
    }
    internal static void AfterRemoteInsertion(bool result,bool run,object state)
    {
        if(state is not RemoteInsertion value)return;
        try
        {
            if(!result||!run||!ReferenceEquals(value.Card.GetRecipe(),value.Recipe)||value.Card.GetRecipeCount()!=value.Count||value.Card.StartingEntityId!=value.Actor.entityId)return;
            foreach(var old in remoteInsertions.Where(old=>ReferenceEquals(old.Card,value.Card)).ToArray())old.CardRetired=true;
            foreach(var old in originals.Where(old=>old.RemoteInsertion!=null&&ReferenceEquals(old.RemoteInsertion.Card,value.Card)).ToArray())old.RowRetired=true;
            value.FrozenBinding=Binding(InsertionRow(value.Card));value.NativeRow=NativeBinding(InsertionRow(value.Card));
            if(value.FrozenBinding.Length>32768||value.NativeRow.Length>32768)return;
            value.Added=true;if(!value.CardCurrent())return;remoteInsertions.Add(value);
        }
        catch{/* No source on a partial native insertion observation. */}
    }
    internal static void EndRemoteInsertion(object state)
    {if(state is RemoteInsertion value&&!remoteInsertions.Contains(value))value.Dispose();}
    internal static void BeforeRemoteCopy(XUiC_RecipeStack from,XUiC_RecipeStack to,bool run,out object state)
    {
        state=null;try
        {
            if(!run||ReferenceEquals(from,to))return;
            var found=remoteInsertions.Where(value=>ReferenceEquals(value.Card,from)&&value.CardCurrent()).ToArray();if(found.Length!=1)return;
            var source=found[0];var cards=source.Queue.GetRecipesToCraft();int index=Array.FindIndex(cards,card=>ReferenceEquals(card,to));
            if(index>=0&&!remoteInsertions.Any(value=>!ReferenceEquals(value,source)&&ReferenceEquals(value.Card,to)))state=new RemoteMove{Source=source,From=from,To=to,Index=index};
        }catch{}
    }
    internal static void AfterRemoteCopy(XUiC_RecipeStack to,bool run,object state)
    {
        try
        {
            if(!run||state is not RemoteMove move||!ReferenceEquals(to,move.To)||!move.Source.CardCurrent()||
                !ReferenceEquals(move.Source.Card,move.From)||to.GetRecipeCount()!=move.Source.Card.GetRecipeCount()||Binding(InsertionRow(to))!=move.Source.FrozenBinding)return;
            move.Source.Card=to;move.Source.Index=move.Index;
        }catch{}
    }
    internal static void BeforeRemoteSync(XUiC_WorkstationWindowGroup window,bool run,out object state)
    {
        state=null;try
        {
            if(!run||!installed||remoteSync!=null)return;
            var members=remoteInsertions.Where(value=>ReferenceEquals(value.Window,window)&&!value.SyncEntered&&value.CardCurrent()).ToArray();
            if(members.Length==0)return;
            var scope=new RemoteSync{Members=members,Previous=remoteSync};foreach(var value in members)value.SyncEntered=true;
            remoteSync=scope;state=scope;
        }catch{}
    }
    internal static void AfterRemoteSync(XUiC_WorkstationWindowGroup window,bool run,object state)
    {
        try
        {
            if(!run||state is not RemoteSync scope||!ReferenceEquals(remoteSync,scope))return;
            foreach(var value in scope.Members)if(ReferenceEquals(value.Window,window)&&value.CardCurrent()&&value.Sent&&value.Body!=null)
                value.SyncReturned=true;
        }catch{}
    }
    internal static void EndRemoteSync(object state)
    {if(state is RemoteSync scope&&ReferenceEquals(remoteSync,scope))remoteSync=scope.Previous;}
    private static void DropRemoteInsertions()
    {
        foreach(var value in remoteInsertions)value.Dispose();remoteInsertions.Clear();
        foreach(var value in remoteHeaders.Values)value.Dispose();remoteHeaders.Clear();remoteSync=null;
    }
    private static void PumpRemoteInsertions()
    {
        double now=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
        foreach(var value in remoteInsertions.ToArray())
        {
            try
            {
                if(value.Attested&&!originals.Any(source=>ReferenceEquals(source.RemoteInsertion,value)))
                {remoteInsertions.Remove(value);value.Dispose();continue;}
                if(!value.SourceCurrent())continue;
                if(value.SyncReturned&&value.Sent&&!value.Attested&&now>=value.QueryAt)
                {value.QueryAt=now+2;value.Connection.AddToSendQueue(NetPackageManager.GetPackage<NetPackageRebirthUiQueueInsertionRequestV1>().Setup(value.Header,true));}
            }catch{/* Query only; never repeat native Add/sync/upload. */}
        }
    }
}
