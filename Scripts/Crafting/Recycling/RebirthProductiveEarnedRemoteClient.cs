using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Original-native-row client coordinator. Local stages are retry/custody evidence,
// never server publication/payment authority. Unknown attempted work is not replayed.
internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class Original
    {
        internal EntityPlayerLocal Actor;internal TileEntityWorkstation Station;internal World World;
        internal RecipeQueueItem Row;internal Recipe Recipe;internal RebirthProductiveEarnedRowProof.Frame Frame;
        internal byte[] Image;internal string Owner,Creation,WorldKey,Root,Path,Digest;internal int Deaths;
        internal Guid Nonce;internal bool Attempted,Applied,BeforeSent,AfterSent,Published,Recovered;
        internal bool HasReadFence;internal long ReadFence;
        internal NativeOutput Output;
        internal NativePending Pending;
        internal NativeTools Tools;
        internal bool ViewerToolsOnly;
        internal double NextQuery;internal RebirthProductiveEarnedObservationBudget Memory;
        internal bool Identity()=>ReferenceEquals(World,GameManager.Instance?.World)&&ReferenceEquals(Actor.world,World)&&!Actor.IsDead()&&Actor.Died==Deaths&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthProductiveEarnedArtifact.WorldIdentity(World)==WorldKey&&PathRoot()==Root&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&LockManager.Instance?.IsLockedByLocalPlayer(Station,0)==true&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(Actor,out var identity)&&identity.StorageKey==Owner&&
            Safe(Path)&&File.Exists(Path)&&!File.Exists(Path+".tmp")&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);
        internal bool Current()=>Identity()&&HasReadFence&&RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var fence)&&fence==ReadFence;
        private string PathRoot()=>System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir());
        internal bool RefreshProjection()
        {
            using(var allocation=Memory?.TryEnterCallbacks())
            {
            if(allocation==null||!Current()||Frame.RowIndex<0||Frame.RowIndex>=Station.Queue.Length)return false;
            var current=Station.Queue[Frame.RowIndex];if(current?.Recipe==null||!ReferenceEquals(current,Row)||!ReferenceEquals(current.Recipe,Recipe))return false;
            if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Frame.BeforeNative,Frame.BeforeElapsedOffset)&&
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Frame.AfterNative,Frame.AfterElapsedOffset))return false;
            return true; // Validation only. An actual typed echo producer must renew the original; equality never rebinds it.
            }
        }
        internal bool Before()
        {using(var allocation=Memory?.TryEnterCallbacks())return allocation!=null&&Current()&&RecipeCurrent()&&ReferenceEquals(Row.Recipe,Recipe)&&Row.Multiplier==Frame.Multiplier&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Frame.BeforeNative,Frame.BeforeElapsedOffset);}
        internal bool After()
        {using(var allocation=Memory?.TryEnterCallbacks())return allocation!=null&&Current()&&RecipeCurrent()&&ReferenceEquals(Row.Recipe,Recipe)&&Row.Multiplier==Frame.Multiplier&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Frame.AfterNative,Frame.AfterElapsedOffset);}
        private bool RecipeCurrent()=>Frame.HasRecipeDefinition&&RebirthProductiveEarnedUiQueueOwner.Binding(Row)==Frame.FullRecipeBinding&&
            RebirthRecipeOutputs.DefinitionIdentity(Recipe)==Frame.ExtrasDefinition;
    }
    private static readonly Dictionary<TileEntityWorkstation,Original> originals=new Dictionary<TileEntityWorkstation,Original>();
    [ThreadStatic] private static Original executing;
    private static World observedWorld;
    private static bool coldObserved,coldUnknown;
    internal static bool Blocks(TileEntityWorkstation station)=>RebirthProductiveEarnedCoordinator.Enabled&&(originals.ContainsKey(station)||
        nativeClosures.Values.Any(value=>value.Current()&&value.Record.Position.Equals(station.ToWorldPos())));
    internal static bool OwnsEarnedRow(TileEntityWorkstation station,RecipeQueueItem row)
    {
        try{return RebirthProductiveEarnedCoordinator.Enabled&&originals.TryGetValue(station,out var original)&&original.Attempted&&
            original.RefreshProjection()&&ReferenceEquals(original.Row,row)&&original.After();}catch{return false;}
    }
    internal static bool HasSavedEarned(TileEntityWorkstation station,RecipeQueueItem row)
    {
        try{return originals.TryGetValue(station,out var original)&&original.Published&&OwnsEarnedRow(station,row)&&
            MatchesStage(original,"published",original.Nonce);}catch{return false;}
    }
    internal static bool IsExecuting(TileEntityWorkstation station)
    {try{return executing!=null&&ReferenceEquals(executing.Station,station)&&executing.Current();}catch{return false;}}
    internal static bool Prepare(TileEntityWorkstation station,RecipeQueueItem row,ref float target,float worked,ItemStack[] tools,out RebirthProductiveEarnedCoordinator.Attempt attempt)
    {
        attempt=null;
        try{return PrepareCore(station,row,ref target,worked,tools,out attempt);}
        catch{return false;}
    }
    private static bool PrepareCore(TileEntityWorkstation station,RecipeQueueItem row,ref float target,float worked,ItemStack[] tools,out RebirthProductiveEarnedCoordinator.Attempt attempt)
    {
        attempt=null;
        if(!RebirthProductiveEarnedCoordinator.Enabled)return false;
        ObserveWorld();
        if(originals.TryGetValue(station,out var old))
        {
            // Only the update pump may re-enter the exact original native timer
            // write after a durable Attempted stage. Arbitrary future elapsed cannot.
            if(!ReferenceEquals(executing,old)||!old.Attempted||old.Applied||!old.Before()||!ReferenceEquals(old.Row,row)||
                old.Frame.WorkedSeconds!=worked||!RebirthProductiveEarnedArtifact.SameTools(tools,old.Frame.AfterTools))return false;
            target=RebirthNativeRowTimer(old.Frame.AfterRow);attempt=new RebirthProductiveEarnedCoordinator.Attempt{Station=station,Row=row,Recipe=row.Recipe,
                Multiplier=row.Multiplier,Target=target,Tools=tools,Frame=old.Frame};return true;
        }
        var world=GameManager.Instance?.World;var actor=world?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(world?.IsRemote()!=true||actor==null||actor.IsDead()||coldUnknown||!coldObserved||originals.Count>=4||station is RebirthCamperStationState||
            !RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||!RebirthRecyclerTransport.IsNativeOwner(station,actor)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var identity))return false;
        // Birth is obtained from the authenticated Describe reply for this exact
        // operation before any native hold, artifact, upload or wear is created.
        if(descriptions.ContainsKey(station)||descriptions.Count>=4)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            byte[] plannedTools=CopyTools(tools),beforeTools=CopyTools(station.Tools),beforeRow=RebirthProductiveEarnedRowProof.CaptureRow(row);
            string recipeBinding=RebirthProductiveEarnedUiQueueOwner.Binding(row),extrasDefinition=RebirthRecipeOutputs.DefinitionIdentity(row.Recipe);
            if(Encoding.UTF8.GetByteCount(recipeBinding)>RebirthProductiveEarnedRowProof.MaximumFrameBytes||
                Encoding.UTF8.GetByteCount(extrasDefinition)>RebirthProductiveEarnedRowProof.MaximumFrameBytes)return false;
            if(!RebirthProductiveEarnedObservationBudget.TryMeasureTools(plannedTools,out long graph,out _)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(graph+plannedTools.Length+beforeTools.Length+beforeRow.Length+
                    2L*(recipeBinding.Length+extrasDefinition.Length)+4096),out var allocation))return false;
            try
            {
                var operation=Guid.NewGuid();
                var pending=new Description{Allocation=allocation,Actor=actor,Station=station,Row=row,Recipe=row.Recipe,Operation=operation,Target=target==0?-float.Epsilon:target,
                    Worked=worked,Tools=ItemStack.Clone(tools),BeforeRow=beforeRow,BeforeTools=beforeTools,
                    World=world,Deaths=actor.Died,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),
                    RecipeBinding=recipeBinding,ExtrasDefinition=extrasDefinition};
                descriptions.Add(station,pending);allocation=null;RebirthProductiveEarnedWire.Install();
                RebirthProductiveEarnedWire.Send(actor,station,RebirthProductiveEarnedWire.Kind.Describe,operation,Guid.Empty,null,null);
            }
            finally{allocation?.Dispose();}
        }
        return false;
    }
    private sealed class Description
    {
        internal EntityPlayerLocal Actor;internal TileEntityWorkstation Station;internal RecipeQueueItem Row;internal Recipe Recipe;
        internal World World;internal Guid Operation;internal float Target,Worked;internal ItemStack[] Tools;internal byte[] BeforeRow,BeforeTools;
        internal int Deaths;internal string Creation;internal double NextTry;internal IDisposable Allocation;
        internal string RecipeBinding,ExtrasDefinition;
    }
    private static readonly Dictionary<TileEntityWorkstation,Description> descriptions=new Dictionary<TileEntityWorkstation,Description>();
    internal static bool HasPending(TileEntityWorkstation station)=>Blocks(station)||RebirthProductiveEarnedCoordinator.Enabled&&descriptions.ContainsKey(station);
    private static byte[] CopyTools(ItemStack[] tools)
    {if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(tools,262144,349528,out var text))throw new InvalidDataException();return Convert.FromBase64String(text);}
    private static float RebirthNativeRowTimer(byte[] bytes)
    {if(!RebirthNativeItemConformanceReader.TryDecodeStationQueue(bytes,out var rows)||rows.Length!=1)throw new InvalidDataException();return rows[0].CraftingTimeLeft;}
    private static void ObserveWorld()
    {
        var world=GameManager.Instance?.World;if(ReferenceEquals(world,observedWorld))return;
        foreach(var original in originals.Values){original.Tools?.Dispose();original.Pending?.Dispose();original.Output?.Dispose();original.Memory?.Dispose();}
        foreach(var description in descriptions.Values){description.Tools=null;description.BeforeRow=description.BeforeTools=null;description.RecipeBinding=description.ExtrasDefinition=null;description.Allocation?.Dispose();}
        originals.Clear();descriptions.Clear();nativeClosures.Clear();observedWorld=world;coldObserved=false;coldUnknown=false; // Disk originals never removed/terminalized here.
    }
    internal static void Update()
    {
        if(!RebirthProductiveEarnedCoordinator.Enabled)return;ObserveWorld();RecoverCurrentOwner();PumpViewerToolRequests();
        foreach(var request in descriptions.Values.ToArray())
        {
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                if(decoder==null)continue;
                double descriptionNow=DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond;
                if(descriptionNow<request.NextTry)continue;request.NextTry=descriptionNow+1;
                if(!ReferenceEquals(request.World,observedWorld)||request.Actor.IsDead()||request.Actor.Died!=request.Deaths||
                    RebirthCamperCharacterAccess.CurrentCreation(request.Actor)!=request.Creation||
                    !ReferenceEquals(request.Row.Recipe,request.Recipe)||RebirthProductiveEarnedUiQueueOwner.Binding(request.Row)!=request.RecipeBinding||
                    RebirthRecipeOutputs.DefinitionIdentity(request.Row.Recipe)!=request.ExtrasDefinition||
                    !RebirthProductiveEarnedRowProof.CaptureRow(request.Row).SequenceEqual(request.BeforeRow)||
                    !RebirthProductiveEarnedArtifact.SameTools(request.Station.Tools,request.BeforeTools))continue;
                if(!RebirthProductiveEarnedWire.TryReadReply(request.Operation,out var reply)||reply.State!=RebirthProductiveEarnedWire.Status.Described||!reply.Position.Equals(request.Station.ToWorldPos()))
                {RebirthProductiveEarnedWire.Send(request.Actor,request.Station,RebirthProductiveEarnedWire.Kind.Describe,request.Operation,Guid.Empty,null,null);continue;}
                if(!RebirthProductiveEarnedNativeBody.TryCapturePlan(request.Station,request.Tools,request.Station.Queue.Length-1,request.Target,
                    out var before,out var after,out int beforeOffset,out int afterOffset))continue;
                var afterRow=new RecipeQueueItem{Recipe=request.Row.Recipe,Multiplier=request.Row.Multiplier,CraftingTimeLeft=request.Target,
                    IsCrafting=request.Row.IsCrafting,Quality=request.Row.Quality,StartingEntityId=request.Row.StartingEntityId,OneItemCraftTime=request.Row.OneItemCraftTime,
                    RepairItem=request.Row.RepairItem,AmountToRepair=request.Row.AmountToRepair};
                var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=reply.Birth,Operation=request.Operation,World=reply.World,RowIndex=request.Station.Queue.Length-1,
                    Multiplier=request.Row.Multiplier,WorkedSeconds=request.Worked,BeforeRow=request.BeforeRow,AfterRow=RebirthProductiveEarnedRowProof.CaptureRow(afterRow),
                    BeforeTools=request.BeforeTools,AfterTools=CopyTools(request.Tools),BeforeNative=before,AfterNative=after,BeforeElapsedOffset=beforeOffset,AfterElapsedOffset=afterOffset};
                frame.FullRecipeBinding=request.RecipeBinding;frame.ExtrasDefinition=request.ExtrasDefinition;
                if(!decoder.Admit(frame,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||
                    !RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _))continue;
                byte[] image=RebirthProductiveEarnedRowProof.Encode(frame);string root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir());
                string path=System.IO.Path.Combine(root,"RebirthProductiveEarnedOwner",reply.World.Replace('/','_').Replace('+','-'),reply.Owner,frame.Operation.ToString("N")+".rew");
                var original=new Original{Actor=request.Actor,Station=request.Station,World=request.World,Row=request.Row,Recipe=request.Recipe,Frame=frame,Image=image,
                    Owner=reply.Owner,Creation=request.Creation,WorldKey=reply.World,Deaths=request.Deaths,Root=root,Path=path,Digest=Hash(image)};
                if(!RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(original.World,original.Station,out original.ReadFence))continue;
                original.HasReadFence=true;
                original.Memory=RebirthProductiveEarnedObservationBudget.TryOpenOwner(request.Actor,frame,image);
                if(original.Memory==null)continue;
                if(!WriteExact(path,image,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||!WriteExact(path+".scope",Scope(original),2048)||!original.Before())
                {original.Memory.Dispose();original.Memory=null;continue;}
                originals.Add(request.Station,original);descriptions.Remove(request.Station);
                request.Tools=null;request.BeforeRow=request.BeforeTools=null;request.RecipeBinding=request.ExtrasDefinition=null;request.Allocation?.Dispose();request.Allocation=null;
                }
            }
            catch{ /* Retain exact description; no new work or operation. */ }
        }
        double now=DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond;
        PumpNativeClosures(now);
        PumpViewerToolsClosures(now);
        foreach(var original in originals.Values.ToArray())
        {
            try
            {
                if(now<original.NextQuery)continue;
                if(original.ViewerToolsOnly){PumpViewerTools(original,now);continue;}
                if(PumpNativeClosed(original,now))continue;
                if(!original.Current())
                {
                    if(original.Identity()&&original.Nonce!=Guid.Empty)
                    {original.NextQuery=now+1;RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.Query,
                        original.Frame.Operation,original.Nonce,null,original.Digest,true);}
                    continue;
                }
                if(PumpNativeOutput(original,now))continue;
                if(!original.RefreshProjection())continue;original.NextQuery=now+1;
                if(original.Recovered)
                {
                    if(!LoadStages(original))continue;
                    // Cold recovery only queries. Neither matching live Before nor
                    // an absent stage file proves an old native attempt never ran.
                    if(RebirthProductiveEarnedWire.TryReadReply(original.Frame.Operation,out var restored)&&restored.State==RebirthProductiveEarnedWire.Status.Published&&
                        restored.Nonce==original.Nonce&&restored.Digest==original.Digest&&restored.Birth==original.Frame.Birth&&restored.Owner==original.Owner&&
                        restored.World==original.WorldKey&&restored.Creation==original.Creation&&restored.Deaths==original.Deaths&&
                        restored.Position.Equals(original.Station.ToWorldPos())&&original.Attempted&&original.Applied&&original.AfterSent&&original.After()&&Stage(original,"published",original.Nonce))original.Published=true;
                    if(original.Nonce==Guid.Empty)
                    {
                        if(RebirthProductiveEarnedWire.TryReadReply(original.Frame.Operation,out var retained)&&retained.State==RebirthProductiveEarnedWire.Status.Held&&
                            retained.Digest==original.Digest&&retained.Birth==original.Frame.Birth&&retained.Owner==original.Owner&&retained.Creation==original.Creation&&
                            retained.World==original.WorldKey&&retained.Deaths==original.Deaths&&retained.Position.Equals(original.Station.ToWorldPos())&&Stage(original,"accepted",retained.Nonce))original.Nonce=retained.Nonce;
                        else RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.ReadHold,original.Frame.Operation,Guid.Empty,null,original.Digest);
                    }
                    if(original.Nonce!=Guid.Empty)RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.Query,
                        original.Frame.Operation,original.Nonce,null,original.Digest);
                    continue;
                }
                if(!LoadStages(original))continue;
                if(!RebirthProductiveEarnedWire.TryReadReply(original.Frame.Operation,out var reply)||reply.Digest!=original.Digest||reply.Birth!=original.Frame.Birth||
                    reply.Creation!=original.Creation||reply.Owner!=original.Owner||reply.World!=original.WorldKey||reply.Deaths!=original.Deaths||!reply.Position.Equals(original.Station.ToWorldPos()))
                {if(!original.Attempted&&original.Before())RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.Hold,original.Frame.Operation,Guid.Empty,original.Image,original.Digest);continue;}
                if(reply.State==RebirthProductiveEarnedWire.Status.Held)
                {
                    if(!Stage(original,"accepted",reply.Nonce))continue;
                    original.Nonce=reply.Nonce;
                    if(!original.Attempted&&original.Before())RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.Prepare,original.Frame.Operation,original.Nonce,original.Image,original.Digest);
                }
                else if(reply.Nonce!=original.Nonce||original.Nonce==Guid.Empty)continue;
                else if(reply.State==RebirthProductiveEarnedWire.Status.Header&&!original.BeforeSent&&!original.Attempted&&original.Before())
                {
                    if(!CanUpload(original)||!Stage(original,"beforeUpload",original.Nonce))continue;original.BeforeSent=true;
                    Upload(original); // Failure/uncertain send never clears the durable original attempt.
                }
                else if(reply.State==RebirthProductiveEarnedWire.Status.Prepared&&!original.Attempted&&original.Before())
                {
                    using(var allocation=original.Memory?.TryEnterCallbacks())
                    {
                        if(allocation==null||!original.Before())continue;
                        var beforeTimer=RebirthNativeRowTimer(original.Frame.BeforeRow);var afterTimer=RebirthNativeRowTimer(original.Frame.AfterRow);
                        if(beforeTimer<=0||afterTimer>0)continue;
                        if(!Stage(original,"attempt",original.Nonce))continue;original.Attempted=true;
                        executing=original;
                        try{RebirthRecyclerTransport.ExecuteEarnedOriginal(original.Actor,original.Station,()=>original.Station.HandleRecipeQueue(beforeTimer-afterTimer));}
                        finally{executing=null;}
                    }
                }
                else if(reply.State==RebirthProductiveEarnedWire.Status.Published&&original.Attempted&&original.After())
                {if(Stage(original,"published",original.Nonce))original.Published=true;}
                if(original.Attempted&&original.After()&&!original.AfterSent)
                {
                    if(!CanUpload(original)||!Stage(original,"applied",original.Nonce)||!Stage(original,"afterUpload",original.Nonce))continue;
                    original.Applied=true;original.AfterSent=true;Upload(original);
                }
                RebirthProductiveEarnedWire.Send(original.Actor,original.Station,RebirthProductiveEarnedWire.Kind.Query,original.Frame.Operation,original.Nonce,null,original.Digest);
            }
            catch{ /* Attempted/unknown is retained. No rewear, replan or stale whole-TE resend. */ }
        }
    }
    private static void RecoverCurrentOwner()
    {
        if(coldObserved||observedWorld?.IsRemote()!=true)return;
        var actor=observedWorld.GetPrimaryPlayer() as EntityPlayerLocal;
        if(actor==null||actor.IsDead()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var identity))return;
        string world=RebirthProductiveEarnedArtifact.WorldIdentity(observedWorld),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        if(world==null)return;
        coldObserved=true;
        string root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir());
        string folder=System.IO.Path.Combine(root,"RebirthProductiveEarnedOwner",world.Replace('/','_').Replace('+','-'),identity.StorageKey);
        try
        {
            if(!Safe(folder))throw new InvalidDataException();if(!Directory.Exists(folder))return;
            var files=new List<string>();int count=0;long bytes=0;
            foreach(string path in Directory.EnumerateFileSystemEntries(folder))
            {
                if(!Safe(path)||Directory.Exists(path))throw new InvalidDataException();
                if(path.EndsWith(".rew.tmp",StringComparison.Ordinal))throw new InvalidDataException();
                if(!path.EndsWith(".rew",StringComparison.Ordinal))continue;
                if(RecoverNativeClose(path,actor,world,identity.StorageKey,creation,root))continue;
                if(++count>4)throw new InvalidDataException();
                long length=new FileInfo(path).Length;
                if(length<1||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||(bytes+=length)>4L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes)throw new InvalidDataException();
                files.Add(path);
            }
            if(files.Count>4)throw new InvalidDataException();
            foreach(string path in files)
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                if(decoder==null){coldObserved=false;return;} // Retry exact scan after pressure, not new work.
                byte[] image=Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);var frame=RebirthProductiveEarnedRowProof.Decode(image);
                if(!frame.HasNativeBodies||!decoder.Admit(frame,image.Length)||!RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _)||
                    frame.World!=world||System.IO.Path.GetFileName(path)!=frame.Operation.ToString("N")+".rew")throw new InvalidDataException();
                byte[] scope=Read(path+".scope",2048);Vector3i position;int deaths;
                using(var stream=new MemoryStream(scope,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
                {
                    if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=identity.StorageKey||
                        RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512)!=creation)throw new InvalidDataException();
                    deaths=reader.ReadInt32();if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44)!=world)throw new InvalidDataException();
                    position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32());
                    if(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Hash(image)||stream.Position!=stream.Length)throw new InvalidDataException();
                }
                // A different life stays retained on disk and blocks fresh owner
                // admission; no rebasing it onto the new-life native inventory.
                if(deaths!=actor.Died){coldUnknown=true;continue;}
                var station=observedWorld.GetTileEntity(position) as TileEntityWorkstation;
                if(station==null||frame.RowIndex>=station.Queue.Length)
                {if(!RecoverNativeAdoptionQuery(path,actor,world,identity.StorageKey,creation,root,decoder,frame,image)){if(!coldObserved)return;coldUnknown=true;}continue;}
                if(originals.TryGetValue(station,out var previous))
                {if(previous.Path!=path||!previous.Image.SequenceEqual(image)||!previous.Current())coldUnknown=true;continue;}
                var row=station.Queue[frame.RowIndex];
                if(row?.Recipe==null||frame.HasRecipeDefinition&&RebirthProductiveEarnedUiQueueOwner.Binding(row)!=frame.FullRecipeBinding)
                {if(!RecoverNativeAdoptionQuery(path,actor,world,identity.StorageKey,creation,root,decoder,frame,image)){if(!coldObserved)return;coldUnknown=true;}continue;}
                var original=new Original{Actor=actor,Station=station,World=observedWorld,Row=row,Recipe=row.Recipe,Frame=frame,Image=image,
                    Owner=identity.StorageKey,Creation=creation,WorldKey=world,Deaths=deaths,Root=root,Path=path,Digest=Hash(image),Recovered=true};
                if(!scope.SequenceEqual(Scope(original))||!LoadStages(original))throw new InvalidDataException();
                original.Memory=RebirthProductiveEarnedObservationBudget.TryOpenOwner(actor,frame,image);
                if(original.Memory==null){coldObserved=false;return;}
                // Description only; actual native echo/read is still required
                // before current owner upload or continuation is possible.
                if(!LoadNativeOutputDescription(original,decoder)||!LoadNativeToolsDescription(original,decoder)||!LoadNativePendingDescription(original,decoder)){original.Memory.Dispose();original.Output?.Dispose();original.Tools?.Dispose();original.Pending?.Dispose();throw new InvalidDataException();}
                originals.Add(station,original);
                }
            }
        }
        catch{coldUnknown=true;} // Preserve known exact contexts; refuse new admission.
    }
    internal static void Applied(RebirthProductiveEarnedCoordinator.Attempt attempt)
    {
        try
        {
            if(attempt==null||!originals.TryGetValue(attempt.Station,out var original)||!ReferenceEquals(executing,original)||!original.Attempted||
                original.Frame.Operation!=attempt.Frame.Operation||!original.After())return;
            original.Applied=Stage(original,"applied",original.Nonce);
        }
        catch{ /* Actual native wear already happened; retain unknown, never retry it. */ }
    }
    private static bool CanUpload(Original original)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
        return original.Current()&&manager?.IsClient==true&&!manager.IsServer&&RebirthLogisticsOriginalTransport.CanSendNative(connection)&&
            (original.Attempted?original.After():original.Before());
    }
    private static void Upload(Original original)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
        if(!original.Current()||manager?.IsClient!=true||manager.IsServer||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
        // Actual live native serialization only, after immutable exact full-body
        // correlation and durable upload-attempt stage. No replayed caller frame.
        bool after=original.Attempted;
        if(after?!original.After():!original.Before())return;
        using(var allocation=original.Memory?.TryEnterCallbacks())
        {if(allocation==null||(after?!original.After():!original.Before()))return;
         manager.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(original.Station,StreamModeWrite.ToServer));}
    }
    private static byte[] Scope(Original original)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {writer.Write((byte)1);writer.Write(original.Owner);writer.Write(original.Creation);writer.Write(original.Deaths);writer.Write(original.WorldKey);
         var position=original.Station.ToWorldPos();writer.Write(position.x);writer.Write(position.y);writer.Write(position.z);writer.Write(original.Digest);writer.Flush();return stream.ToArray();}
    }
    private static bool Stage(Original original,string stage,Guid nonce)
    {
        if(nonce==Guid.Empty||!original.Current()||!File.Exists(original.Path+".scope")||!Read(original.Path+".scope",2048).SequenceEqual(Scope(original)))return false;
        return WriteExact(original.Path+"."+stage,StageBytes(original,stage,nonce),2048);
    }
    private static byte[] StageBytes(Original original,string stage,Guid nonce)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write((byte)1);writer.Write(original.Frame.Operation.ToByteArray());writer.Write(nonce.ToByteArray());writer.Write(original.Digest);writer.Write(stage);writer.Flush();return stream.ToArray();}
    }
    private static bool MatchesStage(Original original,string stage,Guid nonce)
    {
        return original.Current()&&MatchesSavedStageDescription(original,stage,nonce);
    }
    private static bool MatchesSavedStageDescription(Original original,string stage,Guid nonce)
    {
        string path=original.Path+"."+stage;
        return nonce!=Guid.Empty&&original.Identity()&&Safe(path)&&File.Exists(path)&&!File.Exists(path+".tmp")&&Read(path,2048).SequenceEqual(StageBytes(original,stage,nonce));
    }
    private static bool LoadStages(Original original)
    {
        if(!Safe(original.Path)||!Read(original.Path+".scope",2048).SequenceEqual(Scope(original)))return false;
        string[] candidates={"scope","accepted","beforeUpload","attempt","applied","afterUpload","published"};
        if(candidates.Any(name=>File.Exists(original.Path+"."+name+".tmp")))return false;
        string accepted=original.Path+".accepted";
        if(!File.Exists(accepted))return original.Nonce==Guid.Empty&&!new[]{"beforeUpload","attempt","applied","afterUpload","published"}.Any(name=>File.Exists(original.Path+"."+name));
        using(var stream=new MemoryStream(Read(accepted,2048),false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadByte()!=1||new Guid(reader.ReadBytes(16))!=original.Frame.Operation)return false;
            Guid nonce=new Guid(reader.ReadBytes(16));if(nonce==Guid.Empty||reader.ReadString()!=original.Digest||reader.ReadString()!="accepted"||stream.Position!=stream.Length)return false;
            if(original.Nonce!=Guid.Empty&&original.Nonce!=nonce)return false;original.Nonce=nonce;
        }
        string[] names={"beforeUpload","attempt","applied","afterUpload","published"};
        foreach(string name in names)if(File.Exists(original.Path+"."+name)&&!MatchesSavedStageDescription(original,name,original.Nonce))return false;
        original.BeforeSent=File.Exists(original.Path+".beforeUpload");original.Attempted=File.Exists(original.Path+".attempt");
        original.Applied=File.Exists(original.Path+".applied");original.AfterSent=File.Exists(original.Path+".afterUpload");original.Published=File.Exists(original.Path+".published");
        return (!original.Applied||original.Attempted)&&(!original.AfterSent||original.Applied)&&(!original.Published||original.AfterSent);
    }
    private static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static bool WriteExact(string path,byte[] bytes,int maximum)
    {
        if(bytes==null||bytes.Length>maximum||!Safe(path)||File.Exists(path+".tmp"))return false;
        if(File.Exists(path))return Read(path,maximum).SequenceEqual(bytes);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));if(!Safe(path))return false;
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!Read(path+".tmp",maximum).SequenceEqual(bytes))return false;File.Move(path+".tmp",path);return Read(path,maximum).SequenceEqual(bytes);
    }
    private static byte[] Read(string path,int maximum)
    {
        if(!Safe(path))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(stream.Length<1||stream.Length>maximum)throw new InvalidDataException();var bytes=new byte[(int)stream.Length];int offset=0;
         while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<1)throw new EndOfStreamException();offset+=read;}if(stream.ReadByte()!=-1)throw new InvalidDataException();return bytes;}
    }
    private static bool Safe(string path)
    {
        try{string current=System.IO.Path.GetFullPath(path);while(!string.IsNullOrEmpty(current))
        {if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;
         string parent=System.IO.Path.GetDirectoryName(current);if(parent==current)break;current=parent;}return true;}catch{return false;}
    }
}

