using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private sealed class QueueContext
    {
        internal RebirthProductiveEarnedUiOriginal.BindingToken UiSource;
        internal RebirthProductiveEarnedUiDeferredTransport.ClientTerminalPublication RecipientSource;
        internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication LocalOwnerSource;
        internal EntityPlayerLocal Actor;internal World World;internal TileEntityWorkstation Station;internal XUiC_WorkstationWindowGroup Window;
        internal XUiC_CraftingQueue Queue;internal Vector3i Position;internal int BlockType,Deaths;
        internal string Owner,Creation,WorldKey,SaveDirectory,PlayerDirectory;internal Guid Operation,Birth;
    }
    // UQR1 is an effect stage following an actual positive terminal. It is
    // deliberately separate from the independently installed birth observers.
    private sealed class QueueHandoff:IDisposable
    {
        internal QueueContext Binding;
        internal INetConnection Connection;
        internal ulong Clock;
        internal string TerminalPath,Path;internal byte[] TerminalReceipt,Native,Image,After;
        internal int Offset,AfterOffset,Copies,Shifts;internal byte[][] Rows,PreviousUi;internal string[] Definitions,Grids;
        internal int AutomaticSends;internal byte AutomaticHandle;internal bool AutomaticInvalid;
        internal Guid Nonce;internal double Next;internal bool Submitted,EmptyLocal,ResponseObserved,NoChange;internal long WindowSession;
        internal XUiC_RecipeStack[] Cards;internal IDisposable Retention;internal object Owners;internal bool Attempted,Entered,Observed;
        internal bool Current()
        {
            var b=Binding;
            return ThreadManager.IsMainThread()&&b!=null&&(b.UiSource!=null?RebirthProductiveEarnedUiOriginal.Enabled:b.RecipientSource!=null||b.LocalOwnerSource!=null)&&
                ReferenceEquals(GameManager.Instance?.World,b.World)&&ReferenceEquals(b.World.GetPrimaryPlayer(),b.Actor)&&
                !b.Actor.IsDead()&&b.Actor.Died==b.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(b.Actor)==b.Creation&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==b.Owner&&
                b.WorldKey==RebirthProductiveEarnedArtifact.WorldIdentity(b.World)&&b.SaveDirectory==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())&&
                b.PlayerDirectory==System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir())&&ReferenceEquals(b.World.GetTileEntity(b.Position),b.Station)&&
                b.World.GetBlock(b.Position).type==b.BlockType&&b.Window.WindowGroup?.isShowing==true&&
                ReferenceEquals(b.Window.WorkstationData?.TileEntity,b.Station)&&ReferenceEquals(b.Window.craftingQueue,b.Queue)&&
                ReferenceEquals(b.Window.xui?.playerUI?.entityPlayer,b.Actor)&&LockManager.Instance?.IsLockedByLocalPlayer(b.Station,0)==true&&
                (b.Station.lastTickTime==Clock||ReferenceEquals(handoffSync,this)&&Entered&&b.Station.lastTickTime==GameTimer.Instance.ticks||
                    ReferenceEquals(readingQueueEcho?.Source,this)&&readingQueueEcho.ReadCompleted&&readingQueueEcho.Reads==1)&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(b.Window,out long session)&&session==WindowSession&&
                (!b.World.IsRemote()||Connection!=null&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&RebirthLogisticsOriginalTransport.CanSendNative(Connection))&&
                RebirthProductiveEarnedArtifact.FileMatches(TerminalPath,TerminalReceipt);
        }
        public void Dispose(){Retention?.Dispose();Retention=null;Native=Image=After=TerminalReceipt=null;Rows=PreviousUi=null;Cards=null;Definitions=Grids=null;Owners=null;}
    }
    private static readonly System.Collections.Generic.Dictionary<XUiC_CraftingQueue,QueueHandoff> handoffs=new System.Collections.Generic.Dictionary<XUiC_CraftingQueue,QueueHandoff>();
    internal static bool HasRecipientQueueHandoff=>handoffs.Values.Any(value=>value.Binding.RecipientSource!=null||value.Binding.LocalOwnerSource!=null);
    [ThreadStatic]private static QueueHandoff rotating,handoffSync;
    internal static bool CanAdvanceQueueHandoff(XUiC_RecipeStack card)
    {
        var queue=card?.GetParentByType<XUiC_CraftingQueue>();
        return queue==null||!handoffs.ContainsKey(queue)||!card.HasRecipe()||!card.IsCrafting;
    }
    internal static bool CanSyncQueueHandoff(XUiC_WorkstationWindowGroup group)
        =>group?.craftingQueue==null||!handoffs.TryGetValue(group.craftingQueue,out var value)||ReferenceEquals(handoffSync,value);
    internal static bool CanTickQueueHandoff(TileEntityWorkstation station)
        =>!handoffs.Values.Any(value=>ReferenceEquals(value.Binding.Station,station));
    internal static bool CanSendQueueHandoff(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string digest)
        =>handoffs.Values.Any(value=>ReferenceEquals(value.Binding.Actor,actor)&&ReferenceEquals(value.Binding.Station,station)&&
            value.Binding.Operation==operation&&value.Image!=null&&Hash(value.Image)==digest&&value.Current()&&HandoffUiCurrent(value));
    private static void ReleaseQueueHandoffs()
    {foreach(var value in handoffs.Values)value.Dispose();handoffs.Clear();ReleaseLocalQueueContinuations();ReleaseRecipientQueueContinuations();rotating=handoffSync=null;}
    private static void RetainQueueHandoff(Original original,string archived)
    {
        if(!original.TerminalSaved||!original.Current())return;
        var b=original.Binding;
        // A real local recipient scope owns the receipt-removal/owner event
        // and must hand off its own saved final image. Do not create a second
        // UIO queue owner over that same window at original archival.
        if(RebirthProductiveEarnedUiOwnerFile.HoldsLocalOwner(b.Station))return;
        if(!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(b.Window,out long session))return;
        // A positive terminal with remaining quantity needs no card rotation.
        if(b.Card.HasRecipe()&&b.Card.GetRecipeCount()>0)return;
        if(!b.Queue.GetRecipesToCraft().Any(card=>card.HasRecipe()&&card.GetRecipeCount()>0))return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(b.Station,out var native,out int offset))return;
            var terminal=File.ReadAllBytes(original.Path+".TerminalSaved");
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*native.Length+4L*terminal.Length+65536),out var retention))return;
            var context=new QueueContext{UiSource=b,Actor=b.Actor,World=b.World,Station=b.Station,Window=b.Window,Queue=b.Queue,Position=b.Position,BlockType=b.BlockType,
                Deaths=b.Deaths,Owner=b.Owner,Creation=b.Creation,WorldKey=b.WorldKey,SaveDirectory=b.SaveDirectory,PlayerDirectory=b.PlayerDirectory,Operation=b.Operation,Birth=b.Birth};
            var value=new QueueHandoff{Binding=context,TerminalPath=Path.Combine(archived,Path.GetFileName(original.Path))+".TerminalSaved",
                TerminalReceipt=terminal,Native=native,Offset=offset,Path=Path.Combine(archived,"queue.uqr"),Retention=retention,
                Connection=b.World.IsRemote()?SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]:null,WindowSession=session,Clock=b.Station.lastTickTime};
            if(handoffs.TryGetValue(b.Queue,out var previous))previous.Dispose();handoffs[b.Queue]=value;
        }
    }
    internal static void BeginQueueHandoff(XUiC_CraftingQueue queue)
    {
        rotating=null;
        try
        {
            if(!handoffs.TryGetValue(queue,out var value)||value.Attempted||!value.Current())return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.Native,value.Offset))return;
                var cards=queue.GetRecipesToCraft();if(cards==null||cards.Length<2||cards[^1].HasRecipe()||cards.All(card=>!card.HasRecipe()))return;
                if(cards.Any(card=>card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()))return;
                var current=cards.Select(card=>RebirthProductiveEarnedRowProof.CaptureRow(UiRow(card))).ToArray();
                if(value.PreviousUi!=null)
                {if(current.Length!=value.PreviousUi.Length||current.Where((row,index)=>!row.SequenceEqual(value.PreviousUi[index])).Any())return;}
                else if(!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(value.Binding.Station,cards.Select(UiRow).ToArray(),null,null,null,null,
                    out var baseline,out var projection,out int baselineOffset,out int projectionOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(baseline,baselineOffset,projection,projectionOffset))return;
                value.Cards=cards;value.Rows=cards.Select(card=>RebirthProductiveEarnedRowProof.CaptureRow(UiRow(card))).ToArray();
                value.Definitions=cards.Select(card=>card.GetRecipe()==null?"":RebirthRecipeOutputs.DefinitionIdentity(card.GetRecipe())).ToArray();
                value.Copies=0;rotating=value;
            }
        }
        catch{rotating=null;}
    }
    internal static object BeforeQueueHandoffCopy(XUiC_RecipeStack from,XUiC_RecipeStack to,bool run)
    {
        var value=rotating;
        if(!run||value==null||!value.Current())return null;
        int destination=value.Cards.Length-1-value.Copies;
        if(destination<1||!ReferenceEquals(value.Cards[destination-1],from)||!ReferenceEquals(value.Cards[destination],to)||
            !RebirthProductiveEarnedRowProof.CaptureRow(UiRow(from)).SequenceEqual(value.Rows[destination-1])){rotating=null;return null;}
        return value;
    }
    internal static void AfterQueueHandoffCopy(XUiC_RecipeStack to,bool run,object state)
    {
        if(state is not QueueHandoff value||!ReferenceEquals(value,rotating))return;
        int destination=value.Cards.Length-1-value.Copies;
        if(!run||!RebirthProductiveEarnedRowProof.CaptureRow(UiRow(to)).SequenceEqual(value.Rows[destination-1])){rotating=null;return;}
        value.Copies++;
    }
    internal static void EndQueueHandoff(XUiC_CraftingQueue queue,bool run)
    {
        var value=rotating;rotating=null;
        try
        {
            if(!run||value==null||!ReferenceEquals(value.Binding.Queue,queue)||!value.Current()||value.Copies!=value.Cards.Length-1)return;
            var cards=queue.GetRecipesToCraft();if(!cards.SequenceEqual(value.Cards)||cards[0].HasRecipe()||cards[0].GetRecipeCount()!=0)return;
            for(int i=1;i<cards.Length;i++)
            {
                var row=UiRow(cards[i]);if(i==cards.Length-1&&cards[i].HasRecipe())row.IsCrafting=false;
                // The former empty last card implies its predecessor was not
                // crafting; only vanilla's final IsCrafting=true may differ.
                var prior=value.Rows[i-1];
                if(!RebirthProductiveEarnedRowProof.CaptureRow(row).SequenceEqual(prior)||
                    (cards[i].GetRecipe()==null?"":RebirthRecipeOutputs.DefinitionIdentity(cards[i].GetRecipe()))!=value.Definitions[i-1])return;
            }
            value.Shifts++;value.PreviousUi=cards.Select(card=>RebirthProductiveEarnedRowProof.CaptureRow(UiRow(card))).ToArray();
            if(!cards[^1].HasRecipe())return; // Further actual shifts may finish the same handoff.
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.Native,value.Offset)||
                    !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(value.Binding.Station,cards.Select(UiRow).ToArray(),
                        value.Binding.Window.inputWindow?.GetSlots(),value.Binding.Window.outputWindow?.GetSlots(),value.Binding.Window.fuelWindow?.GetSlots(),value.Binding.Window.toolWindow?.GetSlots(),
                        out var before,out value.After,out int offset,out value.AfterOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(before,offset,value.Native,value.Offset)||
                    !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(value.Binding.Station,cards.Select(UiRow).ToArray(),null,null,null,null,
                        out _,out var queueOnly,out _,out int queueOffset)||
                    !RebirthProductiveEarnedNativeBody.SameExceptElapsed(queueOnly,queueOffset,value.After,value.AfterOffset))return;
                var receipt=new XElement("nativeUiQueueHandoff",new XAttribute("version",1),new XAttribute("operation",value.Binding.Operation.ToString("N")),
                    new XAttribute("source",value.Binding.LocalOwnerSource!=null?"localOwner":value.Binding.RecipientSource==null?"uio":"recipient"),
                    new XAttribute("terminal",Hash(value.TerminalReceipt)),new XAttribute("birth",value.Binding.Birth.ToString("N")),new XAttribute("shifts",value.Shifts),
                    new XAttribute("beforeOffset",value.Offset),new XAttribute("afterOffset",value.AfterOffset),new XElement("before",Convert.ToBase64String(value.Native)),new XElement("after",Convert.ToBase64String(value.After)));
                value.Image=Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting));
                value.Rows=cards.Select(card=>RebirthProductiveEarnedRowProof.CaptureRow(UiRow(card))).ToArray();
                value.Definitions=cards.Select(card=>card.GetRecipe()==null?"":RebirthRecipeOutputs.DefinitionIdentity(card.GetRecipe())).ToArray();
                value.Owners=RebirthProductiveEarnedUiQueueOwner.CaptureHandoffOwners(value.Binding.Station);
                value.Grids=HandoffGrids(value);
                if(!WriteExact(value.Path,value.Image))return;
            }
            ProgressQueueHandoff(value);
        }
        catch{/* Durable attempted source remains unknown; no retry or stale native resend. */}
    }
    private static void PumpQueueHandoffs()
    {foreach(var value in handoffs.Values.ToArray())try{if(value.Image!=null)ProgressQueueHandoff(value);}catch{/* Retain unknown attempt. */}}
    private static void ProgressQueueHandoff(QueueHandoff value)
    {
        if(!value.Current()||!HandoffUiCurrent(value))return;
        var b=value.Binding;
        if(value.EmptyLocal&&value.Observed){FinishEmptyLocalQueue(value);return;}
        if(!b.World.IsRemote()&&value.Observed){FinishObservedLocalQueue(value);return;}
        if(b.World.IsRemote())
        {
            if(RebirthProductiveEarnedWire.TryReadReply(b.Operation,out var reply)&&reply.Digest==Hash(value.Image)&&reply.Birth==b.Birth&&
                reply.Position.Equals(b.Position)&&reply.Owner==b.Owner&&reply.Creation==b.Creation&&reply.Deaths==b.Deaths)
            {
                if(reply.State==RebirthProductiveEarnedWire.Status.UiQueueAccepted&&value.Submitted&&value.ResponseObserved&&reply.Nonce==value.Nonce)
                {if(WriteExact(value.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-accepted:"+Hash(value.Image)))){if(b.RecipientSource!=null&&value.NoChange&&value.Cards.All(card=>card.GetRecipe()==null&&card.GetRecipeCount()==0)){ReleaseEmptyRecipientQueue(value);return;}if(b.RecipientSource!=null&&!RetainAcceptedRecipientQueue(value))return;handoffs.Remove(b.Queue);if(b.RecipientSource==null)value.Dispose();}return;}
                if(reply.State==RebirthProductiveEarnedWire.Status.UiQueueHeader)value.Nonce=reply.Nonce;
            }
            double now=Monotonic();
            if(value.Nonce==Guid.Empty||value.Submitted)
            {
                if(now<value.Next)return;value.Next=now+0.5;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder!=null)RebirthProductiveEarnedWire.Send(b.Actor,b.Station,value.Submitted?RebirthProductiveEarnedWire.Kind.UiQueueQuery:RebirthProductiveEarnedWire.Kind.UiQueueHandoff,
                        b.Operation,value.Nonce,value.Submitted?null:value.Image,Hash(value.Image));
                return;
            }
        }
        if(value.Attempted||!WriteExact(value.Path+".attempt",Encoding.ASCII.GetBytes("UQR1-attempt:"+Hash(value.Image))))return;
        value.Attempted=true;handoffSync=value;
        try{b.Window.syncTEfromUI();}finally{handoffSync=null;}
        if(!value.Observed)return;
        RebirthProductiveEarnedUiQueueOwner.ObserveHandoffOwners(value.Owners);
        if(b.World.IsRemote())
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(manager?.IsClient!=true||manager.IsServer||!ReferenceEquals(manager.connectionToServer?[0],value.Connection)||!RebirthLogisticsOriginalTransport.CanSendNative(value.Connection))return;
            if(!TryGetHandoffExplicitHandle(value,out byte handle))return;
            value.Submitted=true;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(b.Station,StreamModeWrite.ToServer,handle));
            return;
        }
        FinishObservedLocalQueue(value);
    }
    private static void FinishObservedLocalQueue(QueueHandoff value)
    {
        if(value?.Observed!=true||!value.Attempted||!value.Entered||!value.Current()||value.Binding.World.IsRemote()||!HandoffUiCurrent(value))return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.After,value.AfterOffset))return;
        if(value.EmptyLocal){FinishEmptyLocalQueue(value);return;}
        // Retry only publication/retention of the already observed native sync.
        // A failed write cannot cause a second syncTEfromUI invocation.
        if(!WriteExact(value.Path+".accepted",Encoding.ASCII.GetBytes("UQR1-local-sync:"+Hash(value.Image))))return;
        if(value.Binding.LocalOwnerSource!=null)
        {if(!RetainAcceptedLocalQueue(value))return;handoffs.Remove(value.Binding.Queue);}
        else{handoffs.Remove(value.Binding.Queue);value.Dispose();}
    }
    internal static bool EnterQueueHandoffSync(XUiC_WorkstationWindowGroup group,bool run,out object state)
    {
        state=null;var value=handoffSync;if(value==null)return true;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(!run||value.Entered||!value.Current()||!ReferenceEquals(group,value.Binding.Window)||!HandoffUiCurrent(value)||decoder==null||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.Native,value.Offset))return false;
            value.Entered=true;state=value;return true;
        }
    }
    internal static void ObserveQueueHandoffSync(XUiC_WorkstationWindowGroup group,bool run,object state)
    {
        try
        {
            if(state is not QueueHandoff value||!run||!ReferenceEquals(value,handoffSync)||!ReferenceEquals(group,value.Binding.Window)||!value.Current()||!HandoffUiCurrent(value))return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(value.Binding.Station,value.After,value.AfterOffset)&&
                    value.Binding.Station.lastTickTime==GameTimer.Instance.ticks&&(!value.Binding.World.IsRemote()||value.AutomaticSends==1&&!value.AutomaticInvalid))
                {
                    value.Clock=value.Binding.Station.lastTickTime;
                    value.Observed=WriteExact(value.Path+".observed",Encoding.ASCII.GetBytes("UQR1-observed:"+Hash(value.Image)));
                }
        }
        catch{}
    }
    private static bool HandoffUiCurrent(QueueHandoff value)
    {
        var cards=value.Binding.Queue.GetRecipesToCraft();if(cards==null||!cards.SequenceEqual(value.Cards))return false;
        for(int i=0;i<cards.Length;i++)
            if(!RebirthProductiveEarnedRowProof.CaptureRow(UiRow(cards[i])).SequenceEqual(value.Rows[i])||
                (cards[i].GetRecipe()==null?"":RebirthRecipeOutputs.DefinitionIdentity(cards[i].GetRecipe()))!=value.Definitions[i])return false;
        return value.Grids!=null&&value.Grids.SequenceEqual(HandoffGrids(value));
    }
    private static string[] HandoffGrids(QueueHandoff value)
    {
        var b=value.Binding;
        return new[]{Grid(b.Window.inputWindow?.GetSlots()??b.Station.Input),Grid(b.Window.outputWindow?.GetSlots()??b.Station.Output),
            Grid(b.Window.fuelWindow?.GetSlots()??b.Station.Fuel),Grid(b.Window.toolWindow?.GetSlots()??b.Station.Tools)};
    }
}



