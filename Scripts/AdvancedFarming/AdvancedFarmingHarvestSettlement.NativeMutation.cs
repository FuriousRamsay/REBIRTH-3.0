using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class AdvancedFarmingHarvestSettlement
{
    internal sealed class NativeMutation:IDisposable
    {
        internal World World;internal Chunk Chunk;internal Vector3i Position;internal Guid Operation,Source;
        internal uint Before,After;internal int Reads;internal bool Observed;internal IDisposable Memory;
        public void Dispose(){if(ReferenceEquals(mutating,this))mutating=null;World=null;Chunk=null;Memory?.Dispose();Memory=null;}
    }
    [ThreadStatic]private static NativeMutation mutating;
    private static string NativeMutationPath(Guid operation)
        =>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","farming-harvest","v1","source-attempts",operation.ToString("N")+".xml");
    private static string NativeMutationImage(World world,Vector3i position,Guid operation,Guid source,uint before,uint after)
        =>new XElement("harvestNativeMutation",new XAttribute("version",1),new XAttribute("world",world.worldState.Guid),
            new XAttribute("operation",operation.ToString("N")),new XAttribute("source",source.ToString("N")),
            new XAttribute("x",position.x),new XAttribute("y",position.y),new XAttribute("z",position.z),
            new XAttribute("before",before),new XAttribute("after",after)).ToString(SaveOptions.DisableFormatting);
    internal static NativeMutation BeginNativeMutation(World world,Vector3i position,Guid operation,Guid source,uint before,uint after)
    {
        NativeMutation scope=null;
        try
        {
            if(mutating!=null||operation==Guid.Empty||source==Guid.Empty||before==after||world?.IsRemote()!=false||!ThreadManager.IsMainThread()||
                !ReferenceEquals(GameManager.Instance?.World,world)||world.GetBlock(position).rawData!=before||!MatchesSource(world,position,source.ToString("N"))||
                !SourceChunk(world,position,out var chunk,out _)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var memory))return null;
            scope=new NativeMutation{World=world,Chunk=chunk,Position=position,Operation=operation,Source=source,Before=before,After=after,Memory=memory};
            string path=NativeMutationPath(operation);
            if(File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak"))return null;
            string image=NativeMutationImage(world,position,operation,source,before,after);
            Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] bytes=Encoding.UTF8.GetBytes(image);
            using(var file=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(File.ReadAllText(path+".tmp",new UTF8Encoding(false,true))!=image||!RebirthDurableFileCommit.TryPublish(path+".tmp",path,out _)||File.ReadAllText(path,new UTF8Encoding(false,true))!=image)return null;
            mutating=scope;var admitted=scope;scope=null;return admitted;
        }
        catch{return null;}
        finally{scope?.Dispose();}
    }
    internal static object EnterNativeMutation(Chunk chunk,WorldBase world,Vector3i local,SourceBefore before)
    {
        var scope=mutating;var position=new Vector3i(chunk.X*16+local.x,local.y,chunk.Z*16+local.z);
        if(scope==null||!ReferenceEquals(scope.World,world)||!ReferenceEquals(scope.Chunk,chunk)||scope.Position!=position)return null;
        scope.Reads++;return scope.Reads==1&&before.Block.rawData==scope.Before?scope:null;
    }
    internal static void ObserveNativeMutation(Chunk chunk,WorldBase world,Vector3i local,bool run,object state)
    {
        if(state is not NativeMutation scope||!ReferenceEquals(mutating,scope)||!run||scope.Reads!=1||!ReferenceEquals(scope.Chunk,chunk)||!ReferenceEquals(scope.World,world))return;
        scope.Observed=chunk.GetBlock(local).rawData==scope.After;
    }
    internal static bool PublishNativeMutation(NativeMutation scope)
    {
        if(scope==null||!ReferenceEquals(mutating,scope)||!scope.Observed||scope.Reads!=1||!ReferenceEquals(GameManager.Instance?.World,scope.World)||
            !ReferenceEquals(scope.World.GetChunkSync(World.toChunkXZ(scope.Position.x),World.toChunkXZ(scope.Position.z)),scope.Chunk)||
            scope.World.GetBlock(scope.Position).rawData!=scope.After)return false;
        return BindDestination(scope.World,scope.Position,scope.Operation,new BlockValue{rawData=scope.After})&&
            RecordCommit(scope.World,scope.Position,scope.Operation,scope.Source,scope.Before,scope.After);
    }
    internal static bool TryRestoreDestinationCommit(World world,Vector3i position,Guid operation,Guid source,uint before,uint after)
    {
        if(HasCommit(world,position,operation,source,before,after))return true;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
            operation==Guid.Empty||source==Guid.Empty||before==after||!SourceChunk(world,position,out var chunk,out _)||
            chunk.ChunkCustomData.dict.ContainsKey(CommitPrefix+operation.ToString("N"))||
            world.GetBlock(position).rawData!=after||!MatchesSource(world,position,operation.ToString("N")))return false;
        // Only the observed native mutation binds the destination to its
        // operation ID. Require its exact durable before/after intent as well.
        try
        {
            var file=new FileInfo(NativeMutationPath(operation));
            if(!file.Exists||file.Length<1||file.Length>2048||(file.Attributes&FileAttributes.ReparsePoint)!=0||
                File.ReadAllText(file.FullName,new UTF8Encoding(false,true))!=NativeMutationImage(world,position,operation,source,before,after))return false;
            return MatchesSource(world,position,operation.ToString("N"))&&RecordCommit(world,position,operation,source,before,after);
        }
        catch{return false;}
    }
}
