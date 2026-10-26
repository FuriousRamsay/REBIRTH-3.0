using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

// Versioned vehicle-owned endpoints. Native world wiring below an attached root
// remains native; this frame records only the moving vehicle-to-root cable.
internal static partial class RebirthCamperRelayWiring
{
    internal const string Key="RebirthCamperRelayLinksV1";
    private const string StampPrefix="rebirth.camper.relay.target:";
    internal sealed class Link
    {
        internal int Relay,Corner=-1,Block;
        internal Vector3i Position;
        internal Guid Stamp;
        internal float Length;
        internal bool Mounted=>Corner>=0;
    }
    internal sealed class Frame
    {
        internal Guid Vehicle,World;
        internal long Revision;
        internal readonly List<Link> Links=new List<Link>();
    }
    private sealed class Cached {internal string Text;internal Frame Frame;}
    private static ConditionalWeakTable<EntityVehicle,Cached> frames=new ConditionalWeakTable<EntityVehicle,Cached>();
    private static string WorldId(World world)=>world.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):world.worldState?.Guid;
    private static bool Scope(EntityVehicle vehicle,out Guid identity,out Guid world)
    {
        identity=world=Guid.Empty;
        return vehicle?.vehicle!=null&&vehicle.world!=null&&RebirthCamperRadial.IsCamper(vehicle)
            &&ReferenceEquals(GameManager.Instance?.World,vehicle.world)&&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            &&vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out string text)
            &&Guid.TryParse(text,out identity)&&identity!=Guid.Empty&&Guid.TryParse(WorldId(vehicle.world),out world)&&world!=Guid.Empty;
    }
    internal static Frame Read(EntityVehicle vehicle)
    {
        if(!Scope(vehicle,out var identity,out var world))return null;
        vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(Key,out string text);
        var cache=frames.GetValue(vehicle,_=>new Cached());
        if(cache.Frame!=null&&cache.Text==text&&cache.Frame.Vehicle==identity&&cache.Frame.World==world)return cache.Frame;
        var frame=new Frame{Vehicle=identity,World=world};
        if(!string.IsNullOrEmpty(text))
        {
            if(text.Length>96*1024)throw new InvalidDataException("Camper wiring frame exceeds limit.");
            var bytes=Convert.FromBase64String(text);
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=0x31575243)throw new InvalidDataException("Camper wiring version.");
                var savedVehicle=new Guid(reader.ReadBytes(16));var savedWorld=new Guid(reader.ReadBytes(16));long revision=reader.ReadInt64();
                int count=reader.ReadInt32();
                if(savedVehicle!=identity||savedWorld==Guid.Empty||revision<0||count<0||count>128)throw new InvalidDataException("Camper wiring scope.");
                frame.Revision=revision;
                var seen=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<count;i++)
                {
                    var link=new Link{Relay=reader.ReadByte(),Corner=reader.ReadSByte(),Position=new Vector3i(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32()),
                        Block=reader.ReadInt32(),Stamp=new Guid(reader.ReadBytes(16)),Length=reader.ReadSingle()};
                    string id=link.Mounted?"turret:"+link.Corner:"block:"+link.Position;
                    if(link.Relay>1||link.Corner< -1||link.Corner>3||link.Block<0||link.Stamp==Guid.Empty||float.IsNaN(link.Length)||float.IsInfinity(link.Length)||link.Length<=0||link.Length>1000||!seen.Add(id))
                        throw new InvalidDataException("Camper wiring endpoint.");
                    // Portable campers retain their identity. World cables cannot
                    // silently become links to unrelated blocks in another save.
                    if(savedWorld==world||link.Mounted)frame.Links.Add(link);
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException("Camper wiring tail.");
            }
        }
        cache.Text=text;cache.Frame=frame;return frame;
    }
    private static void Save(EntityVehicle vehicle,Frame frame)
    {
        if(vehicle.world.IsRemote()||!Scope(vehicle,out var identity,out var world)||frame.Vehicle!=identity||frame.World!=world)
            throw new InvalidOperationException("Authoritative wiring scope required.");
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x31575243);writer.Write(identity.ToByteArray());writer.Write(world.ToByteArray());writer.Write(frame.Revision);writer.Write(frame.Links.Count);
            foreach(var link in frame.Links)
            {writer.Write((byte)link.Relay);writer.Write((sbyte)link.Corner);writer.Write(link.Position.x);writer.Write(link.Position.y);writer.Write(link.Position.z);writer.Write(link.Block);writer.Write(link.Stamp.ToByteArray());writer.Write(link.Length);}
            writer.Flush();var text=Convert.ToBase64String(stream.ToArray());vehicle.vehicle.GetUpdatedItemValue().SetMetadata(Key,text);
            var cache=frames.GetValue(vehicle,_=>new Cached());cache.Text=text;cache.Frame=frame;
        }
        vehicle.SendSyncData(4);VehicleManager.Instance?.TriggerSave();
    }
    private static string StampKey(TileEntity tile)=>StampPrefix+tile.localChunkPos.x+":"+tile.localChunkPos.y+":"+tile.localChunkPos.z;
    private static byte[] Marker(TileEntity tile)
        =>tile?.chunk!=null&&tile.chunk.ChunkCustomData.dict.TryGetValue(StampKey(tile),out var marker)?marker?.data:null;
    private static Guid Claim(TileEntity tile,Guid owner)
    {
        var bytes=Marker(tile);
        if(bytes!=null)
        {
            if(bytes.Length!=32)throw new InvalidOperationException("Device cable marker is invalid.");
            var priorOwner=new Guid(bytes.Skip(16).Take(16).ToArray());
            if(priorOwner==owner)return new Guid(bytes.Take(16).ToArray());
            var world=GameManager.Instance?.World;
            bool liveOwner=world?.Entities?.list?.OfType<EntityVehicle>().Any(vehicle=>
                !vehicle.IsDead()&&RebirthCamperRadial.IsCamper(vehicle)&&vehicle.vehicle!=null&&
                vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out string identity)&&
                Guid.TryParse(identity,out var parsed)&&parsed==priorOwner)==true;
            if(liveOwner)throw new InvalidOperationException("Device already has another camper cable.");
            // The old vehicle is absent from the loaded area containing this
            // device, so its moving cable cannot still be supplying it.
            Log.Out("[CamperRelay] Reclaiming orphan cable target="+tile.ToWorldPos()+" old="+priorOwner+" new="+owner);
            tile.chunk.ChunkCustomData.Remove(StampKey(tile));tile.chunk.isModified=true;
        }
        var stamp=Guid.NewGuid();bytes=stamp.ToByteArray().Concat(owner.ToByteArray()).ToArray();
        tile.chunk.ChunkCustomData.Set(StampKey(tile),new ChunkCustomData(StampKey(tile),ulong.MaxValue,false){data=bytes});tile.chunk.isModified=true;
        return stamp;
    }
    private static bool Matches(TileEntity tile,Link link,Guid owner)
    {
        var bytes=Marker(tile);
        return bytes?.Length==32&&new Guid(bytes.Take(16).ToArray())==link.Stamp&&new Guid(bytes.Skip(16).Take(16).ToArray())==owner
            &&tile.blockValue.type==link.Block&&!tile.IsRemoving;
    }
    internal static void ReleaseDestroyed(EntityVehicle vehicle)
    {
        if(vehicle?.world==null||vehicle.world.IsRemote()||!ThreadManager.IsMainThread())return;
        Frame frame;
        try{frame=Read(vehicle);}catch(Exception error){Log.Warning("[CamperRelay] Destroyed camper wiring cleanup: "+error.Message);return;}
        if(frame==null)return;
        foreach(var link in frame.Links)
        {
            if(link.Mounted)continue;
            var tile=vehicle.world.GetTileEntity(link.Position);
            if(tile?.chunk==null||!Matches(tile,link,frame.Vehicle))continue;
            tile.chunk.ChunkCustomData.Remove(StampKey(tile));tile.chunk.isModified=true;
        }
    }
    internal static void ReleaseForTravel(EntityVehicle vehicle)
    {
        if(vehicle?.world==null||vehicle.world.IsRemote()||!ThreadManager.IsMainThread())return;
        Frame frame;
        try{frame=Read(vehicle);}catch(Exception error){Log.Warning("[CamperRelay] Travel wiring cleanup: "+error.Message);return;}
        if(frame==null||frame.Links.Count==0)return;
        try{Save(vehicle,new Frame{Vehicle=frame.Vehicle,World=frame.World,Revision=checked(frame.Revision+1)});}
        catch(Exception error){Log.Warning("[CamperRelay] Travel wiring save refused: "+error.Message);return;}
        foreach(var link in frame.Links)
        {
            if(link.Mounted)continue;
            var tile=vehicle.world.GetTileEntity(link.Position);
            if(tile?.chunk==null||!Matches(tile,link,frame.Vehicle))continue;
            tile.chunk.ChunkCustomData.Remove(StampKey(tile));tile.chunk.isModified=true;
        }
        foreach(var actor in selections.Where(pair=>ReferenceEquals(pair.Value.Vehicle,vehicle)).Select(pair=>pair.Key).ToArray())selections.Remove(actor);
        Log.Out("[REBIRTH EnergyFlow] relay-travel-disconnect vehicle="+vehicle.entityId+" links="+frame.Links.Count);
    }
    internal static void BlockRemoved(WorldBase world,Vector3i position)
    {
        if(world==null||world.IsRemote())return;
        var tile=world.GetTileEntity(position);if(tile?.chunk==null)return;
        if(tile.chunk.ChunkCustomData.dict.ContainsKey(StampKey(tile))){tile.chunk.ChunkCustomData.Remove(StampKey(tile));tile.chunk.isModified=true;}
    }
    internal static Transform Relay(EntityVehicle vehicle,int side)
        =>vehicle?.RootTransform?.GetComponentsInChildren<RebirthCamperWireTarget>(true).FirstOrDefault(t=>t.Kind==0&&t.Slot==side)?.transform;
    internal static bool Connected(EntityVehicle vehicle,TileEntity tile)
    {
        try
        {
            var frame=Read(vehicle);if(frame==null||tile==null)return false;
            if(tile is TileEntityPowered powered)
            {
                var node=powered.GetPowerItem();int depth=0;
                while(node?.Parent!=null&&depth++<128)node=node.Parent;
                if(depth>=128)return false;
                if(node?.TileEntity!=null)tile=node.TileEntity;
            }
            var link=frame.Links.FirstOrDefault(l=>!l.Mounted&&l.Position==tile.ToWorldPos());
            var relay=link==null?null:Relay(vehicle,link.Relay);
            return relay&&Matches(tile,link,frame.Vehicle)&&(relay.position+Origin.position-tile.ToWorldPos().ToVector3Center()).sqrMagnitude<=link.Length*link.Length;
        }
        catch{return false;}
    }
    internal static bool TurretConnected(EntityVehicle vehicle,int corner)
    {try{return Read(vehicle)?.Links.Any(l=>l.Corner==corner&&Relay(vehicle,l.Relay))==true;}catch{return false;}}
}
