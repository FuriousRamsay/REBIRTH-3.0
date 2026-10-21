using System;

public static partial class RebirthRecipeOutputIntegration
{
    internal sealed class AuthorityLocalNativeSource:IDisposable
    {
        private StationOutputNativeRequest request;private StationOutputNativeLoaded bound;
        private AuthorityLocalNativeSource(StationOutputNativeRequest value){request=value;}
        internal TileEntityWorkstation Tile=>Current()?request.Loaded.Tile:null;
        internal bool Current()=>request?.AuthorityLocalPurpose?.InputsCurrent()==true&&request.Loaded?.Current()==true;
        internal bool SealIdentity(){if(!Current())return false;bound=request.Loaded;return true;}
        internal bool IdentityCurrent()
        {
            var source=request;var loaded=bound;
            return source?.AuthorityLocalPurpose?.InputsCurrent()==true&&loaded!=null&&ReferenceEquals(source.Loaded,loaded)&&
                ReferenceEquals(GameManager.Instance?.World,source.World)&&source.World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&
                ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,loaded.Util)&&ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,loaded.Access)&&
                ReferenceEquals(source.World.GetChunkSync(loaded.Chunk.X,loaded.Chunk.Z),loaded.Chunk)&&!loaded.Chunk.InProgressUnloading&&
                ReferenceEquals(loaded.Chunk.GetTileEntity(loaded.Tile.localChunkPos),loaded.Tile)&&ReferenceEquals(loaded.Tile.GetChunk(),loaded.Chunk)&&
                loaded.Tile.ToWorldPos()==source.Position&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(source.World,loaded.Tile,out var birth)&&birth==source.Birth&&
                RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(source.World,loaded.Tile,out var fence)&&fence==loaded.Fence;
        }
        public void Dispose(){lock(stationNativeLoadGate)if(request!=null){request.Dispose();if(!request.Loading)stationNativeLoadRequests.Remove(request);request=null;bound=null;}}
        internal static AuthorityLocalNativeSource Register(RebirthProductiveEarnedUiDeferredTransport.AuthorityLocalLoadedSource purpose)
        {
            if(purpose?.InputsCurrent()!=true||!RebirthProductiveEarnedNativeBody.TryMeasure(purpose.Body,purpose.Offset,out long graph,out long scratch)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*purpose.Body.Length+graph+scratch+65536),out var memory))return null;
            var p=purpose.Position;
            var request=new StationOutputNativeRequest{Operation=purpose.Operation,World=purpose.World,Save=purpose.Save,Birth=purpose.Birth.ToString("N"),
                OriginalHash=purpose.Hash,Original=purpose.Image,Image=purpose.Original,Grid=Array.Empty<byte>(),AuthorityLocalPurpose=purpose,
                X=p.x>>4,Z=p.z>>4,Position=p,Body=(byte[])purpose.Body.Clone(),Elapsed=purpose.Offset,ReturnedSpan=new byte[purpose.Body.Length+10],Graph=graph,Scratch=scratch,Memory=memory};
            lock(stationNativeLoadGate)stationNativeLoadRequests.Add(request);return new AuthorityLocalNativeSource(request);
        }
    }
}
