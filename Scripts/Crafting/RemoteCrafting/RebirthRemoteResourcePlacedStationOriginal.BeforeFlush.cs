using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class BeforeFlush
    {
        internal EntityPlayerLocal Player;internal ItemActionEntryCraft Entry;internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal XUiC_WorkstationWindowGroup Window;internal TileEntityWorkstation Station;internal bool Entered,Observed,Queued;
        internal readonly HashSet<string> Methods=new HashSet<string>(StringComparer.Ordinal);
        internal bool Current()=>RebirthRemoteResourceClientOriginal.Current(Player,Original)&&RebirthRemoteResourceOwnerAction.HoldsOriginalScope(Player,Original)&&
            RebirthRemoteResourcePlacedStationScope.TryObserveWindow(Player,Entry,out var actual,out _)&&ReferenceEquals(actual,Station)&&
            ReferenceEquals(Window,Entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>())&&
            RebirthRemoteResourceClientOriginal.ReadActor(Original.Copy().Element("actorBefore").Value).Matches(RebirthCamperEnergyActorImage.Capture(Player));
    }
    [ThreadStatic]private static BeforeFlush executingBeforeFlush;
    private static readonly Dictionary<Guid,BeforeFlush> beforeFlushPending=new Dictionary<Guid,BeforeFlush>();
    private static bool SameGrid(ItemStack[] left,ItemStack[] right)=>left!=null&&right!=null&&left.Length==right.Length&&
        left.Select((value,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(value,right[index])).All(value=>value);
    private static bool TryUiQueue(XUiC_WorkstationWindowGroup window,out RecipeQueueItem[] queue)
    {
        queue=null;var cards=window?.craftingQueue?.GetRecipesToCraft();
        if(cards==null||cards.Length<1||cards.Length>byte.MaxValue||cards.Any(card=>card==null||card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()))return false;
        queue=cards.Select(card=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=(short)card.GetRecipeCount(),CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),
            IsCrafting=card.IsCrafting,Quality=(byte)card.OutputQuality,StartingEntityId=card.StartingEntityId,OneItemCraftTime=card.GetOneItemCraftTime()}).ToArray();return true;
    }
    private static bool FlushUiCurrent(BeforeFlush original)
    {
        var window=original.Window;var station=original.Station;
        return original.Current()&&TryUiQueue(window,out var queue)&&RebirthRemoteResourcePlacedStationQueue.Encode(queue)==original.Original.Copy().Element("action").Element("queue").Value&&
            (window.inputWindow==null||SameGrid(window.inputWindow.GetSlots(),station.Input))&&
            (window.outputWindow==null||SameGrid(window.outputWindow.GetSlots(),station.Output))&&
            (window.toolWindow==null||SameGrid(window.toolWindow.GetSlots(),station.Tools))&&
            (window.fuelWindow==null||SameGrid(window.fuelWindow.GetSlots(),station.Fuel));
    }
    private static bool FlushCapturedBefore(EntityPlayerLocal player,ItemActionEntryCraft entry,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(executingBeforeFlush!=null||!RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player,entry,out var station,out _))return false;
        var context=new BeforeFlush{Player=player,Entry=entry,Original=original,Station=station,Window=entry.ItemController.GetParentByType<XUiC_WorkstationWindowGroup>()};
        if(!FlushUiCurrent(context))return false;
        var journal=SmallPlacedJournal(original.Operation,SmallJournalDomain.ClientBeforeFlush);
        // Full original with full UI queue was saved by Capture before this
        // purpose-specific preattempt. Never retry an uncertain native sync.
        byte[] command=Encoding.UTF8.GetBytes(new XElement("placedBeforeFlush",new XAttribute("version",1),new XAttribute("job",original.Operation.ToString("N")),
            new XAttribute("original",original.Digest)).ToString(SaveOptions.DisableFormatting));
        if(journal.Read(original.Operation)!=null)return false;
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,command,command);
        if(record.State!=RebirthCamperTransferJournal.Phase.Prepared||!record.Before.SequenceEqual(command)||!record.After.SequenceEqual(command))return false;
        record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
        beforeFlushPending.Add(original.Operation,context);QueueCapturedBefore(context);return true;
    }
    private static void QueueCapturedBefore(BeforeFlush context)
    {
        var player=context.Player;var original=context.Original;var entry=context.Entry;
        if(context.Queued||!FlushUiCurrent(context))return;
        var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
        var header=new XElement("placedStationRequest",new XAttribute("version",1),new XAttribute("stage",3),original.Copy());
        context.Queued=NetPackageRebirthRemoteRepairOriginal.SendObserved(player.entityId,persistent.PrimaryId,original,header.ToString(SaveOptions.DisableFormatting),sent=>
        {
            beforeFlushPending.Remove(original.Operation);
            if(!clientJobs.TryGetValue(original.Operation,out var job)||!ReferenceEquals(job.Original,original))return;
            job.Stage=1;
            if(!sent||executingBeforeFlush!=null||!FlushUiCurrent(context))return;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
            if(manager?.IsClient!=true||manager.IsServer||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
            executingBeforeFlush=context;
            try
            {
                // Its ORIGINAL SetModified sends the actual current ToServer
                // once, after every header fragment entered this connection.
                // Never append a second explicit send or timeout replay.
                context.Window.syncTEfromUI();if(!context.Observed||!FlushUiCurrent(context))return;
                var journal=SmallPlacedJournal(original.Operation,SmallJournalDomain.ClientBeforeFlush);var saved=journal.Read(original.Operation);if(saved?.State!=RebirthCamperTransferJournal.Phase.Uncertain)return;
                saved=journal.Advance(original.Operation,saved.State,RebirthCamperTransferJournal.Phase.Applied);
                if(saved.State==RebirthCamperTransferJournal.Phase.Applied&&RebirthRemoteResourcePlacedStationClientCustody.TryHoldFresh(player,entry,original)){job.Stage=0;job.Next=0;}
            }
            finally{executingBeforeFlush=null;}
        });
    }
    private static void PumpCapturedBefore(ClientJob job)
    {if(beforeFlushPending.TryGetValue(job.Original.Operation,out var pending)&&!pending.Queued)QueueCapturedBefore(pending);}
    internal static bool IsExecutingBeforeFlush(XUiC_WorkstationWindowGroup window)=>
        executingBeforeFlush!=null&&ReferenceEquals(executingBeforeFlush.Window,window)&&executingBeforeFlush.Current()||
        cancellationFlush!=null&&ReferenceEquals(cancellationFlush.Window,window)&&cancellationFlush.NativeLease();
    internal static bool IsExecutingBeforeFlush(TileEntityWorkstation station)=>
        executingBeforeFlush!=null&&ReferenceEquals(executingBeforeFlush.Station,station)&&executingBeforeFlush.Current()||
        cancellationFlush!=null&&ReferenceEquals(cancellationFlush.Station,station)&&cancellationFlush.NativeLease();
    internal static bool ConsumeBeforeFlushSetter(TileEntityWorkstation station,MethodBase method,object[] arguments)
    {
        var normal=executingBeforeFlush;var cancel=cancellationFlush;
        if(method?.DeclaringType!=typeof(TileEntityWorkstation)||!IsExecutingBeforeFlush(station)||normal!=null&&!normal.Entered||cancel!=null&&!cancel.FlushEntered)return false;
        var methods=normal!=null?normal.Methods:cancel.FlushMethods;string name=method.Name;if(methods.Contains(name))return false;
        if(name==nameof(TileEntityWorkstation.ResetTickTime)){if(arguments?.Length!=0)return false;}
        else
        {
            if(arguments?.Length!=1)return false;var window=normal!=null?normal.Window:cancel.Window;
            if(name=="set_Queue")
            {
                string queue=normal!=null?normal.Original.Copy().Element("action").Element("queue").Value:RebirthCookingRemoteAdmission.ReadProtocolXml(cancel.Header,MaximumResultBytes).Element("queue").Value;
                if(arguments[0] is not RecipeQueueItem[] rows||RebirthRemoteResourcePlacedStationQueue.Encode(rows)!=queue)return false;
            }
            else
            {
                var grid=name=="set_Input"?window.inputWindow?.GetSlots():name=="set_Output"?window.outputWindow?.GetSlots():name=="set_Tools"?window.toolWindow?.GetSlots():name=="set_Fuel"?window.fuelWindow?.GetSlots():null;
                if(arguments[0] is not ItemStack[] items||grid==null||!SameGrid(grid,items))return false;
            }
        }
        methods.Add(name);return true;
    }
    internal static bool EnterBeforeFlush(XUiC_WorkstationWindowGroup window,bool runOriginal,out object state)
    {
        state=null;var original=executingBeforeFlush;if(original==null)return EnterCancellationFlush(window,runOriginal,out state);
        if(!runOriginal||original.Entered||!ReferenceEquals(window,original.Window)||!FlushUiCurrent(original))return false;
        original.Entered=true;state=original;return true;
    }
    internal static void ObserveBeforeFlush(XUiC_WorkstationWindowGroup window,bool runOriginal,object state)
    {
        ObserveCancellationFlush(window,runOriginal,state);
        var original=state as BeforeFlush;
        if(!runOriginal||original==null||!ReferenceEquals(original,executingBeforeFlush)||!ReferenceEquals(window,original.Window)||!original.Entered||!FlushUiCurrent(original))return;
        original.Observed=RebirthRemoteResourcePlacedStationQueue.Encode(original.Station.Queue)==original.Original.Copy().Element("action").Element("queue").Value;
    }
}
