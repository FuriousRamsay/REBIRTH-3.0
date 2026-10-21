using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiRemote
{
    private static readonly object terminalIssuer=new object();
    internal sealed class TerminalPublication
    {
        private readonly Registration original;private readonly Publication publication;
        private readonly RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication deferred;
        private readonly RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication localOwner;
        internal TerminalPublication(object issuer,Registration value,Publication proof)
        {if(!ReferenceEquals(issuer,terminalIssuer))throw new InvalidOperationException();original=value;publication=proof;}
        internal TerminalPublication(object issuer,Registration value,RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof)
        {if(!ReferenceEquals(issuer,terminalIssuer))throw new InvalidOperationException();original=value;deferred=proof;}
        internal TerminalPublication(object issuer,Registration value,RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication proof)
        {if(!ReferenceEquals(issuer,terminalIssuer))throw new InvalidOperationException();original=value;localOwner=proof;}
        internal Guid Operation=>original.Frame.Operation;internal Guid Birth=>original.Frame.Birth;internal Guid Nonce=>original.Nonce;
        internal TileEntityWorkstation Station=>original.Station;
        internal bool Matches(TileEntityWorkstation station)=>ReferenceEquals(original.Station,station)&&
            (localOwner!=null?localOwner.Contributions.Any(value=>ReferenceEquals(value,original.Deferred))&&localOwner.Current()&&original.StationPublication?.Current()==true:
                deferred!=null?ReferenceEquals(deferred.Original,original.Deferred)&&deferred.Current()&&original.StationPublication?.Current()==true:
                original.Current()&&original.Terminal.Owner.Saved()&&publication.Current())&&
            RebirthProductiveEarnedArtifact.FileMatches(original.Path+".TerminalSaved",original.TerminalReceipt);
    }
    // Only the actual saved recipient/removal producer can construct this
    // input. Parsed terminal XML and an initial station save cannot call it.
    internal static bool AcceptDeferredTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof)
    {
        try
        {
        if(proof==null||!ThreadManager.IsMainThread()||!proof.Current()||
            !registrations.TryGetValue(proof.Operation,out var original)||!ReferenceEquals(original.Deferred,proof.Original)||
            original.Nonce!=proof.Nonce||original.Frame.Birth!=proof.Birth||!ReferenceEquals(original.Station,proof.Station)||
            original.StationPublication?.Current()!=true)return false;
        var node=new XElement("uiNativeDeferredTerminalSaved",new XAttribute("operation",proof.Operation.ToString("N")),
            new XAttribute("nonce",proof.Nonce.ToString("N")),new XAttribute("birth",proof.Birth.ToString("N")),new XAttribute("uio",original.Hash));
        byte[] bytes=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
        if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(original.Path+".TerminalSaved",bytes)||!proof.Current())return false;
        original.TerminalReceipt=bytes;
        if(!RebirthProductiveEarnedRemoteHost.RetireUiTerminal(new TerminalPublication(terminalIssuer,original,proof)))return false;
        original.Completed=true;original.Memory?.RetainCompletedAllocation();RebirthProductiveEarnedUiRemoteBefore.Release(original.Frame.Operation);return true;
        }
        catch{return false;}
    }
    private sealed class Upload
    {
        internal Registration Original;internal NetPackageTileEntity Packet;internal World World;
        internal int Reads;internal bool ReadCompleted;internal ulong Elapsed;internal IDisposable Allocation;
    }
    internal sealed class Publication
    {
        internal Registration Original;internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal bool Current()
        {
            using(var callback=Original.Memory.TryEnterCallbacks())return callback!=null&&Original.Current()&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Original.TerminalBody,Original.TerminalOffset);
        }
    }
    [ThreadStatic]private static Upload readingTerminal;
    [ThreadStatic]private static Registration acceptingHostReceipt;
    internal static bool IsAcceptingHostReceipt(Registration original)=>ThreadManager.IsMainThread()&&
        original!=null&&ReferenceEquals(acceptingHostReceipt,original);
    private static readonly System.Collections.Generic.Dictionary<Guid,Publication> published=new System.Collections.Generic.Dictionary<Guid,Publication>();
    internal static bool RegisterTerminal(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,
        byte[] bytes,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        try
        {
            if(!registrations.TryGetValue(operation,out var original)||!original.Current()||original.Nonce!=nonce||nonce==Guid.Empty||
                !ReferenceEquals(original.Actor,actor)||!ReferenceEquals(original.Sender,sender)||!ReferenceEquals(original.Station,station)||original.Creation!=creation||
                bytes==null||bytes.Length<1||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedRemoteHost.TryPublished(actor,sender,operation,nonce,original.RowHash,out bool saved)||!saved)return false;
            XElement image;
            using(var stream=new MemoryStream(bytes,false))using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=RebirthProductiveEarnedRowProof.MaximumArtifactBytes}))image=XElement.Load(reader);
            if(image.Name!="uiTerminalOriginal"||(string)image.Attribute("operation")!=operation.ToString("N")||(string)image.Attribute("uio")!=original.Hash)return false;
            var delivery=image.Element("delivery")?.Elements().SingleOrDefault();
            if(delivery?.Name!="nativeOwningDelivery"||(string)delivery.Attribute("operation")!=operation.ToString("N")||
                (string)delivery.Attribute("viewer")!=original.Owner||(string)delivery.Attribute("creation")!=creation||
                (int)delivery.Attribute("deaths")!=original.Frame.Deaths||(int)delivery.Attribute("startingOwner")!=original.EarlierOwner.StartingEntityId||
                (int)delivery.Attribute("beforeCount")!=(int)original.Frame.Earned.Multiplier||
                (int)delivery.Attribute("afterCount")!=(int)original.Frame.Earned.Multiplier-1)return false;
            var terminal=Convert.FromBase64String((string)image.Element("afterNative"));int offset=(int)image.Attribute("afterOffset");
            var owner=Convert.FromBase64String((string)image.Element("ownerAfter"));
            if(owner.Length<1||owner.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !ValidateTerminal(original,delivery,image.Element("deferredContribution"),terminal,offset,out var deferred)||!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(original.Path+".TerminalOriginal",bytes))return false;
            if(original.Deferred!=null&&!ReferenceEquals(original.Deferred,deferred))return false;
            original.Deferred=deferred;
            if(original.Terminal!=null)
            {
                if(!original.TerminalBody.SequenceEqual(terminal)||!original.OwnerAfter.SequenceEqual(owner)||!XNode.DeepEquals(original.Terminal.Delivery,delivery))return false;
            }
            else
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(bytes.Length+owner.Length+terminal.Length)),out var allocation))return false;
                original.TerminalBody=terminal;original.TerminalOffset=offset;original.OwnerAfter=owner;
                original.Terminal=new XElementHolder{Delivery=new XElement(delivery),Allocation=allocation};
                original.Terminal.Owner=RebirthProductiveEarnedUiOwnerFile.BeginRemote(original);
                if(original.Terminal.Owner==null)return false;
            }
            reply=Reply(original,RebirthProductiveEarnedWire.Status.UiTerminalHeader);return true;
        }
        catch{return false;}
    }
    private static bool ValidateTerminal(Registration original,XElement delivery,XElement contribution,byte[] body,int offset,
        out RebirthProductiveEarnedUiDeferredTransport.HostContribution deferred)
    {
        deferred=null;
        var frame=original.Frame.Earned;
        if(!RebirthProductiveEarnedNativeBody.TryRead(frame.AfterNative,frame.AfterElapsedOffset,out var before,out _)||
            !RebirthProductiveEarnedNativeBody.TryRead(body,offset,out var after,out ulong elapsed)||before.Queue.Length!=after.Queue.Length)return false;
        int index=frame.RowIndex;var row=before.Queue[index];if(row.StartingEntityId!=original.EarlierOwner.StartingEntityId||row.Recipe==null||row.Multiplier<1)return false;
        var outputBefore=ReadGrid(delivery,"outputBefore");
        var outputAfter=ReadGrid(delivery,"outputAfter");
        var primary=ReadGrid(delivery,"primaryNative");
        var extras=ReadGrid(delivery,"extrasNative");
        var bagBefore=ReadGrid(delivery,"bagBefore");var bagAfter=ReadGrid(delivery,"bagAfter");
        var beltBefore=ReadGrid(delivery,"beltBefore");var beltAfter=ReadGrid(delivery,"beltAfter");
        if(primary.Length!=1||primary[0].count!=row.Recipe.count||primary[0].itemValue.type!=row.Recipe.itemValueType||
            !Same(before.Output,outputBefore)||!Same(after.Output,outputAfter)||
            !RebirthProductiveEarnedArtifact.SameTools(bagBefore,original.Frame.Bag)||
            !RebirthProductiveEarnedArtifact.SameTools(beltBefore,original.Frame.Belt)||
            !ValidateOutputs(row,delivery,primary[0],extras))return false;
        bool retry=(bool)delivery.Attribute("retry");
        if(retry&&delivery.Element("cachedPredecessor")?.Elements().SingleOrDefault()?.Attribute("operation")?.Value!=original.Frame.Operation.ToString("N"))return false;
        if((string)delivery.Attribute("sink")=="Output")
        {if(!RebirthRecipeOutputIntegration.PredictOwningOutput(outputBefore,primary[0],extras,retry,out var expected)||!Same(expected,outputAfter)||
            !Same(bagBefore,bagAfter)||!Same(beltBefore,beltAfter))return false;}
        else
        {
            if((string)delivery.Attribute("sink")!="Inventory"||!Same(outputBefore,outputAfter)||
                !RebirthRecyclerOwnerDelivery.Plan(bagBefore,beltBefore,
                    RebirthCamperStationNativeActorPayment.Locks(original.Actor.bag.LockedSlots),
                    RebirthCamperStationNativeActorPayment.Locks(original.Actor.inventory.ItemGrid.SlotLocks),original.Actor.inventory.Length,
                    new[]{primary[0]}.Concat(extras).ToArray(),out var expectedBag,out var expectedBelt)||
                !Same(expectedBag,bagAfter)||!Same(expectedBelt,beltAfter))return false;
        }
        if(row.StartingEntityId!=original.Actor.entityId)
        {
            if(!RebirthProductiveEarnedUiDeferredTransport.TryValidateTerminal(original,contribution,before.Completions,after.Completions,primary[0],out deferred))return false;
            before.Completions=after.Completions;
        }
        else if(contribution!=null)return false;
        row.Multiplier--;
        if(row.Multiplier==0){row.Recipe=null;row.CraftingTimeLeft=0;row.IsCrafting=false;row.Quality=1;row.StartingEntityId=original.Actor.entityId;row.OneItemCraftTime=0;}
        else row.CraftingTimeLeft=retry?row.OneItemCraftTime:row.CraftingTimeLeft+row.OneItemCraftTime;
        before.Output=outputAfter;
        // Same-owner output has no AddCraftComplete receipt. Exact re-encoding
        // proves every unrelated native queue/grid/fuel/receipt/header byte.
        return RebirthProductiveEarnedNativeBody.Encode(before,elapsed,out int expectedOffset).SequenceEqual(body)&&expectedOffset==offset;
    }
    internal static bool ValidateOutputs(RecipeQueueItem row,XElement delivery,ItemStack primary,ItemStack[] extras)
    {
        // Native construction seeds depend on the actual invocation time. Keep
        // that observed seed while checking every other native item byte.
        var value=new ItemValue(row.Recipe.itemValueType,row.Quality,row.Quality);value.Seed=primary.itemValue.Seed;
        if(!Same(new[]{new ItemStack(value,row.Recipe.count)},new[]{primary}))return false;
        return ValidateExtraOutputs(row.Recipe,delivery,extras);
    }
    internal static bool ValidateExtraOutputs(Recipe recipe,XElement delivery,ItemStack[] extras)
    {
        if((string)delivery.Attribute("extrasDefinition")!=RebirthRecipeOutputs.DefinitionIdentity(recipe)||
            !RebirthRecipeOutputs.TryPlan(recipe,1,out var planned,out _)||planned.Count!=extras.Length)return false;
        bool frozen=recipe.ingredients?.Count>0&&recipe.ingredients[0]?.itemValue?.HasMetadata("rebirth.recipe.outputs.frozen.v1")==true;
        for(int i=0;i<extras.Length;i++)
        {
            var expected=planned[i].Clone();if(!frozen)expected.itemValue.Seed=extras[i].itemValue.Seed;
            if(!Same(new[]{expected},new[]{extras[i]}))return false;
        }
        return true;
    }
    private static bool Same(ItemStack[] left,ItemStack[] right)=>RebirthCamperInventorySnapshot.Capture(left).SequenceEqual(RebirthCamperInventorySnapshot.Capture(right));
    internal static bool TryReadDeferredPrimary(XElement delivery,out ItemStack primary)
    {
        primary=null;
        try
        {
            if(delivery?.Name!="nativeOwningDelivery")return false;
            var slots=ReadGrid(delivery,"primaryNative");
            if(slots.Length!=1||slots[0]?.itemValue==null||slots[0].count<1)return false;
            primary=slots[0];return true;
        }
        catch{return false;}
    }
    internal static ItemStack[] ReadGrid(XElement image,string field)
    {
        byte[] bytes=Convert.FromBase64String((string)image.Element(field));
        if(bytes.Length<8||bytes.Length>2*RebirthProductiveEarnedRowProof.MaximumFrameBytes)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadUInt32()!=0x31534352)throw new InvalidDataException();int count=reader.ReadInt32();
            if(count<0||count>byte.MaxValue)throw new InvalidDataException();long graph=0;
            for(int i=0;i<count;i++)
            {
                int quantity=reader.ReadInt32(),size=reader.ReadInt32();if(quantity<0||quantity>ushort.MaxValue||size<1||size>196608||size>stream.Length-stream.Position)throw new InvalidDataException();
                int offset=checked((int)stream.Position);
                if(quantity>0)
                {if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(bytes,offset,out var measured,out int consumed)||consumed!=size)throw new InvalidDataException();graph=checked(graph+measured.CloneAllocationBound+measured.ParserScratchBound);}
                else if(size>64)throw new InvalidDataException();
                if(graph>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3)throw new InvalidDataException();stream.Position+=size;
            }
            if(stream.Position!=stream.Length)throw new InvalidDataException();
        }
        var slots=RebirthCamperInventorySnapshot.Restore(bytes);
        if(!RebirthCamperInventorySnapshot.Capture(slots).SequenceEqual(bytes))throw new InvalidDataException();return slots;
    }
    internal static bool EnterNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run||readingTerminal!=null||world?.IsRemote()!=false)return true;
        try
        {
            var original=registrations.Values.SingleOrDefault(value=>!value.Completed&&value.Terminal!=null&&value.Current()&&ReferenceEquals(value.Sender,packet.Sender)&&
                value.Station.ToWorldPos().Equals(packet.teWorldPos));
            if(original==null)return true;
            if(packet.ms==null||packet.ms.Length!=original.TerminalBody.Length||world.GetBlock(packet.teWorldPos).type!=packet.teBlockId)return false;
            var allocation=original.Memory.TryEnterCallbacks();if(allocation==null)return false;
            try
            {
                var raw=new byte[(int)packet.ms.Length];lock(packet.ms){long position=packet.ms.Position;try{packet.ms.Position=0;int read=0;while(read<raw.Length){int n=packet.ms.Read(raw,read,raw.Length-read);if(n<1)return false;read+=n;}}finally{packet.ms.Position=position;}}
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,original.TerminalOffset,original.TerminalBody,original.TerminalOffset))return false;
                var captured=new Upload{Original=original,Packet=packet,World=world,Elapsed=BitConverter.ToUInt64(raw,original.TerminalOffset),Allocation=allocation};
                readingTerminal=captured;state=captured;allocation=null;return true;
            }
            finally{allocation?.Dispose();}
        }
        catch{return false;}
    }
    internal static void EnterNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {var value=readingTerminal;if(run&&value!=null&&mode==StreamModeRead.FromClient&&ReferenceEquals(station,value.Original.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;}
    internal static void ObserveNativeRead(PooledBinaryReader reader,bool run)
    {var value=readingTerminal;if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length)value.ReadCompleted=true;}
    internal static void ObserveNativeUpload(NetPackageTileEntity packet,World world,bool run,object state)
    {
        if(state is not Upload value)return;value.Allocation?.Dispose();value.Allocation=null;
        try
        {
            var original=value.Original;
            if(!run||!ReferenceEquals(readingTerminal,value)||!ReferenceEquals(packet,value.Packet)||!ReferenceEquals(world,value.World)||!original.Current()||
                value.Reads!=1||!value.ReadCompleted||!ReferenceEquals(world.GetTileEntity(packet.teWorldPos),original.Station)||
                GameTimer.Instance.ticks<value.Elapsed||original.Station.lastTickTime!=GameTimer.Instance.ticks-value.Elapsed)return;
            if(published.TryGetValue(original.Frame.Operation,out var existing))
            {
                if(!existing.Current())return;
                acceptingHostReceipt=original;
                try{RebirthProductiveEarnedUiDeferredTransport.ObserveHostReceiptAccepted(original);}
                finally{acceptingHostReceipt=null;}
                return;
            }
            var proof=new Publication{Original=original};
            if(!proof.Current()||!RebirthRemoteResourceNativeBinding.TryResolve(world,RemoteResourceIdentity.Workstation(packet.teWorldPos),
                RemoteResourceSourceKind.WorkstationOutput,original.Frame.Birth.ToString("N"),out var binding))return;
            proof.Allocation=original.Memory.TryObserveNative(original.TerminalBody,original.TerminalOffset);if(proof.Allocation==null)return;
            proof.Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,proof.Current,original.Memory);
            if(proof.Watch==null){proof.Allocation.Dispose();return;}published.Add(original.Frame.Operation,proof);
            acceptingHostReceipt=original;
            try{RebirthProductiveEarnedUiDeferredTransport.ObserveHostReceiptAccepted(original);}
            finally{acceptingHostReceipt=null;}
        }
        catch{}
    }
    internal static void EndNativeUpload(object state)
    {if(state is Upload value){value.Allocation?.Dispose();if(ReferenceEquals(readingTerminal,value))readingTerminal=null;}}
    private static bool TerminalSaved(Registration original)
    {
        // The deferred recipient's real consumption/PDS/removal publication
        // has its own producer; initial station+viewer save cannot settle it.
        if(original.Deferred!=null)return false;
        if(original.Terminal?.Owner?.Saved()!=true||!published.TryGetValue(original.Frame.Operation,out var proof)||!proof.Current()||
            !proof.Watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string tile)||!proof.Current())return false;
        if(original.TerminalReceipt!=null)
        {if(!RebirthProductiveEarnedRemoteHost.RetireUiTerminal(new TerminalPublication(terminalIssuer,original,proof)))return false;original.Completed=true;original.Memory?.RetainCompletedAllocation();RebirthProductiveEarnedUiRemoteBefore.Release(original.Frame.Operation);return true;}
        var receipt=new XElement("uiNativeTerminalSaved",new XAttribute("operation",original.Frame.Operation.ToString("N")),new XAttribute("uio",original.Hash),
            new XAttribute("native",Hash(original.TerminalBody)),new XAttribute("owner",Hash(original.Terminal.Owner.Final)),
            new XAttribute("nativePath",native.Path),new XAttribute("payload",native.PayloadDigest),new XAttribute("offset",offset),new XAttribute("length",length),new XAttribute("tile",tile));
        var bytes=Encoding.UTF8.GetBytes(receipt.ToString(SaveOptions.DisableFormatting));
        if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(original.Path+".TerminalSaved",bytes))return false;
        original.TerminalReceipt=bytes;
        if(!RebirthProductiveEarnedRemoteHost.RetireUiTerminal(new TerminalPublication(terminalIssuer,original,proof)))return false;original.Completed=true;original.Memory?.RetainCompletedAllocation();RebirthProductiveEarnedUiRemoteBefore.Release(original.Frame.Operation);return true;
    }
    private static void PruneCompleted()
    {
        foreach(var pair in registrations.Where(value=>value.Value.Completed&&value.Value.GroupReferences==0&&!RetainsQueueTransition(value.Value)).ToArray())
        {
            var original=pair.Value;
            if(published.TryGetValue(pair.Key,out var proof)){proof.Watch.Dispose();proof.Allocation.Dispose();published.Remove(pair.Key);}
            original.Terminal.Owner.Dispose();original.Terminal.Allocation.Dispose();original.Memory.Dispose();original.OwnerRetention?.Dispose();original.Deferred?.Dispose();
            original.Image=original.TerminalBody=original.OwnerAfter=original.TerminalReceipt=null;original.Frame=null;original.Terminal=null;
            registrations.Remove(pair.Key);
        }
    }
    // Release only transient source handles when the actual world is replaced.
    // Durable originals and native early holds remain unknown on cold entry.
    internal static void ReleaseWorld()
    {
        RebirthProductiveEarnedUiRemoteBefore.ReleaseWorld();
        ReleaseQueueTransitions();
        readingTerminal?.Allocation?.Dispose();readingTerminal=null;acceptingHostReceipt=null;
        foreach(var proof in published.Values){proof.Watch?.Dispose();proof.Allocation?.Dispose();}
        published.Clear();
        foreach(var original in registrations.Values)
        {
            original.Terminal?.Owner?.Dispose();original.Terminal?.Allocation?.Dispose();original.OwnerRetention?.Dispose();original.Deferred?.Dispose();
            original.Image=original.TerminalBody=original.OwnerAfter=original.TerminalReceipt=null;
            original.Frame=null;original.Terminal=null;original.Memory?.Dispose();
        }
        registrations.Clear();preparing=null;
    }
}

internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static bool RetireUiTerminal(RebirthProductiveEarnedUiRemote.TerminalPublication proof)
    {
        if(proof==null)return false;
        if(!pending.TryGetValue(proof.Operation,out var original))return proof.Matches(proof.Station);
        if(proof.Nonce!=original.Nonce||!proof.Matches(original.Station))return false;
        var frame=original.Artifact.ReadFrame();if(frame.Birth!=proof.Birth)return false;
        if(!original.UiMarkerRetired)
        {if(!RebirthProductiveEarnedCoordinator.RetireUiTerminalMarker(original.Station,original.Artifact,proof))return false;original.UiMarkerRetired=true;}
        if(early.TryGetValue(proof.Operation,out var held))
        {
            string retained=held.Path+".completed";if(!Safe(held.Path)||!Safe(retained)||File.Exists(held.Path+".tmp"))return false;
            if(File.Exists(held.Path))
            {if(File.Exists(retained))return false;File.Move(held.Path,retained);}
            if(!File.Exists(retained))return false;
            early.Remove(proof.Operation);coldEarly.RemoveAll(value=>value.Operation==proof.Operation);
        }
        original.Observation?.Dispose();original.BeforeWatch?.Dispose();original.BeforeAllocation?.Dispose();original.Memory?.Dispose();
        original.Artifact.RetainCompleted();pending.Remove(proof.Operation);return true;
    }
}

