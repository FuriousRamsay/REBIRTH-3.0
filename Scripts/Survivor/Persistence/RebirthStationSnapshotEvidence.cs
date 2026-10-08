using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

// Demand-scoped serializer evidence only. This is NOT a disk/publication/payment witness.
// The dispatcher must keep the request alive through successful native publication and
// separately reconcile exact station contents. Pooled snapshot reuse invalidates evidence.
public static class RebirthStationSnapshotEvidence
{
    public const int MaximumRequests = 4;
    public const int MaximumSnapshotBytes = 8 * 1024 * 1024;
    private static readonly object gate = new object();
    private static readonly List<Request> requests = new List<Request>();
    private static readonly ConditionalWeakTable<object, State> states = new ConditionalWeakTable<object, State>();
    [ThreadStatic] private static Frame active;
    private sealed class State { public long Generation; public Request Request; public byte[] Bytes; public int InputStart,InputLength,QueueStart,QueueLength,OutputStart,OutputLength,CompletionStart,CompletionLength; }
    public sealed class Request : IDisposable
    {
        internal object World, Chunk, Tile; internal Func<bool> Observe; internal Func<byte[],byte[],bool> ValidateStation; internal Func<byte[],byte[],bool> ValidateTerminal; internal int ThreadId; internal int X, Z; internal bool Closed;
        internal object Snapshot; internal long Generation; internal bool Published; internal Publication Proof;
        internal Request(object world,object chunk,int x,int z,Func<bool> observe){World=world;Chunk=chunk;X=x;Z=z;Observe=observe;ThreadId=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        public void Dispose(){lock(gate){Closed=true;requests.Remove(this);Invalidate(Snapshot);Snapshot=null;World=null;}}
    }
    public sealed class Frame
    {
        internal Frame Previous; internal object Snapshot, Chunk; internal Request Request;
        internal long Generation; internal Stream SerializedStream, StationStream; internal bool Serialized, InvalidStation; internal byte[] InputBytes,QueueBytes,OutputBytes,CompletionBytes; internal long InputStart,QueueStart,OutputStart,CompletionStart;
    }
    // Caller authenticates its station admission first. No watch exists during ordinary saves.
    public static Request Watch(object world,object chunk,int chunkX,int chunkZ,Func<bool> observe=null,object tile=null,Func<byte[],byte[],bool> validateStation=null,Func<byte[],byte[],bool> validateTerminal=null)
    {
        if(world==null||chunk==null||validateTerminal!=null&&(tile==null||validateStation==null))return null;
        lock(gate){if(requests.Count>=MaximumRequests)return null;
            foreach(var r in requests)if(ReferenceEquals(r.World,world)&&r.X==chunkX&&r.Z==chunkZ)return null;
            var request=new Request(world,chunk,chunkX,chunkZ,observe){Tile=tile,ValidateStation=validateStation,ValidateTerminal=validateTerminal};requests.Add(request);return request;}
    }
    public static Frame Begin(object snapshot,object chunk,object world,int x,int z,bool serializing)
    {
        if(snapshot==null)return null;
        lock(gate){Invalidate(snapshot);Request request=null;
            if(serializing)foreach(var r in requests)if(!r.Closed&&ReferenceEquals(r.World,world)&&ReferenceEquals(r.Chunk,chunk)&&r.ThreadId==System.Threading.Thread.CurrentThread.ManagedThreadId&&r.X==x&&r.Z==z){request=r;break;}
            if(request==null)return null;
            try{if(request.Observe!=null&&!request.Observe())return null;}catch{return null;}
            Invalidate(request.Snapshot);request.Snapshot=null;request.Published=false;request.Proof=null;
            var state=states.GetOrCreateValue(snapshot);state.Generation++;
            var frame=new Frame{Previous=active,Snapshot=snapshot,Chunk=chunk,Request=request,Generation=state.Generation};
            active=frame;return frame;}
    }
    public static long StartStationSpan(object tile,Stream stream)
    {var f=active;return f!=null&&f.Request!=null&&ReferenceEquals(f.Request.Tile,tile)&&stream!=null&&stream.CanSeek?stream.Position:-1;}
    public static long StartTerminalSpan(object tile,Stream stream)
    {return active?.Request?.ValidateTerminal!=null?StartStationSpan(tile,stream):-1;}
    public static void CaptureStationSpan(object tile,Stream stream,long start,bool input)
    {CaptureSpan(tile,stream,start,input?0:1);}
    public static void CaptureTerminalSpan(object tile,Stream stream,long start,bool output)
    {CaptureSpan(tile,stream,start,output?2:3);}
    private static void CaptureSpan(object tile,Stream stream,long start,int kind)
    {
        var f=active;if(start<0||f==null||f.Request==null||!ReferenceEquals(f.Request.Tile,tile))return;
        try{long end=stream.Position,length=end-start;
            if(length<=0||length>256*1024||!stream.CanRead||
                f.StationStream!=null&&!ReferenceEquals(f.StationStream,stream)||
                (kind==0?f.InputBytes!=null:kind==1?f.QueueBytes!=null:kind==2?f.OutputBytes!=null:f.CompletionBytes!=null)){f.InvalidStation=true;return;}
            var bytes=new byte[(int)length];
            try{stream.Position=start;int offset=0;while(offset<bytes.Length){int n=stream.Read(bytes,offset,bytes.Length-offset);if(n<=0){f.InvalidStation=true;return;}offset+=n;}}
            finally{stream.Position=end;}
            f.StationStream=stream;if(kind==0){f.InputBytes=bytes;f.InputStart=start;}else if(kind==1){f.QueueBytes=bytes;f.QueueStart=start;}else if(kind==2){f.OutputBytes=bytes;f.OutputStart=start;}else{f.CompletionBytes=bytes;f.CompletionStart=start;}
        }catch{f.InvalidStation=true;}
    }    public static void Serialized(object chunk,Stream stream)
    {
        var frame=active;
        if(frame!=null&&frame.Request!=null&&ReferenceEquals(frame.Chunk,chunk))
        {try{if(frame.Request.Observe!=null&&!frame.Request.Observe())return;}catch{return;}frame.Serialized=true;frame.SerializedStream=stream;}
    }
    public static void Finish(Frame frame,Stream finalStream,Exception error)
    {
        if(frame==null)return;
        bool sameFrame=ReferenceEquals(active,frame);active=frame.Previous;
        if(!sameFrame||error!=null||frame.Request==null||!frame.Serialized||
            !ReferenceEquals(frame.SerializedStream,finalStream))return;
        if(frame.Request.ValidateStation!=null){try{if(frame.InvalidStation||!ReferenceEquals(frame.StationStream,finalStream)||
            frame.InputBytes==null||frame.QueueBytes==null||!frame.Request.ValidateStation(frame.InputBytes,frame.QueueBytes))return;}catch{return;}}
        if(frame.Request.ValidateTerminal!=null){try{if(frame.InvalidStation||frame.OutputBytes==null||frame.CompletionBytes==null||!frame.Request.ValidateTerminal(frame.OutputBytes,frame.CompletionBytes))return;}catch{return;}}
        byte[] bytes;
        try{
            if(finalStream==null||!finalStream.CanRead||!finalStream.CanSeek||finalStream.Position!=0||
                finalStream.Length<=8||finalStream.Length>MaximumSnapshotBytes)return;
            long original=finalStream.Position;
            try{bytes=new byte[(int)finalStream.Length];int offset=0;
                while(offset<bytes.Length){int n=finalStream.Read(bytes,offset,bytes.Length-offset);if(n<=0)return;offset+=n;}}
            finally{finalStream.Position=original;}
            if(bytes[0]!=116||bytes[1]!=116||bytes[2]!=99||bytes[3]!=0||bytes[4]!=47||bytes[5]!=0||bytes[6]!=0||bytes[7]!=0)return;
        }catch{return;}
        if(frame.Request.ValidateStation!=null&&(!SpanMatches(bytes,frame.InputStart,frame.InputBytes)||!SpanMatches(bytes,frame.QueueStart,frame.QueueBytes)))return;
        if(frame.Request.ValidateTerminal!=null&&(!SpanMatches(bytes,frame.OutputStart,frame.OutputBytes)||!SpanMatches(bytes,frame.CompletionStart,frame.CompletionBytes)))return;
        lock(gate){if(frame.Request.Closed||!states.TryGetValue(frame.Snapshot,out var state)||state.Generation!=frame.Generation)return;
            if(!ReferenceEquals(frame.Request.Snapshot,frame.Snapshot))Invalidate(frame.Request.Snapshot);
            state.Bytes=bytes;state.InputStart=(int)frame.InputStart;state.QueueStart=(int)frame.QueueStart;state.InputLength=frame.InputBytes?.Length??0;state.QueueLength=frame.QueueBytes?.Length??0;state.OutputStart=(int)frame.OutputStart;state.CompletionStart=(int)frame.CompletionStart;state.OutputLength=frame.OutputBytes?.Length??0;state.CompletionLength=frame.CompletionBytes?.Length??0;state.Request=frame.Request;frame.Request.Snapshot=frame.Snapshot;frame.Request.Generation=frame.Generation;}
    }
    private static bool SpanMatches(byte[] snapshot,long start,byte[] span)
    {
        if(snapshot==null||span==null||start<8||start>snapshot.Length-span.Length)return false;
        for(int i=0;i<span.Length;i++)if(snapshot[(int)start+i]!=span[i])return false;
        return true;
    }    public static void Invalidate(object snapshot)
    {if(snapshot==null)return;lock(gate){if(states.TryGetValue(snapshot,out var s)){s.Generation++;s.Bytes=null;s.Request=null;}}}
    public static bool TryCopy(Request request,out byte[] serialized)
    {
        serialized=null;
        lock(gate){if(request==null||request.Closed||request.Snapshot==null||!states.TryGetValue(request.Snapshot,out var s)||
            s.Request!=request||s.Generation!=request.Generation||s.Bytes==null)return false;
            serialized=(byte[])s.Bytes.Clone();return true;}
    }
    internal static bool TryGetPublication(object snapshot,out Request request,out byte[] bytes)
    {
        request=null;bytes=null;lock(gate){if(snapshot==null||!states.TryGetValue(snapshot,out var s)||s.Request==null||s.Request.Closed||s.Bytes==null)return false;
            request=s.Request;bytes=(byte[])s.Bytes.Clone();return true;}
    }
    internal static void MarkPublished(Request request,object snapshot,long generation,string path=null,string digest=null)
    {lock(gate){if(request!=null&&!request.Closed&&request.Snapshot==snapshot&&request.Generation==generation&&
        states.TryGetValue(snapshot,out var s)&&s.Generation==generation&&s.Request==request){request.Published=true;
        if(path!=null&&digest!=null&&s.InputLength>0&&s.QueueLength>0)request.Proof=new Publication(path,digest,request.X,request.Z,s.InputStart,s.InputLength,s.QueueStart,s.QueueLength,s.OutputStart,s.OutputLength,s.CompletionStart,s.CompletionLength);}}}
    public static bool HasPublished(Request request)
    {lock(gate){return request!=null&&!request.Closed&&request.Published;}}    public sealed class Publication
    {
        public readonly string Path,PayloadDigest; public readonly int ChunkX,ChunkZ,InputStart,InputLength,QueueStart,QueueLength,OutputStart,OutputLength,CompletionStart,CompletionLength;
        internal Publication(string path,string digest,int x,int z,int inputStart,int inputLength,int queueStart,int queueLength,int outputStart,int outputLength,int completionStart,int completionLength)
        {Path=path;PayloadDigest=digest;ChunkX=x;ChunkZ=z;InputStart=inputStart;InputLength=inputLength;QueueStart=queueStart;QueueLength=queueLength;OutputStart=outputStart;OutputLength=outputLength;CompletionStart=completionStart;CompletionLength=completionLength;}
    }
    public static bool TryGetPublished(Request request,out Publication proof)
    {lock(gate){proof=request!=null&&!request.Closed&&request.Published?request.Proof:null;return proof!=null;}}}