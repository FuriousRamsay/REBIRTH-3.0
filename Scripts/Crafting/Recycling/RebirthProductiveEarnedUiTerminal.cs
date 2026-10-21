using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    [ThreadStatic]private static Original terminalSync;
    private static bool CurrentDetached(Original original)
    {
        try
        {
            var b=original.Binding;
            return original.Deferred!=null&&original.OwnerFile?.Current()==true&&original.EarlierOwner?.SourceCurrent()==true&&
                ReferenceEquals(GameManager.Instance?.World,b.World)&&ReferenceEquals(b.World.GetTileEntity(b.Position),b.Station)&&
                b.World.GetBlock(b.Position).type==b.BlockType&&RebirthProductiveEarnedArtifact.WorldIdentity(b.World)==b.WorldKey&&
                b.SaveDirectory==Path.GetFullPath(GameIO.GetSaveGameDir())&&b.PlayerDirectory==Path.GetFullPath(GameIO.GetPlayerDataDir())&&
                (b.World.IsRemote()?RebirthProductiveEarnedWire.TryReadReply(b.Operation,out var reply)&&reply.Birth==b.Birth&&reply.QueueOwner==b.OwnerPurpose:
                    RebirthLogisticsEndpointIdentity.TryGetIncarnation(b.World,b.Station,out var birth)&&birth==b.Birth.ToString("N"));
        }
        catch{return false;}
    }
    private static bool CurrentDelivered(Original original)
    {
        try
        {
            var b=original.Binding;var cards=b.Queue.GetRecipesToCraft();var after=original.Delivery;
            return original.OwnerFile?.Current()==true&&ReferenceEquals(GameManager.Instance?.World,b.World)&&
                b.Station.ToWorldPos().Equals(b.Position)&&b.World.GetBlock(b.Position).type==b.BlockType&&
                RebirthProductiveEarnedArtifact.WorldIdentity(b.World)==b.WorldKey&&ReferenceEquals(b.World.GetTileEntity(b.Station.ToWorldPos()),b.Station)&&
                b.Window.WindowGroup?.isShowing==true&&ReferenceEquals(b.Window.WorkstationData.TileEntity,b.Station)&&ReferenceEquals(b.Window.craftingQueue,b.Queue)&&
                b.Index>=0&&b.Index<cards.Length&&ReferenceEquals(cards[b.Index],b.Card)&&cards.Count(value=>ReferenceEquals(value,b.Card))==1&&
                b.Card.GetRecipeCount()==(int)after.Attribute("afterCount")&&LockManager.Instance?.IsLockedByLocalPlayer(b.Station,0)==true&&
                Grid(b.Window.outputWindow?.GetSlots()??b.Station.Output)==(string)after.Element("outputAfter")&&
                Grid(b.Actor.bag.ItemGrid.items)==(string)after.Element("bagAfter")&&Grid(b.Actor.inventory.ItemGrid.items)==(string)after.Element("beltAfter");
        }
        catch{return false;}
    }
    private static string Grid(ItemStack[] cells)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(cells));
    private static RecipeQueueItem UiRow(XUiC_RecipeStack card)=>new RecipeQueueItem{Recipe=card.GetRecipe(),Multiplier=checked((short)card.GetRecipeCount()),
        CraftingTimeLeft=card.GetRecipeCraftingTimeLeft(),IsCrafting=card.IsCrafting,Quality=checked((byte)card.OutputQuality),StartingEntityId=card.StartingEntityId,
        OneItemCraftTime=card.GetOneItemCraftTime()};
    internal static void Update()
    {
        ObserveWorld();
        PumpQueueHandoffs();
        if(!RebirthProductiveEarnedUiOriginal.Enabled)return;
        RebirthProductiveEarnedUiQueueOwner.Pump();
        foreach(var original in originals.Values.ToArray())
        {
            try
            {
                if(original.TerminalSaved){ArchiveCompleted(original);continue;}
                if(!original.Delivered||!original.Current())continue;
                if(original.StationPublished)
                {
                    if(original.Binding.World.IsRemote()){ObserveRemoteTerminal(original);continue;}
                    if(original.Deferred?.Saved()!=true)continue;
                    // The saved aggregate at StationPublished predates real
                    // native receipt consumption. Publish the actual removed
                    // receipt image separately before retiring the original.
                    if(original.Terminal==null)
                    {
                        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                        {
                            var station=original.Binding.Station;
                            if(decoder==null||station.Queue==null||station.Queue.Length==0||station.Queue[0]==null||
                                !RebirthProductiveEarnedNativeBody.TryCapturePlan(station,station.Tools,0,station.Queue[0].CraftingTimeLeft,
                                    out var consumed,out _,out int offset,out _))continue;
                            original.Terminal=BeginPublication(original,"deferredConsumed",consumed,offset);
                        }
                    }
                    if(original.Terminal?.Saved()==true&&Stage(original,"TerminalSaved"))
                    {original.TerminalSaved=true;original.Terminal.Dispose();original.Terminal=null;ArchiveCompleted(original);}continue;
                }
                if(!original.TerminalSyncAttempted)
                {
                    using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    {
                        if(decoder==null||!original.Current())continue;var b=original.Binding;
                        if(!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(b.Station,b.Queue.GetRecipesToCraft().Select(UiRow).ToArray(),
                            b.Window.inputWindow?.GetSlots(),b.Window.outputWindow?.GetSlots(),b.Window.fuelWindow?.GetSlots(),b.Window.toolWindow?.GetSlots(),
                            out var terminalBefore,out var terminalBody,out int beforeOffset,out int afterOffset))continue;
                        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(terminalBefore.Length+terminalBody.Length)),out var terminalRetention))continue;
                        original.TerminalBefore=terminalBefore;original.TerminalBody=terminalBody;original.TerminalBeforeOffset=beforeOffset;original.TerminalOffset=afterOffset;
                        original.TerminalRetention?.Dispose();original.TerminalRetention=terminalRetention;
                        var intent=new XElement("uiTerminalOriginal",new XAttribute("operation",b.Operation.ToString("N")),new XAttribute("uio",original.Digest),
                            new XAttribute("beforeOffset",original.TerminalBeforeOffset),new XAttribute("afterOffset",original.TerminalOffset),
                            new XElement("beforeNative",Convert.ToBase64String(original.TerminalBefore)),new XElement("afterNative",Convert.ToBase64String(original.TerminalBody)),
                            new XElement("ownerAfter",Convert.ToBase64String(original.OwnerFile.After)),
                            new XElement("delivery",new XElement(original.Delivery)));
                        if(original.Deferred!=null)
                        {if(!RebirthProductiveEarnedUiQueueOwner.TryCopyDeferredContribution(original.Deferred.Receipt,out var contribution))continue;intent.Add(contribution);}
                        if(!WriteExact(original.Path+".TerminalOriginal",Encoding.UTF8.GetBytes(intent.ToString(SaveOptions.DisableFormatting)))||!Stage(original,"TerminalSyncAttempted"))continue;
                        original.TerminalSyncAttempted=true;
                    }
                    terminalSync=original;
                    try{original.Binding.Window.syncTEfromUI();}
                    finally{terminalSync=null;}
                }
                if(!original.TerminalSynced)continue;
                if(original.Binding.World.IsRemote()){ObserveRemoteTerminal(original);continue;}
                if(original.Terminal==null)original.Terminal=BeginPublication(original,"terminal",original.TerminalBody,original.TerminalOffset);
                if(original.Terminal?.Saved()!=true||!RetainViewerSaved(original))continue;
                if(original.Deferred!=null)
                {if(Stage(original,"StationPublished")){original.StationPublished=true;original.Terminal.Dispose();original.Terminal=null;}continue;}
                if(!Stage(original,"TerminalSaved"))continue;
                original.TerminalSaved=true;original.Terminal.Dispose();original.Terminal=null;
                // No replay, refund, or copied output. This actual original is
                // complete only after both native station and owner publication.
                ArchiveCompleted(original);
            }
            catch{/* Original attempt and native artifacts remain owned on uncertainty. */}
        }
    }
    internal static bool EnterTerminalSync(XUiC_WorkstationWindowGroup window,bool run,out object state)
    {
        state=null;var original=terminalSync;if(original==null)return true;
        if(!run||!ReferenceEquals(window,original.Binding.Window)||!original.Current()||!original.TerminalSyncAttempted)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Binding.Station,original.TerminalBefore,original.TerminalBeforeOffset))return false;}
        if(!RebirthProductiveEarnedUiOwnerFile.EnterLocalCachedUiSync(window,original,out original.LocalCachedSync))return false;
        state=original;return true;
    }
    internal static void ObserveTerminalSync(XUiC_WorkstationWindowGroup window,bool run,object state)
    {
        try
        {
            if(!run||state is not Original original||!ReferenceEquals(terminalSync,original)||!ReferenceEquals(original.Binding.Window,window)||!original.Current()||
                original.Binding.Station.lastTickTime!=GameTimer.Instance.ticks||original.Binding.World.IsRemote()&&(original.TerminalAutomaticSends!=1||original.TerminalAutomaticInvalid))return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Binding.Station,original.TerminalBody,original.TerminalOffset))return;}
            RebirthProductiveEarnedUiOwnerFile.ObserveLocalCachedUiSync(window,original,original.LocalCachedSync);
            original.LocalCachedSync=null;
            if(Stage(original,"TerminalSynced"))original.TerminalSynced=true;
        }
        catch{}
    }
    private static void ObserveRemoteTerminal(Original original)
    {
        bool replyCurrent=RebirthProductiveEarnedWire.TryReadReply(original.Binding.Operation,out var reply)&&reply.Operation==original.Binding.Operation&&
            reply.Nonce==original.Nonce&&reply.Digest==original.Digest&&reply.Birth==original.Binding.Birth&&reply.Owner==original.Binding.Owner&&
            reply.Creation==original.Binding.Creation&&reply.Deaths==original.Binding.Deaths&&reply.World==original.Binding.WorldKey&&
            reply.Position.Equals(original.Binding.Station.ToWorldPos());
        if(!replyCurrent||reply.State!=RebirthProductiveEarnedWire.Status.UiTerminalHeader&&reply.State!=RebirthProductiveEarnedWire.Status.UiTerminalSaved&&reply.State!=RebirthProductiveEarnedWire.Status.UiTerminalStationSaved)
        {
            double now=Monotonic();if(now<original.NextTerminal)return;original.NextTerminal=now+0.5;
            var info=new FileInfo(original.Path+".TerminalOriginal");if(info.Length<1||info.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return;
            var bytes=File.ReadAllBytes(info.FullName);
            RebirthProductiveEarnedWire.Send(original.Binding.Actor,original.Binding.Station,RebirthProductiveEarnedWire.Kind.UiTerminal,
                original.Binding.Operation,original.Nonce,bytes,Hash(bytes));return;
        }
        if(reply.State==RebirthProductiveEarnedWire.Status.UiTerminalSaved)
        {
            if(!RetainViewerSaved(original)||!Stage(original,"TerminalSaved"))return;
            original.TerminalSaved=true;ArchiveCompleted(original);return;
        }
        if(reply.State==RebirthProductiveEarnedWire.Status.UiTerminalStationSaved)
        {
            if(original.Deferred==null||!RetainViewerSaved(original))return;
            if(!original.StationPublished&&Stage(original,"StationPublished"))original.StationPublished=true;
            QueryUi(original);return;
        }
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!File.Exists(original.Path+".TerminalUploadAttempted")&&manager?.IsClient==true&&!manager.IsServer&&
            RebirthLogisticsOriginalTransport.CanSendNative(manager.connectionToServer?[0])&&Stage(original,"TerminalUploadAttempted"))
        {
            if(!TryGetTerminalExplicitHandle(original,out byte handle))return;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(original.Binding.Station,StreamModeWrite.ToServer,handle));
            manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(original.Binding.Actor));
        }
        QueryUi(original);
    }
    private static void ArchiveCompleted(Original original)
    {
        if(!original.TerminalSaved||!original.Current()||!SealHistoricalTerminal(original))return;
        string directory=Path.GetDirectoryName(original.Path),retained=directory+".completed";
        string root=Path.GetFullPath(Path.Combine(GameIO.GetPlayerDataDir(),"RebirthProductiveEarnedUi"));
        if(!Path.GetFullPath(directory).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
            Path.GetFileName(directory)!=original.Binding.Operation.ToString("N")||!Safe(directory)||!Safe(retained)||Directory.Exists(retained))return;
        RetainQueueHandoff(original,retained);
        Directory.Move(directory,retained);
        RebirthProductiveEarnedWire.ReleaseSavedUiStatus(original.Binding.Operation,original.Nonce);
        if(descriptions.TryGetValue(original.Binding.Card,out var description)&&description.Operation==original.Binding.Operation)
            descriptions.Remove(original.Binding.Card);
        originals.Remove(original.Binding.Card);ReleaseRemoteBefore(original);original.Deferred?.Receipt?.Dispose();original.Deferred?.Dispose();original.OwnerFile.Dispose();original.Ui.Dispose();original.BeforeFlush.Dispose();original.EarnedFlush?.Dispose();
        original.LocalCachedSync=null;original.Delivery=null;original.TerminalBefore=original.TerminalBody=null;original.DeliveryRetention?.Dispose();original.TerminalRetention?.Dispose();
    }
    private static bool RetainViewerSaved(Original original)
    {
        if(original.ViewerSaved)return original.Current();
        if(original.OwnerFile?.Saved()!=true)return false;
        var receipt=new XElement("nativeViewerFilePublished",new XAttribute("operation",original.Binding.Operation.ToString("N")),
            new XAttribute("uio",original.Digest),new XAttribute("path",original.OwnerFile.FinalPath),new XAttribute("native",Hash(original.OwnerFile.Final)));
        if(!WriteExact(original.Path+".ViewerSaved",Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting))))return false;
        original.ViewerSaved=true;return true;
    }
}

