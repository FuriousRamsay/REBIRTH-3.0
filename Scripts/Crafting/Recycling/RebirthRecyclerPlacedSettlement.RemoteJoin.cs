using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class RecyclerJoining:IDisposable
    {
        internal NetPackagePlayerId Packet;internal PlayerDataFile File;internal World World;internal INetConnection Connection;
        internal EntityPlayerLocal Actor;internal byte[] Native,Buffs;internal ItemStack[] Bag,Belt;internal int Reads,Deaths;
        internal string Owner,Account,Creation,WorldKey;internal bool Hydrated,Completed;internal Guid[] Pending;
        internal readonly System.Collections.Generic.Dictionary<Guid,TileEntityWorkstation> UnqualifiedStations=new System.Collections.Generic.Dictionary<Guid,TileEntityWorkstation>();
        internal readonly System.Collections.Generic.HashSet<Guid> Unstarted=new System.Collections.Generic.HashSet<Guid>();
        private IDisposable memory;
        private RecyclerJoining(IDisposable memory){this.memory=memory;}
        internal bool Current()=>memory!=null&&Completed&&Hydrated&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died==Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&identity.CanonicalId==Account&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldKey&&!Connection.IsDisconnected()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&
            SameOwnerGrid(Actor.bag.ItemGrid.items,Bag)&&SameOwnerGrid(Actor.inventory.ItemGrid.items,Belt);
        internal static RecyclerJoining Enter(NetPackagePlayerId packet,World world,GameManager callbacks,bool run)
        {
            if(!authorityInstalled||!run||!ThreadManager.IsMainThread()||joiningRecyclerOwner!=null||world?.IsRemote()!=true||
                !ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(GameManager.Instance,callbacks)||packet?.playerDataFile?.bLoaded!=true||packet.playerDataFile.bDead||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetReceivedConnection(packet,out var connection)||connection==null||connection.IsDisconnected()||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],connection)||
                joinedRecyclerOwners.Count>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return null;
            try
            {
                var source=new RecyclerJoining(memory){Packet=packet,File=packet.playerDataFile,World=world,Connection=connection,Deaths=packet.playerDataFile.deaths,
                    WorldKey=RebirthWorkstationCompletionOutbox.CurrentGuid(world)};
                source.Native=NativeOwnerImage(source.File);source.Bag=RebirthGearPlayerDataInventory.ReadSlots(source.File,true);source.Belt=RebirthGearPlayerDataInventory.ReadSlots(source.File,false);
                source.Buffs=source.File.buffData?.ToArray();if(source.Native.Length>16*1024*1024||source.Bag==null||source.Belt==null||ReadRecyclerReceipts(source.Buffs)==null)return null;
                memory=null;return source;
            }
            finally{memory?.Dispose();}
        }
        public void Dispose()
        {joinedRecyclerOwners.Remove(this);UnqualifiedStations.Clear();Unstarted.Clear();Packet=null;File=null;Actor=null;World=null;Connection=null;Native=Buffs=null;Bag=Belt=null;Pending=null;memory?.Dispose();memory=null;}
    }
    private static readonly System.Collections.Generic.List<RecyclerJoining> joinedRecyclerOwners=new System.Collections.Generic.List<RecyclerJoining>();
    [ThreadStatic]private static RecyclerJoining joiningRecyclerOwner;
    internal static object EnterRecyclerRemoteJoin(NetPackagePlayerId packet,World world,GameManager callbacks,bool run)
    {var source=RecyclerJoining.Enter(packet,world,callbacks,run);if(source!=null)joiningRecyclerOwner=source;return source;}
    internal static void ObserveRecyclerRemoteJoin(NetPackagePlayerId packet,World world,bool run,object state)
    {
        if(state is not RecyclerJoining source||!run||!ReferenceEquals(joiningRecyclerOwner,source)||!ReferenceEquals(source.Packet,packet)||
            !ReferenceEquals(source.World,world)||source.Reads!=1||!source.Hydrated||!ReferenceEquals(world.GetPrimaryPlayer(),source.Actor)||
            !ReferenceEquals(world.GetEntity(source.Actor.entityId),source.Actor)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return;
        source.Owner=identity.StorageKey;source.Account=identity.CanonicalId;source.Creation=RebirthCamperCharacterAccess.CurrentCreation(source.Actor);
        if(string.IsNullOrEmpty(source.Creation))return;
        source.Completed=true;
        if(!source.Current()){source.Completed=false;return;}
        source.Pending=ReadRecyclerReceipts(source.Buffs).Where(value=>
            {try{return HasJoinedRecyclerOriginal(source,value.Key)&&!HasJoinedRecyclerRetirement(source,value.Key,value.Value);}catch{return true;}}).Select(value=>value.Key).ToArray();
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
        if(Directory.Exists(root))foreach(string path in Directory.EnumerateFiles(root,"*.remote-client.xml"))
        {
            string name=Path.GetFileName(path);
            if(!Guid.TryParseExact(name.Substring(0,name.Length-".remote-client.xml".Length),"N",out var operation)||source.Pending.Contains(operation)||
                !HasJoinedRecyclerOriginal(source,operation)||!PhaseBytes(source.Buffs,"rbRecyclerPlaced_"+operation.ToString("N"),out float phase)||phase!=0||
                !PhaseBytes(source.Buffs,"rbRecyclerSlot_"+operation.ToString("N"),out float count)||count!=0)continue;
            if(source.Unstarted.Count>=8192)break;source.Unstarted.Add(operation);
        }
        source.Pending=source.Pending.Concat(source.Unstarted).Distinct().ToArray();
        if(source.Pending.Length==0){source.Completed=false;return;}
        joinedRecyclerOwners.Add(source);
        foreach(var old in recyclerClients.Where(value=>value.Source!=null&&source.Pending.Contains(value.Source.Operation)&&
            ReferenceEquals(value.Source.World,source.World)&&value.Source.Owner==source.Owner&&value.Source.CanonicalOwner==source.Account&&
            value.Source.Creation==source.Creation&&value.Source.Deaths==source.Deaths&&value.Connection?.IsDisconnected()==true).ToArray())
        {
            var station=old.UnqualifiedStation??old.Source.Station;
            if(station!=null&&ReferenceEquals(source.World.GetTileEntity(station.ToWorldPos()),station))source.UnqualifiedStations[old.Source.Operation]=station;
            // Actual new native join owns the unresolved financial selector;
            // retire only stale disconnected observation graphs, never files.
            recyclerClients.Remove(old);old.Dispose();
        }
    }
    internal static void EndRecyclerRemoteJoin(object state,Exception error)
    {
        if(state is not RecyclerJoining source)return;
        if(ReferenceEquals(joiningRecyclerOwner,source))joiningRecyclerOwner=null;
        if(error!=null||!source.Completed||!joinedRecyclerOwners.Contains(source)){source.Dispose();return;}
        source.Packet=null;source.File=null;
    }
    private static object EnterJoinedRecyclerOwner(PlayerDataFile data,EntityPlayer actor,global::StreamModeRead mode,bool run)
    {
        var source=joiningRecyclerOwner;
        if(!run||mode!=global::StreamModeRead.FromServer||source==null||source.Reads!=0||!ReferenceEquals(source.File,data)||actor is not EntityPlayerLocal local||
            !ReferenceEquals(local.world,source.World)||local.entityId!=source.Packet.id||!ReferenceEquals(source.World.GetPrimaryPlayer(),local)||
            !NativeOwnerImage(data).SequenceEqual(source.Native))return null;
        source.Actor=local;source.Reads++;return source;
    }
    private static void ObserveJoinedRecyclerOwner(PlayerDataFile data,EntityPlayer actor,bool run,RecyclerJoining source)
    {
        if(run&&ReferenceEquals(joiningRecyclerOwner,source)&&ReferenceEquals(source.File,data)&&ReferenceEquals(source.Actor,actor)&&source.Reads==1&&
            actor.Died==source.Deaths&&SameOwnerGrid(source.Actor.bag.ItemGrid.items,source.Bag)&&SameOwnerGrid(source.Actor.inventory.ItemGrid.items,source.Belt))source.Hydrated=true;
    }
    private static bool JoinedRecyclerOwnerHeld(EntityPlayerLocal actor)=>joinedRecyclerOwners.Any(value=>ReferenceEquals(value.Actor,actor)&&value.Current()&&
        value.Pending?.Any(operation=>!value.Unstarted.Contains(operation)||value.UnqualifiedStations.ContainsKey(operation))==true);
    private static bool HasJoinedRecyclerOriginal(RecyclerJoining source,Guid operation)
    {
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-client.xml");
        // Streaming discovery only creates a hold. Full immutable original and
        // actual owner/native read remain mandatory for financial reconstruction.
        if(RebirthWorkstationCompletionPublication.PublishedLength(path)<1)return false;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var reader=System.Xml.XmlReader.Create(file,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,
            MaxCharactersInDocument=24L*1024*1024,MaxCharactersFromEntities=0}))
        {
            reader.MoveToContent();if(reader.Name!="workstationCompletionIntent"||reader.GetAttribute("operation")!=operation.ToString("N")||reader.GetAttribute("world")!=source.WorldKey)return false;
            while(reader.Read())if(reader.NodeType==System.Xml.XmlNodeType.Element&&reader.Depth==2&&reader.Name=="placedRecyclerOriginal")
                return reader.GetAttribute("operation")==operation.ToString("N")&&reader.GetAttribute("owner")==source.Owner&&reader.GetAttribute("account")==source.Account&&
                    reader.GetAttribute("creation")==source.Creation&&reader.GetAttribute("world")==source.WorldKey&&
                    int.TryParse(reader.GetAttribute("deaths"),out int deaths)&&deaths==source.Deaths;
            return false;
        }
    }
    private static bool HasJoinedRecyclerRetirement(RecyclerJoining source,Guid operation,float phase)
    {
        if(phase!=3)return false;
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-retired.xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var memory))return false;
        using(memory)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=record?.Element("before")?.Elements().SingleOrDefault();
            var retired=record?.Element("recyclerRemoteRetired")?.Element("recyclerRemoteStatus");
            return record?.Name=="workstationCompletionIntent"&&(string)record.Attribute("operation")==operation.ToString("N")&&(string)record.Attribute("world")==source.WorldKey&&
                original?.Name=="placedRecyclerOriginal"&&(string)original.Attribute("operation")==operation.ToString("N")&&(string)original.Attribute("owner")==source.Owner&&
                (string)original.Attribute("account")==source.Account&&(string)original.Attribute("creation")==source.Creation&&(int?)original.Attribute("deaths")==source.Deaths&&
                (string)original.Attribute("world")==source.WorldKey&&(int?)retired?.Attribute("version")==1&&(int?)retired.Attribute("stage")==6&&
                (string)retired.Attribute("operation")==operation.ToString("N")&&(string)retired.Attribute("owner")==source.Owner&&(string)retired.Attribute("creation")==source.Creation&&
                (int?)retired.Attribute("deaths")==source.Deaths&&(string)retired.Attribute("world")==source.WorldKey&&
                (string)retired.Attribute("original")==RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting)));
        }
    }
    private static void SweepJoinedRecyclerOwners(World world)
    {foreach(var source in joinedRecyclerOwners.Where(value=>!ReferenceEquals(value.World,world)||value.Connection.IsDisconnected()).ToArray())source.Dispose();}
}
