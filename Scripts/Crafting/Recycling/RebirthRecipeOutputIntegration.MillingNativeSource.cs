using System;
using System.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    // The common reader observes a native source. Its typed purpose remains
    // milling-owned and cannot create an owner Load/callback capability.
    internal sealed class MillingNativeSource:IDisposable
    {
        private StationOutputNativeRequest request;
        private MillingNativeSource(StationOutputNativeRequest value){request=value;}
        internal TileEntityWorkstation Tile=>Current()?request.Loaded.Tile:null;
        internal bool Current()=>request?.MillingPurpose?.Current()==true&&request.Loaded?.Current()==true;
        public void Dispose()
        {
            lock(stationNativeLoadGate)if(request!=null)
            {request.Dispose();if(!request.Loading)stationNativeLoadRequests.Remove(request);request=null;}
        }
        internal static MillingNativeSource Register(RebirthCookingMillingPublication.StaticSourcePurpose purpose,byte[] body,int elapsed)
        {
            if(purpose?.Current()!=true||body==null||!RebirthProductiveEarnedNativeBody.TryMeasure(body,elapsed,out long graph,out long scratch)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*body.Length+graph+scratch+65536),out var memory))return null;
            var position=purpose.Position;
            var request=new StationOutputNativeRequest{Operation=purpose.Operation,World=purpose.World,Save=purpose.Save,Birth=purpose.Birth,
                OriginalHash=purpose.Hash,Original=purpose.Intent,Image=purpose.Image,Grid=purpose.Output,
                MillingPurpose=purpose,X=position.x>>4,Z=position.z>>4,Position=position,Body=(byte[])body.Clone(),Elapsed=elapsed,
                ReturnedSpan=new byte[body.Length+10],Graph=graph,Scratch=scratch,Memory=memory};
            lock(stationNativeLoadGate)stationNativeLoadRequests.Add(request);
            return new MillingNativeSource(request);
        }
    }
}
