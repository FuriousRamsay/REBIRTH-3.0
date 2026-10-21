using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal sealed class NativeHydrationSource:IDisposable
    {
        private readonly World world;private readonly TileEntityWorkstation station;private readonly EntityPlayerLocal viewer;
        private readonly INetConnection connection;private readonly string owner,creation,worldKey;private readonly int deaths,block;
        private readonly long readVersion;
        internal readonly byte[] Image;internal readonly string Path;
        private IDisposable allocation;private int references=1;private bool rootClosed;
        private NativeHydrationSource(RebirthProductiveEarnedUiDeferredTransport.ClientHydration original,byte[] image,string path,IDisposable memory)
        {world=original.World;station=original.Station;viewer=original.Actor;connection=original.Connection;owner=original.Owner;creation=original.Creation;
            deaths=original.Deaths;worldKey=original.WorldKey;block=world.GetBlock(station.ToWorldPos()).type;readVersion=original.ReadVersion;Image=image;Path=path;allocation=memory;}
        internal bool RowCurrent()=>Current()&&RebirthProductiveEarnedUiDeferredTransport.IsCurrentQueueOwnerNativeRead(world,station,readVersion);
        // Earlier original identity only. Native row eligibility separately
        // requires the exact actual read's Recipe handle, never getter equality.
        internal bool Current()=>allocation!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()&&
            ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&world.GetBlock(station.ToWorldPos()).type==block&&
            ReferenceEquals(world.GetPrimaryPlayer(),viewer)&&ReferenceEquals(world.GetEntity(viewer.entityId),viewer)&&!viewer.IsDead()&&viewer.Died==deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(viewer)==creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==owner&&
            worldKey==RebirthProductiveEarnedArtifact.WorldIdentity(world)&&!connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],connection)&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);
        internal IDisposable Retain(){if(!Current())return null;references++;return new Lease(this);}
        private void Release(){if(--references==0){allocation?.Dispose();allocation=null;}}
        private sealed class Lease:IDisposable{private NativeHydrationSource source;internal Lease(NativeHydrationSource value){source=value;}public void Dispose(){var old=System.Threading.Interlocked.Exchange(ref source,null);old?.Release();}}
        public void Dispose(){if(rootClosed)return;rootClosed=true;Release();}
        internal static NativeHydrationSource Issue(RebirthProductiveEarnedUiDeferredTransport.ClientHydration original)
        {
            if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(original)||original.NativeBefore==null)return null;
            long size=checked(8L*Encoding.UTF8.GetByteCount(original.Header.ToString(SaveOptions.DisableFormatting))+16L*original.NativeBefore.Length+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(size,out var allocation))return null;
            try
            {
                var image=Encoding.UTF8.GetBytes(new XElement("nativeQueueOwnerHydrationOriginal",new XAttribute("version",1),new XAttribute("group",original.Group.ToString("N")),
                    new XElement(original.Header),new XElement("native",Convert.ToBase64String(original.NativeBefore))).ToString(SaveOptions.DisableFormatting));
                string path=System.IO.Path.Combine(GameIO.GetPlayerDataDir(),"RebirthProductiveQueueOwner","nativeHydrations",original.Group.ToString("N")+".original");
                if(image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(path,image))return null;
                var result=new NativeHydrationSource(original,image,path,allocation);allocation=null;return result;
            }
            finally{allocation?.Dispose();}
        }
    }
    private sealed class HydrationEntry:IDisposable
    {
        internal Witness Existing,Added;internal RecipeQueueItem Row;internal string Purpose;internal IDisposable CurrentLease;
        public void Dispose(){CurrentLease?.Dispose();CurrentLease=null;Added?.Dispose();Added=null;}
    }
    internal static bool ImportActualNativeHydration(RebirthProductiveEarnedUiDeferredTransport.ClientHydration original)
    {
        if(!installed||!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(original))return false;
        var envelope=original.Header.Element("queueOwners");if(envelope==null)return true;
        if(original.Header.Elements("queueOwners").Count()!=1||envelope.Attributes().Count()!=1||(int?)envelope.Attribute("version")!=1||
            envelope.Elements().Any(value=>value.Name!="row")||Encoding.UTF8.GetByteCount(envelope.ToString(SaveOptions.DisableFormatting))>48*1024)return false;
        var entries=envelope.Elements("row").ToArray();if(entries.Length<1||entries.Length>byte.MaxValue||
            entries.Select(value=>(int)value.Attribute("index")).Distinct().Count()!=entries.Length)return false;
        using var source=NativeHydrationSource.Issue(original);if(source==null||!source.Current())return false;
        ObserveWorld();
        var staged=new System.Collections.Generic.List<HydrationEntry>();
        try
        {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||original.NativeBefore==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.NativeBefore,original.NativeBeforeOffset))return false;
            foreach(var entry in entries)
            {
                int index=(int)entry.Attribute("index"),count=(int)entry.Attribute("count"),originalCount=(int)entry.Attribute("originalCount");
                if(entry.Attributes().Count()!=3||entry.Elements().Count()!=2||entry.Element("purpose")==null||entry.Element("binding")==null||
                    index<0||index>=original.Station.Queue.Length||count<1||originalCount<count||originalCount>short.MaxValue)return false;
                var row=original.Station.Queue[index];string purpose=entry.Element("purpose").Value,binding=entry.Element("binding").Value;
                var owner=ReadImportedCreator(purpose);
                if(owner.Name!="queueOwnerPurpose"||owner.Attributes().Count()!=11||(int?)owner.Attribute("version")!=1||
                    !Guid.TryParseExact((string)owner.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                    !Guid.TryParseExact((string)owner.Attribute("birth"),"N",out var birth)||birth==Guid.Empty||row?.Recipe==null||row.Multiplier!=count||
                    !MatchesPurposeCard(purpose,row,birth,original.WorldKey)||Binding(row)!=binding||
                    (string)owner.Attribute("definition")!=ProofHash(Encoding.UTF8.GetBytes(binding)))return false;
                var previous=originals.Where(value=>ReferenceEquals(value.World,original.World)&&ReferenceEquals(value.Station,original.Station)&&value.Operation==operation&&value.Birth==birth).ToArray();
                if(previous.Length>1)return false;
                if(previous.Length==1)
                {
                    var value=previous[0];
                    if(value.Closed||value.RowRetired||value.OriginalOwnerKey!=(string)owner.Attribute("owner")||value.Creation!=(string)owner.Attribute("creation")||
                        value.StartingEntityId!=(int)owner.Attribute("entity")||value.Deaths!=(int)owner.Attribute("deaths")||value.RecipeBinding!=binding||value.OriginalCount!=originalCount)return false;
                    // Renewal is issued by this new authenticated original
                    // native read and exact retained host source, not equality.
                    if(NativeBinding(row)!=value.NativeRecipeBinding||staged.Any(item=>ReferenceEquals(item.Existing,value)))return false;
                    var hold=source.Retain();if(hold==null)return false;
                    staged.Add(new HydrationEntry{Existing=value,Row=row,Purpose=purpose,CurrentLease=hold});continue;
                }
                if(originals.Count+staged.Count(item=>item.Added!=null)>=64||staged.Any(item=>item.Added?.Operation==operation&&item.Added.Birth==birth))return false;
                string nativeBinding=NativeBinding(row),save=Path.GetFullPath(GameIO.GetSaveGameDir());
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*binding.Length+65536),out var memory))return false;
                var retained=source.Retain();if(retained==null){memory.Dispose();return false;}
                var witness=new Witness{World=original.World,Station=original.Station,Recipe=row.Recipe,Operation=operation,Birth=birth,
                    OriginalOwnerKey=(string)owner.Attribute("owner"),Creation=(string)owner.Attribute("creation"),StartingEntityId=(int)owner.Attribute("entity"),Deaths=(int)owner.Attribute("deaths"),
                    OriginalCount=originalCount,RecipeBinding=binding,NativeRecipeBinding=nativeBinding,WorldKey=original.WorldKey,Save=save,
                    Image=source.Image,Path=source.Path,Retention=memory,HydrationRetention=retained,NativeHydration=source,ClientPurpose=purpose,Published=true};
                var pending=new HydrationEntry{Added=witness,Row=row,Purpose=purpose};staged.Add(pending);
                if(!witness.Current())return false;
            }
            if(!source.Current()||!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(original)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.NativeBefore,original.NativeBeforeOffset))return false;
            // All parsing, native matching, durable source writes and resource
            // acquisitions precede mutation of any retained original handle.
            int required=checked(originals.Count+staged.Count(item=>item.Added!=null));
            if(originals.Capacity<required)originals.Capacity=required;
            var retired=new System.Collections.Generic.List<IDisposable>(staged.Count);
            foreach(var pending in staged)
            {
                if(pending.Added!=null){originals.Add(pending.Added);pending.Added=null;continue;}
                var value=pending.Existing;retired.Add(value.HydrationCurrentRetention);
                value.HydrationCurrentRetention=pending.CurrentLease;pending.CurrentLease=null;
                value.NativeHydration=source;value.ClientPurpose=pending.Purpose;value.Recipe=pending.Row.Recipe;
            }
            // Preserve the immutable first image's permanent source lease;
            // only a superseded current-read lineage lease can retire here.
            foreach(var lease in retired)lease?.Dispose();
        }
        return true;
        }
        finally{foreach(var pending in staged)pending.Dispose();}
    }
    private static bool TryUseNativeHydration(RebirthProductiveEarnedUiOriginal.BindingToken binding,out Witness witness)
    {
        witness=null;if(binding?.Current()!=true||binding.Station.Queue==null||binding.Index<0||binding.Index>=binding.Station.Queue.Length)return false;
        var row=binding.Station.Queue[binding.Index];
        var matches=originals.Where(value=>value.NativeHydration!=null&&ReferenceEquals(value.Station,binding.Station)&&value.Birth==binding.Birth&&
            value.ClientPurpose==binding.OwnerPurpose&&value.SourcePublished()&&value.Matches(row)&&ReferenceEquals(row.Recipe,binding.Recipe)).ToArray();
        if(matches.Length!=1)return false;witness=matches[0];return true;
    }
    internal static bool TryObserveRemoteNativeSource(XUiC_RecipeStack card,TileEntityWorkstation station,int index,RecipeQueueItem row,string purpose,out Witness witness)
    {
        witness=null;
        try
        {
            if(TryObserveRemoteInsertionSource(card,station,index,row,purpose,out witness))return true;
            if(card?.xui?.playerUI?.entityPlayer?.world?.IsRemote()!=true||station?.Queue==null||index<0||index>=station.Queue.Length||
                !ReferenceEquals(station.Queue[index],row)||!ReferenceEquals(card.GetRecipe(),row?.Recipe)||card.GetRecipeCount()!=row.Multiplier||
                card.StartingEntityId!=row.StartingEntityId||!TryObserveQueueOwner(station,index,row,out var source)||source.NativeHydration==null||
                source.ClientPurpose!=purpose||Binding(InsertionRow(card))!=source.RecipeBinding)return false;
            witness=source;return true;
        }
        catch{witness=null;return false;}
    }
}
