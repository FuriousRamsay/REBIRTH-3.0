using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    // Actual selected player-source original. Reply delivery never serves as
    // source payment; only its native owner witness may cross that boundary.
    internal sealed class InventoryOriginal
    {
        internal readonly Guid Operation;internal readonly EntityPlayer Actor;internal readonly World World;
        internal readonly string Owner,Creation,Directory,Text,Purpose;internal readonly int Deaths;
        internal readonly RebirthConsumptionInventoryPlan Inventory;
        internal readonly RebirthMetabolismStateRepository.NpcServingState Metabolism;
        internal readonly RebirthMetabolismService.NpcSourcePlan Serving;
        internal readonly IDisposable Memory;
        private readonly RebirthRemoteResourceNetworkScope network;
        private readonly string save;
        private readonly GameManager game;
        internal bool BeforeIssued,SourceIssued,SourceSaved,CanonicalEntered,CanonicalSaved,DeferredEntered,DeferredReturned,DeferredStateSaved,SecondaryEntered,SecondarySaved,NativeEntered,NativeSaved,Committed,Terminal,ObservationRetired,MemoryReleased;
        internal int QualifiedPrefix;
        internal long NextPurpose;internal byte LastPurpose;
        internal InventoryOriginal(Guid operation,EntityPlayer actor,RebirthStablePlayerIdentity identity,RebirthConsumptionInventoryPlan inventory,
            RebirthMetabolismStateRepository.NpcServingState metabolism,RebirthMetabolismService.NpcSourcePlan serving,IDisposable memory,string exactOriginal=null)
        {
            Operation=operation;Actor=actor;World=actor.world;Owner=identity.CanonicalId;Creation=RebirthCamperCharacterAccess.CurrentCreation(actor);Deaths=actor.Died;
            Inventory=inventory;Metabolism=metabolism;Serving=serving;Memory=memory;game=GameManager.Instance;network=RebirthRemoteResourceNetworkScope.Capture(actor);
            save=Path.GetFullPath(GameIO.GetSaveGameDir());Directory=Path.Combine(save,"RebirthData","PlayerConsumption",identity.StorageKey,operation.ToString("N"));
            var image=new XElement("playerSourceConsumption",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("owner",Owner),new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),new XAttribute("world",World.worldState.Guid),
                new XAttribute("sourceKind",(byte)inventory.Kind),new XAttribute("sourceSlot",inventory.Slot),new XAttribute("beltCount",inventory.BeltCount),
                new XElement("beforeBag",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(inventory.BeforeBag))),
                new XElement("beforeBelt",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(inventory.BeforeBelt))),
                new XElement("afterBag",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(inventory.AfterBag))),
                new XElement("afterBelt",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(inventory.AfterBelt))),
                new XElement("bagLocks",new string(inventory.BagLocks.Select(value=>value?'1':'0').ToArray())),
                new XElement("beltLocks",new string(inventory.BeltLocks.Select(value=>value?'1':'0').ToArray())),
                new XElement("source",new XAttribute("count",serving.Expected.count),RebirthNativeItemCodec.Encode(serving.Expected.itemValue)),
                new XElement("replacement",new XAttribute("count",serving.Replacement?.count??0),serving.Replacement?.IsEmpty()==false?RebirthNativeItemCodec.Encode(serving.Replacement.itemValue):""),
                new XElement("siblings",new XAttribute("count",serving.Siblings?.count??0),serving.Siblings?.IsEmpty()==false?RebirthNativeItemCodec.Encode(serving.Siblings.itemValue):""),
                new XElement("serving",RebirthNativeItemCodec.Encode(serving.NativeItem.itemValue)),RebirthMetabolismService.DescribeNpcPlan(serving),
                new XElement("metabolismBefore",metabolism.Before),new XElement("metabolismAfter",RebirthMetabolismStateRepository.DescribeNpcServing(metabolism.Id,serving.State)));
            Text=exactOriginal??image.ToString(SaveOptions.DisableFormatting);
            if(Encoding.UTF8.GetByteCount(Text)>16*1024*1024)throw new InvalidDataException("Player consumption original exceeds admitted bound.");
            if(exactOriginal!=null)image=RebirthCookingRemoteAdmission.ReadProtocolXml(exactOriginal,16*1024*1024);
            var purpose=new XElement("playerOwnerConsumption",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("account",Owner),new XAttribute("creation",Creation),new XAttribute("deaths",exactOriginal==null?Deaths:(int)image.Attribute("deaths")),new XAttribute("world",World.worldState.Guid),
                new XAttribute("binding",RebirthRemoteResourceDurableJournal.Digest(Text)),
                image.Elements().Where(value=>value.Name!="metabolismBefore"&&value.Name!="metabolismAfter"&&value.Name!="plan").Select(value=>new XElement(value)),
                new XElement("sourceAddress",new XAttribute("kind",(byte)inventory.Kind),new XAttribute("slot",inventory.Slot),new XAttribute("beltCount",inventory.BeltCount)),
                new XElement("nativeSecondary",serving.Deferred.Where(value=>value.Kind==RebirthMetabolismService.NpcDeferredKind.HydrationCost).Select(value=>value.Write())),
                new XElement("feedback",RebirthMetabolismService.DescribeNpcPlan(serving).Element("result")));
            Purpose=purpose.ToString(SaveOptions.DisableFormatting);
            if(Encoding.UTF8.GetByteCount(Purpose)>MaximumInventoryPurpose)throw new InvalidDataException("Player consumption native purpose exceeds admission bound.");
        }
        internal bool Current()=>!Terminal&&!ObservationRetired&&ThreadManager.IsMainThread()&&network?.Current==true&&ReferenceEquals(GameManager.Instance,game)&&
            ReferenceEquals(game.World,World)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(Actor,out var owner)&&owner.CanonicalId==Owner;
        internal bool StateCurrent()=>Current()&&RebirthMetabolismStateRepository.NpcServingBeforeCurrent(Metabolism);
        internal void Record(string stage,XElement detail=null)
            =>RecordBoundStage(Directory,Operation,Text,Current,stage,detail);
        internal static void RecordBoundStage(string Directory,Guid Operation,string Text,Func<bool> Current,string stage,XElement detail=null)
        {
            if(!Current()||stage.Length>64||stage.Any(value=>!char.IsLetterOrDigit(value)&&value!='-'))throw new InvalidDataException("Player consumption stage scope changed.");
            for(var parent=new DirectoryInfo(Directory);parent!=null;parent=parent.Parent)
                if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Player consumption original path changed.");
            System.IO.Directory.CreateDirectory(Directory);
            var text=new XElement("playerConsumptionStage",new XAttribute("version",1),new XAttribute("stage",stage),new XElement("original",Text),detail).ToString(SaveOptions.DisableFormatting);
            var bytes=new UTF8Encoding(false,true).GetBytes(text);if(bytes.Length>64*1024*1024)throw new InvalidDataException("Player consumption stage exceeds bound.");
            string path=Path.Combine(Directory,stage+".xml");
            if(File.Exists(path))
            {
                var info=new FileInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length!=bytes.Length||!File.ReadAllBytes(path).SequenceEqual(bytes))throw new IOException("Player consumption immutable stage conflicts.");
                PublishControl(Directory,Operation,Text,Current,stage,bytes);return;
            }
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(!Current()||!File.ReadAllBytes(temporary).SequenceEqual(bytes))throw new IOException("Player consumption stage publication changed.");
            File.Move(temporary,path);PublishControl(Directory,Operation,Text,Current,stage,bytes);
        }
        private static void PublishControl(string Directory,Guid Operation,string Text,Func<bool> Current,string stage,byte[] fullStage)
        {
            string digest;using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(fullStage)).Replace("-","").ToLowerInvariant();
            string original=RebirthRemoteResourceDurableJournal.Digest(Text);int next=0;bool retained=false;
            var heads=System.IO.Directory.EnumerateFiles(Directory,"head-*.xml").Take(1025).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            if(heads.Length>1024)throw new InvalidDataException("Player consumption control history bound exceeded.");
            foreach(string path in heads)
            {
                var info=new FileInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length<1||info.Length>4096)throw new InvalidDataException("Player consumption control changed.");
                using var reader=System.Xml.XmlReader.Create(path,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096});
                var head=XElement.Load(reader);
                if(head.Name!="playerConsumptionHead"||head.Nodes().Any()||head.Attributes().Count()!=6||(int?)head.Attribute("version")!=1||
                    (int?)head.Attribute("sequence")!=next||(string)head.Attribute("operation")!=Operation.ToString("N")||(string)head.Attribute("original")!=original||
                    Path.GetFileName(path)!="head-"+next.ToString("D5")+".xml")throw new InvalidDataException("Player consumption control lineage changed.");
                if((string)head.Attribute("stage")==stage)
                {if((string)head.Attribute("body")!=digest)throw new InvalidDataException("Player consumption stage control conflicts.");retained=true;}
                next++;
            }
            if(retained)return;
            var image=new XElement("playerConsumptionHead",new XAttribute("version",1),new XAttribute("sequence",next),new XAttribute("operation",Operation.ToString("N")),
                new XAttribute("original",original),new XAttribute("stage",stage),new XAttribute("body",digest));
            byte[] bytes=Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));string destination=Path.Combine(Directory,"head-"+next.ToString("D5")+".xml");
            string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(!Current()||!File.ReadAllBytes(temporary).SequenceEqual(bytes))throw new IOException("Player consumption control publication changed.");
            File.Move(temporary,destination);
            if(!File.ReadAllBytes(destination).SequenceEqual(bytes))throw new IOException("Player consumption control readback changed.");
        }
    }
    private static readonly Dictionary<Guid,InventoryOriginal> inventoryOriginals=new Dictionary<Guid,InventoryOriginal>();
    [ThreadStatic]private static InventoryOriginal canonicalInventoryMutation;
    internal static bool AllowsInventoryCanonical(InventoryOriginal original)=>ReferenceEquals(canonicalInventoryMutation,original)&&original?.SourceSaved==true&&
        original.CanonicalEntered&&!original.Metabolism.Applied&&original.StateCurrent()&&inventoryOriginals.TryGetValue(original.Operation,out var active)&&ReferenceEquals(active,original);
    internal static bool ApplyInventoryCanonical(InventoryOriginal original)
    {
        if(original?.SourceSaved!=true||!original.Current()||!inventoryOriginals.TryGetValue(original.Operation,out var active)||!ReferenceEquals(active,original))return false;
        try
        {
            if(original.CanonicalSaved)return true;
            if(!original.Metabolism.Applied)
            {
                if(original.CanonicalEntered||canonicalInventoryMutation!=null||!original.StateCurrent())return false;
                original.Record("ingestion-intent");original.CanonicalEntered=true;canonicalInventoryMutation=original;
                try{ObserveInventoryCall(original.Operation,1);if(!RebirthMetabolismStateRepository.ApplyInventoryServing(original))return false;}finally{canonicalInventoryMutation=null;}
            }
            if(!RebirthMetabolismStateRepository.SaveInventoryServing(original,out string full))return false;
            original.Record("ingestion-saved",new XElement("saved",full));original.CanonicalSaved=true;return true;
        }
        catch{return false;}
    }
    internal static bool AdvanceInventoryConsumption(InventoryOriginal original)
    {
        if(original==null||!original.Current()||!inventoryOriginals.TryGetValue(original.Operation,out var current)||!ReferenceEquals(current,original))return false;
        try
        {
            if(!original.SourceSaved)
            {
                // Both are actual independently accepted/native saved files,
                // not client reports or a current replicated actor grid.
                if(!ArchiveInventoryOwner(original,"before")||!ArchiveInventoryOwner(original,"source"))return false;
                original.Record("source-saved");original.SourceSaved=true;
            }
            if(!ApplyInventoryCanonical(original))return false;
            if(!original.DeferredReturned)
            {
                if(original.DeferredEntered)return false;
                original.Record("secondary-intent");original.DeferredEntered=true;
                ObserveInventoryCall(original.Operation,2);
                RebirthMetabolismService.ApplyNpcSourceDeferredEffects(original.Serving,true);
                original.DeferredReturned=true;original.Record("secondary-returned");
            }
            original.Record("secondary-returned");
            if(!original.DeferredStateSaved)
            {
                if(!RebirthMetabolismStateRepository.SaveInventoryServing(original,out string secondaryState))return false;
                original.Record("secondary-state-saved",new XElement("saved",secondaryState));original.DeferredStateSaved=true;
            }
            // Native owner hydration cost and preserved Use follow their
            // separately entered actual native purpose callbacks. No host
            // replica or absent marker can manufacture their completion.
            if(!ArchiveInventoryOwner(original,"secondary"))return false;original.SecondarySaved=true;
            if(!ArchiveInventoryOwner(original,"after"))return false;original.NativeSaved=true;
            if(!RebirthMetabolismStateRepository.SaveInventoryServing(original,out string saved))return false;
            original.Record("committed",new XElement("saved",saved));original.Committed=true;return true;
        }
        catch{return false;}
    }
    internal static bool HoldsInventoryOriginal(EntityPlayer actor)=>HoldsLoadedInventoryActor(actor)||inventoryOriginals.Values.Any(value=>!value.Terminal&&ReferenceEquals(value.Actor,actor));
    internal static bool HoldsInventoryState(RebirthMetabolismState state)=>inventoryOriginals.Values.Any(value=>!value.Terminal&&
        (ReferenceEquals(value.Metabolism.Original,state)||ReferenceEquals(value.Metabolism.After,state)));
    internal static bool HoldsInventoryWorld(World world)=>inventoryNativeLoads.Any(value=>ReferenceEquals(value.World,world)&&value.OwnerCurrent()&&value.Operations.Length>0)||
        inventoryOriginals.Values.Any(value=>!value.Terminal&&value.Current()&&ReferenceEquals(value.World,world));
    internal static bool TryPrepareInventoryOriginal(Guid operation,EntityPlayer actor,RebirthMetabolismSourceKind kind,int slot,ItemStack expected,bool autoSip,
        out InventoryOriginal original,out RebirthConsumeResult result)
    {
        original=null;result=new RebirthConsumeResult{Success=false,Failure=RebirthMetabolismConsumeFailure.ItemChanged,Message="The original serving could not be prepared."};
        IDisposable memory=null;
        try
        {
            if(inventoryOriginals.TryGetValue(operation,out var existing))
            {
                if(!existing.Current()||!ReferenceEquals(existing.Actor,actor)||existing.Inventory.Kind!=kind||existing.Inventory.Slot!=slot||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(existing.Serving.Expected,expected))return false;
                original=existing;result=existing.Serving.Result;return true;
            }
            if(operation==Guid.Empty||!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||HoldsInventoryOriginal(actor)||
                inventoryOriginals.Count>=16||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                !RebirthConsumptionOwnerPublication.TryCaptureCreation(actor,out _)||expected==null||expected.IsEmpty()||expected.count>ushort.MaxValue||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out memory))return false;
            if(System.IO.Directory.Exists(Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","PlayerConsumption",identity.StorageKey,operation.ToString("N"))))return false;
            var token=RebirthMetabolismStateRepository.CaptureNpcServingState(actor,out var detached);if(token==null)return false;
            string auditBefore=CaptureInventoryAuditFingerprint(actor);
            var serving=RebirthMetabolismService.PlanNpcSource(actor,expected,detached,out var planned,autoSip);
            if(serving==null){result=planned;ObserveInventoryRefusal(actor,token,auditBefore,result);return false;}
            var bag=actor.bag?.ItemGrid?.items;var belt=actor.inventory?.ItemGrid?.items;
            if(bag==null||belt==null||!RebirthConsumptionInventoryPlan.TryCreate(kind,slot,RebirthToolbeltCapacity.GetOwnedSlotCount(actor,belt.Length),bag,belt,
                RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots),RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks),expected,serving.Replacement??ItemStack.Empty.Clone(),serving.Siblings,out var inventory))
            {ObserveInventoryRefusal(actor,token,auditBefore,result);return false;}
            var candidate=new InventoryOriginal(operation,actor,identity,inventory,token,serving,memory);
            if(!candidate.StateCurrent())return false;
            candidate.Record("prepared");inventoryOriginals.Add(operation,candidate);ObserveInventoryAdmission(operation,actor,candidate.Purpose);original=candidate;result=planned;memory=null;return true;
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
}
