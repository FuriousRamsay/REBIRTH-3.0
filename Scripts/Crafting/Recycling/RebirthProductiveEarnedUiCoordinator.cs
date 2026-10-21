using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Dedicated owning UI custody. Every effect stays on the retained actual card.
// No source enables this coordinator before cumulative source-chain review.
internal static partial class RebirthProductiveEarnedUiCoordinator
{
    internal sealed class Original
    {
        internal RebirthProductiveEarnedUiOriginal.BindingToken Binding;
        internal RebirthProductiveEarnedUiOriginal.BeforeFlush BeforeFlush,EarnedFlush;
        internal RebirthProductiveEarnedUiOriginal.Original Ui;
        internal RebirthProductiveEarnedUiOwnerFile.Witness OwnerFile;
        internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        internal RebirthProductiveEarnedUiOwnerFile.DeferredWitness Deferred;
        internal byte[] Image;internal string Path,Digest;
        internal NativePublication Before,After,Terminal;
        internal bool WearAttempted,Worn,EarnedSaved,CompletionAttempted,Delivered,ViewerSaved,StationPublished,TerminalSaved,Recovered;
        internal XElement Delivery;internal RebirthRecipeOutputIntegration.OwningObservation Output;
        internal IDisposable DeliveryRetention,TerminalRetention;
        internal Guid Nonce;internal byte[] TerminalBefore,TerminalBody;internal int TerminalBeforeOffset,TerminalOffset;
        internal int BeforeCount;
        internal double NextHeader,NextQuery,NextTerminal;
        internal bool TerminalSyncAttempted,TerminalSynced;internal int TerminalAutomaticSends;internal byte TerminalAutomaticHandle;internal bool TerminalAutomaticInvalid;
        internal object LocalCachedSync;
        internal float PreparationTarget,PreparationWorked;
        internal byte[] PreparationTools;internal IDisposable PreparationAllocation;
        internal bool PreparationComplete,OwnerBeforeAttached,WearObserved;
        internal bool Current()=>(StationPublished?CurrentDetached(this):Delivered?CurrentDelivered(this):Binding?.Current()==true)&&Ui!=null&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);
    }
    internal sealed class NativePublication:IDisposable
    {
        internal Original Original;internal byte[] Expected;internal int Offset;internal string Purpose;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal bool Closed;
        internal bool Current()
        {
            using(var callback=Memory?.TryEnterCallbacks())
                return callback!=null&&!Closed&&Original.Current()&&
                    RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Binding.Station,Expected,Offset);
        }
        internal bool Saved()
        {
            if(!Current()||Watch==null||!Watch.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string tile)||!Current())return false;
            var receipt=new XElement("nativeUiPublication",new XAttribute("operation",Original.Binding.Operation.ToString("N")),
                new XAttribute("purpose",Purpose),new XAttribute("uio",Original.Digest),new XAttribute("native",Hash(Expected)),
                new XAttribute("path",publication.Path),new XAttribute("payload",publication.PayloadDigest),
                new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",tile));
            return WriteExact(Original.Path+"."+Purpose+"Saved",Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting)));
        }
        public void Dispose(){if(Closed)return;Closed=true;Watch?.Dispose();Allocation?.Dispose();Memory?.Dispose();Expected=null;}
    }
    private static readonly Dictionary<XUiC_RecipeStack,Original> originals=new Dictionary<XUiC_RecipeStack,Original>();
    private static bool installed,coldUnknown,coldDiscovered;private static World observedWorld;
    internal static void Install()
    {
        if(installed)return;
        RebirthProductiveEarnedUiOwnerFile.Install();RebirthProductiveEarnedCancellation.Install();
        RebirthProductiveEarnedUiDeferredTransport.InstallRecipientWindowObserver();
        RebirthProductiveEarnedUiQueueOwner.Install();
        RebirthProductiveEarnedRemoteHost.Install();RebirthRecipeOutputIntegration.Install();RebirthProductiveEarnedWire.Install();
        InstallQueueRotationBoundary();
        installed=true;
    }
    internal static void InstallQueueRotationBoundary()
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new HarmonyLib.Harmony("rebirth.productive.ui.queue.rotation"),typeof(RebirthProductiveEarnedUiQueueRotation));
        RebirthHarmonyBootstrap.PatchClassOnce(new HarmonyLib.Harmony("rebirth.productive.ui.queue.rotation"),typeof(RebirthProductiveEarnedUiQueueRefresh));
    }
    internal static bool Blocks(TileEntityWorkstation station)=>station!=null&&station is not RebirthCamperStationState&&
        (handoffs.Values.Any(value=>ReferenceEquals(value.Binding.Station,station)&&(value.Binding.RecipientSource!=null||value.Binding.LocalOwnerSource!=null))||RebirthProductiveEarnedUiRemote.BlocksQueueHandoff(station)||RebirthProductiveEarnedUiRemoteBefore.BlocksStation(station)||
            RebirthProductiveEarnedUiOriginal.Enabled&&(coldUnknown||handoffs.Values.Any(value=>ReferenceEquals(value.Binding.Station,station))||
                originals.Values.Any(value=>ReferenceEquals(value.Binding.Station,station)&&!value.TerminalSaved)));
    internal static bool BlocksCancellation(XUiC_RecipeStack card)=>Blocks(card?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
    private static NativePublication BeginPublication(Original original,string purpose,byte[] expected,int offset)
    {
        if(!original.Current()||original.Binding.World.IsRemote())return null;
        var memory=RebirthProductiveEarnedObservationBudget.TryOpenUiFrame(original.Binding.World,original.Binding.Birth,original.Binding.Operation,original.Image);
        if(memory==null)return null;
        var result=new NativePublication{Original=original,Expected=(byte[])expected.Clone(),Offset=offset,Purpose=purpose,Memory=memory};
        try
        {
            result.Allocation=memory.TryObserveNative(expected,offset);
            if(result.Allocation==null||!RebirthRemoteResourceNativeBinding.TryResolve(original.Binding.World,
                RemoteResourceIdentity.Workstation(original.Binding.Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,
                original.Binding.Birth.ToString("N"),out var binding)){result.Dispose();return null;}
            RebirthRemoteResourceNativePublication.Install();
            result.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,result.Current,memory);
            if(result.Watch==null||!result.Current()){result.Dispose();return null;}return result;
        }
        catch{result.Dispose();return null;}
    }
    // Return true only when this dedicated source owns the final timer write.
    // The caller must retain the old timer while preparation is incomplete.
    internal static bool SetTime(XUiC_RecipeStack card,float target,float worked,ItemStack[] worn)
    {
        if(!RebirthProductiveEarnedUiOriginal.Enabled)return false;
        try
        {
            ObserveWorld();
            if(coldUnknown)return true;
            if(originals.TryGetValue(card,out var retained))
            {
                if(!retained.PreparationComplete)
                {ContinuePreparation(retained,card);return true;}
                if(retained.Recovered||!retained.Current())return true;
                if(retained.CompletionAttempted)return true;
                if(retained.WearAttempted)return true; // No replay of unknown native wear.
                if(retained.Binding.World.IsRemote())return AdvanceRemoteWear(retained,card);
                if(retained.Before==null)
                {
                    RebirthProductiveEarnedRowProof.Frame frame;
                    using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    {if(decoder==null)return true;frame=retained.Ui.ReadFrame();}
                    retained.Before=BeginPublication(retained,"before",frame.BeforeNative,frame.BeforeElapsedOffset);
                }
                if(retained.Before?.Saved()!=true||!retained.Ui.CurrentBefore()||!RetireLocalQueueContinuations(retained))return true;
                ApplyWear(retained,card);return true;
            }
            var station=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
            if(originals.Values.Any(value=>ReferenceEquals(value.Binding.Station,station)&&!value.TerminalSaved))return true;
            if(card.craftingTimeLeft<=0||target>0||worked<=0||originals.Count>=4)return false;
            Guid operation=descriptions.TryGetValue(card,out var described)?described.Operation:Guid.NewGuid();
            if(!RebirthProductiveEarnedUiOriginal.TryBind(card,operation,out var binding))
            {DescribeRemote(card,operation);return true;}
            if(!RebirthProductiveEarnedUiOriginal.TryPrepareBeforeFlush(binding,out var flush))return true;
            byte[] preparationTools;IDisposable preparationAllocation;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(worn,262144,349528,out var encodedTools))
                {flush.Dispose();return true;}
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*encodedTools.Length+65536),out preparationAllocation))
                {flush.Dispose();return true;}
                try{preparationTools=System.Text.Encoding.ASCII.GetBytes(encodedTools);}
                catch{preparationAllocation.Dispose();flush.Dispose();throw;}
            }
            var original=new Original{Binding=binding,BeforeFlush=flush,BeforeCount=card.GetRecipeCount(),
                PreparationTarget=target,PreparationWorked=worked,PreparationTools=preparationTools,PreparationAllocation=preparationAllocation};
            originals.Add(card,original);
            ContinuePreparation(original,card);
            return true;
        }
        catch{return true;} // Original/native custody stays unknown, never rewear.
    }
    private static void ContinuePreparation(Original original,XUiC_RecipeStack card)
    {
        var binding=original.Binding;
        if(original.PreparationComplete||binding?.Current()!=true||!ReferenceEquals(binding.Card,card)||original.PreparationTools==null)return;
        ItemStack[] worn;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {if(decoder==null||!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(System.Text.Encoding.ASCII.GetString(original.PreparationTools),
            binding.Window.toolWindow.GetSlots().Length,262144,349528,out worn))return;}
        // Only a positive actual sync observation can cross an existing attempt.
        // TryExecute itself refuses a durable unknown attempt; never replay it.
        if(!original.BeforeFlush.IsObserved&&!RebirthProductiveEarnedUiOriginal.TryExecuteBeforeFlush(original.BeforeFlush))return;
            if(!binding.World.IsRemote()&&binding.EarlierOwner==null)
            {
                if(!RebirthProductiveEarnedUiQueueOwner.TryObserveQueueOwner(binding.Station,binding.Index,binding.Station.Queue[binding.Index],out binding.EarlierOwner))return;
                binding.OwnerPurpose=RebirthProductiveEarnedUiQueueOwner.Export(binding.EarlierOwner);
            }
            original.EarlierOwner=binding.EarlierOwner;
            if(binding.World.IsRemote()&&!RebirthProductiveEarnedUiQueueOwner.TryImportPurpose(binding,out original.EarlierOwner))return;
            if(card.StartingEntityId!=binding.Actor.entityId)
            {if(original.Deferred==null)original.Deferred=RebirthProductiveEarnedUiOwnerFile.BeginDeferred(original,original.EarlierOwner);if(original.Deferred==null)return;}
            if(original.OwnerFile==null)original.OwnerFile=RebirthProductiveEarnedUiOwnerFile.Begin(binding);
            if(original.OwnerFile==null)return;
            if(original.Ui==null&&!RebirthProductiveEarnedUiOriginal.TryFreezeFinal(binding,original.PreparationTarget,original.PreparationWorked,worn,out original.Ui))return;
            if(!original.OwnerBeforeAttached)
            {if(!original.Ui.AttachOwnerBefore(original.OwnerFile.Before))return;original.OwnerBeforeAttached=true;}
            if(original.Image==null){original.Image=original.Ui.CopyArtifact();original.Digest=Hash(original.Image);}
            original.Path=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"RebirthProductiveEarnedUi",binding.WorldKey.Replace('/','_').Replace('+','-'),binding.Owner,binding.Operation.ToString("N"),"original.uio");
            if(!WriteExact(original.Path,original.Image))return;
            if(!WriteExact(original.Path+".OwnerBefore",original.OwnerFile.Before))return;
            original.PreparationComplete=true;original.PreparationTools=null;original.PreparationAllocation?.Dispose();original.PreparationAllocation=null;
            if(binding.World.IsRemote())
            {
                // Publish the actual current native owner file once. The host
                // prepares only against this exact accepted and saved before.
                var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(manager?.IsClient==true&&!manager.IsServer&&RebirthLogisticsOriginalTransport.CanSendNative(manager.connectionToServer?[0]))
                    manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(binding.Actor));
                PrepareRemote(original);
            }
    }
    private static void ApplyWear(Original original,XUiC_RecipeStack card)
    {
        if(original.OwnerFile==null||!original.Current()||!original.Ui.CurrentBefore())return;
        original.Before?.Dispose();original.Before=null;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return;var frame=original.Ui.ReadFrame();
            if(!RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out var after,out var beforeTools,out var tools)||
                RebirthProductiveToolWork.Limit(beforeTools,original.Binding.Recipe,original.Binding.Station.block,frame.WorkedSeconds)!=frame.WorkedSeconds||
                !RebirthProductiveEarnedArtifact.SameTools(RebirthProductiveToolWork.Wear(beforeTools,original.Binding.Recipe,original.Binding.Station.block,frame.WorkedSeconds),frame.AfterTools)||
                !Stage(original,"WearAttempted"))return;
            original.WearAttempted=true;
            original.Binding.Window.WorkstationData.SetToolStacks(tools);
            original.Binding.Window.GetChildByType<XUiC_WorkstationToolGrid>()?.SetSlots(tools);
            card.craftingTimeLeft=after.CraftingTimeLeft;
            if(!original.Ui.CurrentUi(true))return;
            original.WearObserved=true;
        }
        FinishObservedWear(original);
    }
    private static void FinishObservedWear(Original original)
    {
        if(!original.WearObserved||original.Worn||!original.Current())return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {if(decoder==null||!original.Ui.CurrentUi(true))return;}
        if(original.EarnedFlush==null&&!RebirthProductiveEarnedUiOriginal.TryPrepareBeforeFlush(original.Binding,"earned",out original.EarnedFlush))return;
        if(!original.EarnedFlush.IsObserved&&!RebirthProductiveEarnedUiOriginal.TryExecuteBeforeFlush(original.EarnedFlush))return;
        if(!original.Ui.CurrentEarnedProjection()||!Stage(original,"Worn"))return;
        original.Worn=true;
    }
    private static bool Stage(Original original,string purpose)=>original.Current()&&WriteExact(original.Path+"."+purpose,
        Encoding.ASCII.GetBytes("UIO1:"+purpose+":"+original.Digest+":"+original.Nonce.ToString("N")));
    private static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static double Monotonic()=>System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
    private static void QueryUi(Original original)
    {
        double now=Monotonic();if(now<original.NextQuery)return;original.NextQuery=now+0.25;
        RebirthProductiveEarnedWire.Send(original.Binding.Actor,original.Binding.Station,RebirthProductiveEarnedWire.Kind.UiQuery,
            original.Binding.Operation,original.Nonce,null,original.Digest);
    }
    private static bool WriteExact(string path,byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||!Safe(path)||File.Exists(path+".tmp"))return false;
        if(File.Exists(path))return RebirthProductiveEarnedArtifact.FileMatches(path,bytes);
        Directory.CreateDirectory(Path.GetDirectoryName(path));if(!Safe(path))return false;
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!RebirthProductiveEarnedArtifact.FileMatches(path+".tmp",bytes))return false;File.Move(path+".tmp",path);return RebirthProductiveEarnedArtifact.FileMatches(path,bytes);
    }
    private static bool Safe(string path)
    {
        try
        {
            string current=Path.GetFullPath(path);
            while(current!=null)
            {
                try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;}
                catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
                current=Path.GetDirectoryName(current);
            }
            return true;
        }
        catch{return false;}
    }
    internal static void ObservePurposeWorld()=>ObserveWorld();
    private static void ObserveWorld()
    {
        var world=GameManager.Instance?.World;
        if(ReferenceEquals(world,observedWorld))
        {if(!coldDiscovered)DiscoverCurrentOriginals();return;}
        foreach(var original in originals.Values){original.PreparationTools=null;original.PreparationAllocation?.Dispose();original.PreparationAllocation=null;original.Deferred?.Dispose();original.Output?.Dispose();original.Before?.Dispose();original.After?.Dispose();original.Terminal?.Dispose();original.OwnerFile?.Dispose();original.Ui?.Dispose();original.BeforeFlush?.Dispose();original.EarnedFlush?.Dispose();original.LocalCachedSync=null;original.Delivery=null;original.TerminalBefore=original.TerminalBody=null;original.DeliveryRetention?.Dispose();original.TerminalRetention?.Dispose();}
        originals.Clear();descriptions.Clear();ReleaseRemoteBefores();ReleaseQueueHandoffs();RebirthProductiveEarnedUiRemote.ReleaseWorld();observedWorld=world;coldDiscovered=false;DiscoverCurrentOriginals();
    }
    private static void DiscoverCurrentOriginals()
    {
        // A dedicated authority has no owning local UI/account namespace.
        // Its authenticated remote originals retain their separate host holds.
        if(GameManager.IsDedicatedServer){coldDiscovered=true;coldUnknown=false;return;}
        if(GameManager.Instance?.World==null||string.IsNullOrEmpty(RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance.World))||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out _)){coldUnknown=true;return;}
        coldUnknown=HasUnresolvedOriginals();coldDiscovered=true;
    }
    private static bool HasUnresolvedOriginals()
    {
        try
        {
            var currentWorld=GameManager.Instance?.World;
            string worldKey=RebirthProductiveEarnedArtifact.WorldIdentity(currentWorld);
            if(currentWorld==null||string.IsNullOrEmpty(worldKey)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return true;
            // Scan only the actual current owner's original namespace. An old
            // save/account record cannot acquire custody of unrelated work.
            // This is discovery, never a source/action restoration authority.
            string root=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"RebirthProductiveEarnedUi",
                worldKey.Replace('/','_').Replace('+','-'),owner.StorageKey);
            if(!Safe(root))return true;if(!Directory.Exists(root))return false;
            var queue=new Queue<string>();queue.Enqueue(root);int entries=0;
            while(queue.Count>0)foreach(string path in Directory.EnumerateFileSystemEntries(queue.Dequeue()))
            {
                if(++entries>1024||!Safe(path))return true;
                if(Directory.Exists(path))
                {
                    string name=Path.GetFileName(path);
                    if(Guid.TryParseExact(name,"N",out _)&&TryArchiveHistoricalTerminal(path,worldKey,owner.StorageKey))continue;
                    if(name.EndsWith(".completed",StringComparison.Ordinal)&&Guid.TryParseExact(name.Substring(0,name.Length-10),"N",out _))
                    {if(File.Exists(Path.Combine(path,"queue.uqr.attempt"))&&!File.Exists(Path.Combine(path,"queue.uqr.accepted")))return true;continue;}
                    queue.Enqueue(path);
                }
                else return true; // No old artifact/absent stage proves unused.
            }
            return false;
        }
        catch{return true;}
    }
}



