using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class NativeIntroduction
    {
        internal NetPackageTileEntity Packet;internal World World;internal TileEntityWorkstation Station;internal EntityPlayer Actor;
        internal string Owner,Creation;internal int Deaths,Reads;internal bool ReadCompleted;
        internal byte[] Before,PacketBody;internal string[] Bindings;internal int[] Counts;internal Witness[] Owners;internal IDisposable Retention;
    }
    [ThreadStatic]private static NativeIntroduction reading;
    internal static void EnterNativeUpload(NetPackageTileEntity packet,World world,bool run,out object state)
    {
        state=null;if(!run||!installed||reading!=null||world?.IsRemote()!=false)return;
        try
        {
            ObserveWorld();var sender=packet.Sender;var station=world.GetTileEntity(packet.teWorldPos) as TileEntityWorkstation;
            if(station==null||station is RebirthCamperStationState||sender==null||!sender.loginDone||sender.disconnecting||packet.ms==null||
                packet.ms.Length<1||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity))return;
            var actor=world.GetEntity(sender.entityId) as EntityPlayer;string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            if(actor==null||actor.IsDead()||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId),sender)||
                !packet.ValidEntityIdForSender(actor.entityId)||!RebirthOriginalActionScope.Validate(identity.StorageKey,creation))return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!Capture(station,out var before,out _)||station.Queue==null)return;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(before.Length+packet.ms.Length)+65536),out var retention))return;
                var value=new NativeIntroduction{Packet=packet,World=world,Station=station,Actor=actor,Owner=identity.StorageKey,Creation=creation,
                    Deaths=actor.Died,Before=before,Retention=retention,Bindings=new string[station.Queue.Length],Counts=new int[station.Queue.Length],Owners=new Witness[station.Queue.Length]};
                try
                {
                    for(int i=0;i<station.Queue.Length;i++)
                    {
                        var row=station.Queue[i];if(row?.Recipe==null||row.Multiplier<1)continue;
                        value.Bindings[i]=NativeBinding(row);value.Counts[i]=row.Multiplier;
                        var owners=originals.Where(owner=>ReferenceEquals(owner.Station,station)&&owner.Matches(row)).ToArray();if(owners.Length==1)value.Owners[i]=owners[0];
                    }
                    value.PacketBody=new byte[(int)packet.ms.Length];lock(packet.ms)
                    {long position=packet.ms.Position;try{packet.ms.Position=0;int total=0;while(total<value.PacketBody.Length){int n=packet.ms.Read(value.PacketBody,total,value.PacketBody.Length-total);if(n<1)throw new System.IO.EndOfStreamException();total+=n;}}finally{packet.ms.Position=position;}}
                    reading=value;state=value;retention=null;
                }
                finally{retention?.Dispose();}
            }
        }
        catch{}
    }
    internal static void EnterNativeRead(TileEntityWorkstation station,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {var value=reading;if(run&&value!=null&&mode==StreamModeRead.FromClient&&ReferenceEquals(station,value.Station)&&ReferenceEquals(reader.BaseStream,value.Packet.ms))value.Reads++;}
    internal static void ObserveNativeRead(PooledBinaryReader reader,bool run)
    {var value=reading;if(run&&value!=null&&value.Reads==1&&ReferenceEquals(reader.BaseStream,value.Packet.ms)&&reader.BaseStream.Position==value.Packet.ms.Length)value.ReadCompleted=true;}
    internal static void ObserveNativeUpload(NetPackageTileEntity packet,World world,bool run,object state)
    {try{ObserveNativeUploadCore(packet,world,run,state);ObserveRemoteIntroduction(packet,world,run,state);}catch{/* Native result remains unchanged; no owner proof is issued. */}}
    private static void ObserveNativeUploadCore(NetPackageTileEntity packet,World world,bool run,object state)
    {
        if(state is not NativeIntroduction value||!run||!ReferenceEquals(reading,value)||value.Reads!=1||!value.ReadCompleted||
            !ReferenceEquals(value.Packet,packet)||!ReferenceEquals(value.World,world)||!ReferenceEquals(world.GetTileEntity(packet.teWorldPos),value.Station)||
            value.Actor.IsDead()||value.Actor.Died!=value.Deaths||RebirthCamperCharacterAccess.CurrentCreation(value.Actor)!=value.Creation||
            !RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var identity)||identity.StorageKey!=value.Owner)return;
        string[] afterBindings;RecipeQueueItem[] rows;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!Capture(value.Station,out var accepted,out int offset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(accepted,offset,value.PacketBody,offset)||
                GameTimer.Instance.ticks<BitConverter.ToUInt64(value.PacketBody,offset)||value.Station.lastTickTime!=GameTimer.Instance.ticks-BitConverter.ToUInt64(value.PacketBody,offset))return;
            rows=value.Station.Queue;afterBindings=rows.Select(row=>row?.Recipe!=null&&row.Multiplier>0?NativeBinding(row):null).ToArray();
        }
        for(int i=0;i<rows.Length;i++)
        {
            string binding=afterBindings[i];if(binding==null||afterBindings.Count(other=>other==binding)!=1)continue;
            var previous=Enumerable.Range(0,value.Bindings.Length).Where(index=>value.Bindings[index]==binding).ToArray();
            if(previous.Length==0)
            {if(rows[i].StartingEntityId==value.Actor.entityId)Issue(value.Actor,value.Station,rows[i],value.Owner,value.Creation,value.Deaths,value.Before);}
            else if(previous.Length==1&&rows[i].Multiplier<=value.Counts[previous[0]]&&value.Owners[previous[0]] is Witness owner)
            {
                // Same positively observed earlier source crosses this exact
                // accepted native read; a new row or count increase cannot adopt it.
                owner.Recipe=rows[i].Recipe;
            }
        }
    }
    internal static void EndNativeUpload(object state)
    {if(state is NativeIntroduction value){value.Before=value.PacketBody=null;value.Retention?.Dispose();value.Retention=null;if(ReferenceEquals(reading,value))reading=null;}}
}
