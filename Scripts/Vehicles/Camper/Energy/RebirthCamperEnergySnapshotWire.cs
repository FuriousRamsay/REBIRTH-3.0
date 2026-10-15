using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

// Observations only, never an actor/source mutation input.
internal static class RebirthCamperEnergySnapshotWire
{
    internal static byte[] Write(RebirthCamperEnergySnapshot snapshot,RebirthCamperEnergyPoiSnapshot poi,bool busy,string status=null,bool boundsAllowed=false,bool boundsShown=false,int sourceCapacity=0)
    {
        using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            if(sourceCapacity!=0&&sourceCapacity!=RebirthCamperEnergyCapacity.Normal&&sourceCapacity!=RebirthCamperEnergyCapacity.Electrician)throw new InvalidDataException("Authoritative source capacity required.");
            w.Write((byte)4);w.Write(busy);w.Write(status??string.Empty);w.Write(boundsAllowed);w.Write(boundsShown&&boundsAllowed);w.Write((byte)sourceCapacity);w.Write(snapshot!=null);
            if(snapshot!=null)
            {
                w.Write((byte)snapshot.Mode);w.Write(snapshot.TransitionSeconds);w.Write(snapshot.Range);w.Write(snapshot.Revision);
                w.Write(snapshot.AvailableWatts);w.Write(snapshot.DemandWatts);w.Write(snapshot.SuppliedWatts);w.Write(snapshot.Sources.Count);
                foreach(var s in snapshot.Sources)
                {
                    w.Write(s.Id.ToByteArray());w.Write((byte)s.Position);w.Write((byte)s.Kind);w.Write(s.IsOn);w.Write(s.CurrentFuel);w.Write(s.MaxFuel);w.Write(s.CurrentPower);w.Write(s.MaxOutput);
                    w.Write(s.AvailableWatts);w.Write((byte)s.Status);CamperFrame.Write(w,RebirthCamperEnergyItems.Write(new[]{s.Installed}));
                    CamperFrame.Write(w,RebirthCamperEnergyItems.Write(s.Components));
                }
                ValidateEstimate(snapshot);w.Write((byte)snapshot.RuntimeEstimateStatus);w.Write(snapshot.RuntimeEstimateSolarConditional);
                w.Write(snapshot.EstimatedRuntimeSeconds.HasValue);if(snapshot.EstimatedRuntimeSeconds.HasValue)w.Write(snapshot.EstimatedRuntimeSeconds.Value);
            }
            w.Write(poi!=null);if(poi!=null){w.Write(poi.InRange);w.Write(poi.PoiName??string.Empty);w.Write(poi.SupportedLights);w.Write(poi.IsOn);w.Write(poi.Range);w.Write(poi.DemandWatts);}
            w.Flush();return stream.ToArray();
        }
    }
    internal static void Read(byte[] bytes,out RebirthCamperEnergySnapshot snapshot,out RebirthCamperEnergyPoiSnapshot poi,out bool busy,out string status,out bool boundsAllowed,out bool boundsShown)
        =>Read(bytes,out snapshot,out poi,out busy,out status,out boundsAllowed,out boundsShown,out _);
    internal static void Read(byte[] bytes,out RebirthCamperEnergySnapshot snapshot,out RebirthCamperEnergyPoiSnapshot poi,out bool busy,out string status,out bool boundsAllowed,out bool boundsShown,out int sourceCapacity)
    {
        snapshot=null;poi=null;busy=true;status=null;boundsAllowed=false;boundsShown=false;sourceCapacity=0;
        if(bytes==null||bytes.Length>8*1024*1024)throw new InvalidDataException("Bounded Energy observation required.");
        using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream,Encoding.UTF8,true))
        {
            int version=r.ReadByte();if(version!=2&&version!=3&&version!=4)throw new InvalidDataException("Energy observation version changed.");busy=r.ReadBoolean();status=r.ReadString();
            boundsAllowed=r.ReadBoolean();boundsShown=r.ReadBoolean();if(boundsShown&&!boundsAllowed)throw new InvalidDataException("Unscoped bounds observation.");
            if(version>=3){sourceCapacity=r.ReadByte();if(sourceCapacity!=0&&sourceCapacity!=RebirthCamperEnergyCapacity.Normal&&sourceCapacity!=RebirthCamperEnergyCapacity.Electrician)throw new InvalidDataException("Source capacity observation invalid.");}
            if(status.Length>128)throw new InvalidDataException("Energy status bound exceeded.");
            if(r.ReadBoolean())
            {
                snapshot=new RebirthCamperEnergySnapshot {Mode=(RebirthCamperEnergyMode)r.ReadByte(),TransitionSeconds=r.ReadSingle(),Range=r.ReadSingle(),Revision=r.ReadInt64(),
                    AvailableWatts=r.ReadInt32(),DemandWatts=r.ReadInt32(),SuppliedWatts=r.ReadInt32()};
                if(!Enum.IsDefined(typeof(RebirthCamperEnergyMode),snapshot.Mode)||float.IsNaN(snapshot.TransitionSeconds)||float.IsInfinity(snapshot.TransitionSeconds)||
                    snapshot.TransitionSeconds<0||float.IsNaN(snapshot.Range)||float.IsInfinity(snapshot.Range)||snapshot.Range<0||snapshot.Revision<0||
                    snapshot.AvailableWatts<0||snapshot.DemandWatts<0||snapshot.SuppliedWatts<0)throw new InvalidDataException("Energy observation fields invalid.");
                int count=r.ReadInt32();if(count<0||count>12)throw new InvalidDataException("Provisional configured source bound exceeded.");
                var sources=new List<RebirthCamperEnergySourceSnapshot>();var seen=new HashSet<Guid>();var positions=new HashSet<int>();
                for(int i=0;i<count;i++)
                {
                    var id=new Guid(r.ReadBytes(16));int position=version>=3?r.ReadByte():i;
                    if(position<0||position>=12||!positions.Add(position))throw new InvalidDataException("Source position observation invalid.");
                    var s=new RebirthCamperEnergySourceSnapshot {Id=id,Position=position,Kind=(RebirthCamperEnergySourceKind)r.ReadByte(),IsOn=r.ReadBoolean(),
                        CurrentFuel=r.ReadUInt16(),MaxFuel=r.ReadUInt16(),CurrentPower=r.ReadUInt16(),MaxOutput=r.ReadUInt16(),AvailableWatts=r.ReadUInt16(),Status=(RebirthCamperEnergySourceStatus)r.ReadByte()};
                    var item=RebirthCamperEnergyItems.Read(CamperFrame.Read(r),1);s.components=RebirthCamperEnergyItems.Read(CamperFrame.Read(r),6);
                    if(s.Id==Guid.Empty||!seen.Add(s.Id)||!Enum.IsDefined(typeof(RebirthCamperEnergySourceKind),s.Kind)||
                        !Enum.IsDefined(typeof(RebirthCamperEnergySourceStatus),s.Status)||item.Length!=1||item[0].count!=1||s.components.Length!=6||s.CurrentFuel>s.MaxFuel)
                        throw new InvalidDataException("Native source observation invalid.");s.installed=item[0];sources.Add(s);
                }
                snapshot.Sources=new ReadOnlyCollection<RebirthCamperEnergySourceSnapshot>(sources);
                if(version>=4)
                {
                    snapshot.RuntimeEstimateStatus=(RebirthCamperEnergyEstimateStatus)r.ReadByte();snapshot.RuntimeEstimateSolarConditional=r.ReadBoolean();
                    if(r.ReadBoolean())snapshot.EstimatedRuntimeSeconds=r.ReadDouble();ValidateEstimate(snapshot);
                }
            }
            if(r.ReadBoolean())
            {
                poi=new RebirthCamperEnergyPoiSnapshot {InRange=r.ReadBoolean(),PoiName=r.ReadString(),SupportedLights=r.ReadInt32(),IsOn=r.ReadBoolean(),Range=r.ReadSingle(),DemandWatts=r.ReadInt32()};
                if(poi.PoiName.Length>4096||poi.SupportedLights<0||poi.DemandWatts<0||float.IsNaN(poi.Range)||float.IsInfinity(poi.Range)||poi.Range<0)throw new InvalidDataException("POI observation invalid.");
            }
            if(boundsAllowed&&snapshot?.Mode!=RebirthCamperEnergyMode.Camp)throw new InvalidDataException("Bounds require current Camp observation.");
            if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing Energy observation bytes.");
        }
    }
    private static void ValidateEstimate(RebirthCamperEnergySnapshot snapshot)
    {
        if(!Enum.IsDefined(typeof(RebirthCamperEnergyEstimateStatus),snapshot.RuntimeEstimateStatus)||
            snapshot.EstimatedRuntimeSeconds.HasValue&&(double.IsNaN(snapshot.EstimatedRuntimeSeconds.Value)||double.IsInfinity(snapshot.EstimatedRuntimeSeconds.Value)||snapshot.EstimatedRuntimeSeconds.Value<0)||
            (snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.Finite)!=snapshot.EstimatedRuntimeSeconds.HasValue||
            snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.DaylightSupported&&!snapshot.RuntimeEstimateSolarConditional||
            snapshot.RuntimeEstimateStatus!=RebirthCamperEnergyEstimateStatus.Unavailable&&snapshot.Mode!=RebirthCamperEnergyMode.Camp||
            snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.Idle&&snapshot.DemandWatts!=0||
            (snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.Finite||snapshot.RuntimeEstimateStatus==RebirthCamperEnergyEstimateStatus.DaylightSupported)&&snapshot.DemandWatts<=0)
            throw new InvalidDataException("Energy runtime estimate invalid.");
    }
}
