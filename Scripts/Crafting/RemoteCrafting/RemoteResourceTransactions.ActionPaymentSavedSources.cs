using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private static bool SaveActionTarget(ActionPayment payment,bool after)
    {
        if(after?payment.TargetAfterSaved:payment.TargetBeforeSaved)return true;
        string targetPhase=after?"after":"before",targetStage=after?"target-after-saved":"target-before-saved";
        if(payment.TargetMaterial!=null)
        {
            if(payment.RestartTargetSource==null||after&&!payment.RestartTargetSource.LoadedAfterPublication)
            {
                if(payment.TargetPublication==null||!payment.TargetPublication.TryCopyPublishedTile(out var savedTarget)||!RememberPublishedActionTarget(payment,payment.TargetPublication,savedTarget,after))return false;
            }
            if(!FinishActionMaterial(payment,payment.TargetMaterial,targetPhase,targetStage))return false;
            payment.TargetMaterial.Dispose();payment.TargetMaterial=null;payment.TargetPublication?.Dispose();payment.TargetPublication=null;
            if(after)payment.TargetAfterSaved=true;else payment.TargetBeforeSaved=true;return true;
        }
        if(!payment.TargetCurrent())return false;
        if(payment.TargetPublication!=null&&(payment.TargetPublication.Closed||payment.TargetPublication.Expires<=DateTime.UtcNow)){payment.TargetPublication.Dispose();payment.TargetPublication=null;}
        payment.TargetPublication??=RebirthRemoteResourceNativePublication.BeginElectricalTarget(payment);
        if(payment.TargetPublication?.Saved()!=true)return false;
        // Copying a region witness retains snapshot, tile, compressed payload,
        // and marker arrays simultaneously. Reserve that peak before the copy.
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out var memory))return false;
        using(memory)
        {
            if(!payment.TargetPublication.TryCopyPublishedTile(out var tile))return false;
            string stage=after?"target-after-saved":"target-before-saved",path=Path.Combine(payment.Directory,stage+".native.xml");
            ActionPath(path);
            if(!File.Exists(path))
            {
                using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {using(var writer=XmlWriter.Create(output,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),CloseOutput=false})){tile.WriteActionArtifact(writer);writer.Flush();}output.Flush(true);}
            }
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var comparison=new ActionCompareStream(input,48L*1024*1024))
            using(var writer=XmlWriter.Create(comparison,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),CloseOutput=false}))
            {tile.WriteActionArtifact(writer);writer.Flush();comparison.RequireEnd();}
            var material=RememberActionMaterial(targetPhase,path,48*1024*1024);VerifyActionMaterialImage(material,tile,null);payment.TargetMaterial=material;
            if(!RememberPublishedActionTarget(payment,payment.TargetPublication,tile,after))return false;
            if(!FinishActionMaterial(payment,payment.TargetMaterial,targetPhase,stage))return false;
            payment.TargetMaterial.Dispose();payment.TargetMaterial=null;
        }
        payment.TargetPublication.Dispose();payment.TargetPublication=null;
        if(after)payment.TargetAfterSaved=true;else payment.TargetBeforeSaved=true;return true;
    }
    private static bool ActionRootCurrent(ActionPayment payment,ActionLeg leg)
    {
        if(!payment.ActorCurrent())return false;
        if(leg.RemoteOwner)return RemoteActionOriginalCurrent(payment.RemoteOwner);
        if(leg.Local)return ReferenceEquals(leg.Grid,leg.Belt?payment.Actor.inventory.ItemGrid:payment.Actor.bag.ItemGrid);
        if(leg.Source is StaticContainerResourceSource container)return ReferenceEquals(container.NativeTile,leg.Endpoint)&&ReferenceEquals((container.NativeTile as TileEntityComposite)?.GetFeature<TEFeatureStorage>()?.ItemGrid,leg.Grid);
        if(leg.Source is WorkstationOutputResourceSource station)return ReferenceEquals(station.NativeStation,leg.Endpoint);
        if(leg.Source is VehicleResourceSource vehicle)return ReferenceEquals(vehicle.Vehicle,leg.Endpoint)&&ReferenceEquals(payment.World.GetEntity(vehicle.Vehicle.entityId),leg.Endpoint)&&ReferenceEquals(vehicle.Vehicle.bag?.ItemGrid,leg.Grid);
        if(leg.Source is DroneResourceSource drone)return ReferenceEquals(drone.Drone,leg.Endpoint)&&ReferenceEquals(payment.World.GetEntity(drone.Drone.entityId),leg.Endpoint)&&ReferenceEquals(drone.Drone.bag?.ItemGrid,leg.Grid);
        return leg.Npc!=null&&ReferenceEquals(payment.World.GetEntity(leg.Npc.NativeActor.entityId),leg.Endpoint)&&leg.Npc.NativeActor.RebirthRuntimeState?.StableId==leg.Npc.NativeIdentity;
    }
    private static bool EnsureActionIdentity(ActionPayment payment,ActionLeg leg)
    {
        if(!ActionRootCurrent(payment,leg)||!SameActionItems(ActionLive(payment,leg),leg.Before))return false;
        if(leg.Local||leg.Npc!=null)return true;
        string token=null;
        if(leg.Source is StaticContainerResourceSource container)token=(container.NativeTile as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>()?.Incarnation;
        else if(leg.Source is WorkstationOutputResourceSource station)
        {
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(payment.World,station.NativeStation,out token))
            {
                if(leg.StationIdentity!=null&&(leg.StationIdentity.Closed||leg.StationIdentity.Expires<=DateTime.UtcNow)){leg.StationIdentity.Dispose();leg.StationIdentity=null;}
                leg.StationIdentity??=RebirthLogisticsEndpointIdentity.Begin(payment.World,station.NativeStation,()=>ActionRootCurrent(payment,leg)&&SameActionItems(ActionLive(payment,leg),leg.Before));
                if(leg.StationIdentity?.Saved()!=true)return false;token=leg.StationIdentity.Id;
            }
        }
        else if(leg.Source is VehicleResourceSource vehicle)
        {
            token=RebirthLogisticsVehiclePublication.CurrentIncarnation(vehicle.Vehicle);
            if(token==null){leg.VehicleIdentity??=RebirthLogisticsVehiclePublication.BeginIdentityAdmission(vehicle.Vehicle,()=>ActionRootCurrent(payment,leg)&&SameActionItems(ActionLive(payment,leg),leg.Before));if(!RebirthLogisticsVehiclePublication.Verify(leg.VehicleIdentity,()=>ActionRootCurrent(payment,leg)))return false;token=leg.VehicleIdentity.Incarnation;}
        }
        else if(leg.Source is DroneResourceSource drone)
        {
            token=RebirthLogisticsDronePublication.CurrentIncarnation(drone.Drone);
            if(token==null){leg.DroneIdentity??=RebirthLogisticsDronePublication.BeginIdentityAdmission(drone.Drone,()=>ActionRootCurrent(payment,leg)&&SameActionItems(ActionLive(payment,leg),leg.Before));if(!RebirthLogisticsDronePublication.Verify(leg.DroneIdentity,()=>ActionRootCurrent(payment,leg)))return false;token=leg.DroneIdentity.Incarnation;}
        }
        if(!RebirthRemoteResourceNativeBinding.TryResolve(payment.World,leg.Id,leg.Source.Kind,token,out var binding)||!ReferenceEquals(binding.Endpoint,leg.Endpoint)||!ReferenceEquals(binding.Grid,leg.Grid))return false;
        leg.Native=binding;leg.StationIdentity?.Dispose();leg.StationIdentity=null;leg.VehicleIdentity?.Dispose();leg.VehicleIdentity=null;leg.DroneIdentity?.Dispose();leg.DroneIdentity=null;return true;
    }
    private static bool SaveActionNative(ActionPayment payment,ActionLeg leg,string phase,ItemStack[] expected)
    {
        if(leg.SavedMaterial!=null)return FinishActionLegMaterial(payment,leg,phase);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024,out var memory))return false;
        using(memory)return SaveActionNativeReserved(payment,leg,phase,expected);
    }
    private static bool SaveActionNativeReserved(ActionPayment payment,ActionLeg leg,string phase,ItemStack[] expected)
    {
        if(!ActionLegCurrent(payment,leg)||!SameActionItems(ActionLive(payment,leg),expected))return false;
        if(leg.Publication!=null&&(leg.Publication.Closed||leg.Publication.Expires<=DateTime.UtcNow)){leg.Publication.Dispose();leg.Publication=null;}
        if(leg.Publication==null)leg.Publication=RebirthRemoteResourceNativePublication.Begin(leg.Native,()=>ActionLegCurrent(payment,leg)&&SameActionItems(ActionLive(payment,leg),expected));
        if(leg.Publication?.Saved()!=true)return false;
        int index=payment.Legs.IndexOf(leg);string stage="source-"+index+"-"+phase+"-saved",path=Path.Combine(payment.Directory,stage+".native.xml");
        XElement mobile=null;RebirthRemoteResourceNativePublication.SavedTile tile=null;
        if(leg.Publication.Tile!=null){if(!leg.Publication.TryCopyPublishedTile(out tile))return false;}
        else if(leg.Publication.Vehicle is RebirthLogisticsVehiclePublication.Expected vehicle&&vehicle.Native!=null)
            mobile=new XElement("nativeActionVehicle",new XAttribute("version",1),new XAttribute("incarnation",vehicle.Incarnation),new XAttribute("id",vehicle.Native.Id),new XAttribute("class",vehicle.Native.Class),new XAttribute("save",vehicle.Native.SaveDirectory),new XElement("bag",Convert.ToBase64String(vehicle.Native.Bag)),new XElement("body",Convert.ToBase64String(vehicle.Native.Entity)));
        else if(leg.Publication.Drone is RebirthLogisticsDronePublication.Expected drone&&drone.Bag!=null&&drone.Entity!=null)
            mobile=new XElement("nativeActionDrone",new XAttribute("version",1),new XAttribute("incarnation",drone.Incarnation),new XAttribute("id",drone.Id),new XAttribute("class",drone.Class),new XAttribute("save",drone.Directory),new XElement("bag",Convert.ToBase64String(drone.Bag)),new XElement("body",Convert.ToBase64String(drone.Entity)));
        else return false;
        if(!File.Exists(path))
        {
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {using(var writer=XmlWriter.Create(output,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),CloseOutput=false})){if(tile!=null)tile.WriteActionArtifact(writer);else mobile.WriteTo(writer);writer.Flush();}output.Flush(true);}
        }
        var info=new FileInfo(path);if(info.Length<1||info.Length>48L*1024*1024)throw new InvalidDataException("Action source artifact exceeds bound");
        // Exact full artifact, never only its hash/count. Compare a streamed
        // encoding against the actual positive witness without another clone.
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var comparison=new ActionCompareStream(input,48L*1024*1024))
        using(var writer=XmlWriter.Create(comparison,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),CloseOutput=false}))
        {if(tile!=null)tile.WriteActionArtifact(writer);else mobile.WriteTo(writer);writer.Flush();comparison.RequireEnd();}
        var material=RememberActionMaterial(phase,path,48*1024*1024);VerifyActionMaterialImage(material,tile,mobile);leg.SavedMaterial=material;return FinishActionLegMaterial(payment,leg,phase);
    }
    private sealed class ActionCompareStream:Stream
    {
        private readonly Stream input;private readonly long maximum;private long count;private readonly byte[] scratch=new byte[8192];
        internal ActionCompareStream(Stream source,long limit){input=source;maximum=limit;}
        internal void RequireEnd(){if(input.ReadByte()!=-1)throw new IOException("Action artifact has trailing bytes");}
        public override void Write(byte[] bytes,int offset,int length)
        {if(length<0||count+length>maximum)throw new InvalidDataException();int consumed=0;while(consumed<length){int take=Math.Min(scratch.Length,length-consumed),received=0;while(received<take){int read=input.Read(scratch,received,take-received);if(read==0)throw new EndOfStreamException();received+=read;}for(int i=0;i<take;i++)if(scratch[i]!=bytes[offset+consumed+i])throw new IOException("Full native action artifact differs");consumed+=take;}count+=length;}
        public override void WriteByte(byte value){if(++count>maximum||input.ReadByte()!=value)throw new IOException("Native action artifact differs");}
        public override void Flush(){}public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}public override int Read(byte[] bytes,int offset,int count)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long length)=>throw new NotSupportedException();
    }
}
