using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Only CaptureAfter, under the completed actual native invocation, can stamp
// a child effect. Recovery observes exact existing stamps and never invents one.
internal static class RebirthRemoteResourceBlockFootprintPublication
{
    private sealed class Watch
    {
        internal World World;internal Chunk Chunk;internal Func<bool> Current;
        internal RebirthStationSnapshotEvidence.Request Request;internal float Started;
        internal byte[] Stamp,Birth;internal RebirthRemoteResourceBlockFootprintAdmission.BoundCell Bound;
    }
    private static readonly Dictionary<string,Watch> watches=new Dictionary<string,Watch>();
    private static World boundWorld;private static string boundDirectory;
    private static string Suffix(Vector3i position)=>position.x+"_"+position.y+"_"+position.z;
    private static string Key(Guid operation,Vector3i position)=>"rebirth.remote.block.child.v1:"+operation.ToString("N")+":"+Suffix(position);
    private static string WatchKey(Guid operation,Vector3i position)=>operation.ToString("N")+"|"+Suffix(position);
    private static RebirthRemoteResourceClientOriginalJournal Journal(Vector3i position)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteBlockChildSaved_"+Suffix(position)));
    private static void Bind(World world)
    {
        string directory=GameIO.GetSaveGameDir();if(ReferenceEquals(boundWorld,world)&&boundDirectory==directory)return;
        foreach(var watch in watches.Values)watch.Request?.Dispose();watches.Clear();boundWorld=world;boundDirectory=directory;
    }
    private static string Effect(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,
        RebirthRemoteResourceBlockFootprintAdmission.BoundCell bound,string afterBirth,byte[] birth)
    {
        var cell=bound.Cell;
        return new XElement("blockChildEffect",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",command.Digest),
            new XAttribute("x",cell.Position.x),new XAttribute("y",cell.Position.y),new XAttribute("z",cell.Position.z),new XAttribute("beforeBirth",bound.Birth),new XAttribute("afterBirth",afterBirth),
            new XAttribute("raw",cell.After.rawData),new XAttribute("damage",cell.After.damage),new XAttribute("density",cell.AfterDensity),new XElement("birthImage",Convert.ToBase64String(birth))).ToString(SaveOptions.DisableFormatting);
    }
    private static bool ReadEffect(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,
        RebirthRemoteResourceBlockFootprintAdmission.BoundCell bound,byte[] stamp,out byte[] birth)
    {
        birth=null;
        try
        {
            if(stamp==null||stamp.Length>4096)return false;string text=new UTF8Encoding(false,true).GetString(stamp);
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,4096);string after=(string)body.Attribute("afterBirth"),encoded=body.Element("birthImage")?.Value;
            if(encoded==null||encoded.Length>1368)return false;birth=Convert.FromBase64String(encoded);
            return Convert.ToBase64String(birth)==encoded&&Effect(original,command,bound,after,birth)==text&&
                RebirthOriginalNativeBlockIdentity.IsRecordedClaim(world,birth,1,original.Operation,original.Digest,bound.Birth,after,bound.Cell.After.rawData);
        }
        catch{birth=null;return false;}
    }
    internal static bool CaptureAfter(RebirthRemoteResourceBlockActorScope.Scope scope)
    {
        try
        {
            if(scope?.InvocationCompleted!=true||scope.Calls!=1||!RebirthRemoteResourceBlockCommand.TryReadAcceptedWorld(scope.World,scope.Original,out var root,out var command)||
                root.Phase!=2||command.Digest!=scope.Accepted.Digest||!RebirthRemoteResourceBlockFootprintAdmission.TryRead(scope.Original,command,out var cells))return false;
            // Preflight the complete native postimage before stamping any cell.
            var images=new List<Tuple<RebirthRemoteResourceBlockFootprintAdmission.BoundCell,Chunk,byte[]>>();
            foreach(var bound in cells)
            {
                var cell=bound.Cell;var actual=scope.World.GetBlock(cell.Position);
                var chunk=scope.World.GetChunkSync(World.toChunkXZ(cell.Position.x),World.toChunkXZ(cell.Position.z)) as Chunk;
                if(chunk==null||chunk.InProgressUnloading||chunk.ChunkCustomData.dict.Count>=ushort.MaxValue||chunk.ChunkCustomData.dict.ContainsKey(Key(scope.Original.Operation,cell.Position))||
                    actual.rawData!=cell.After.rawData||actual.damage!=cell.After.damage||scope.World.GetDensity(cell.Position)!=cell.AfterDensity||
                    !RebirthOriginalNativeBlockIdentity.TryReadClaim(scope.World,cell.Position,1,scope.Original.Operation,scope.Original.Digest,out var before,out var after)||before!=bound.Birth)return false;
                byte[] birth=RebirthOriginalNativeBlockIdentity.CopyMarker(scope.World,cell.Position);if(birth==null)return false;
                byte[] stamp=Encoding.UTF8.GetBytes(Effect(scope.Original,command,bound,after,birth));if(stamp.Length>4096)return false;
                images.Add(Tuple.Create(bound,chunk,stamp));
            }
            foreach(var image in images)
            {
                string key=Key(scope.Original.Operation,image.Item1.Cell.Position);
                image.Item2.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=image.Item3});image.Item2.isModified=true;
            }
            return true;
        }
        catch{return false;}
    }
    private static string Saved(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,byte[] stamp,string regionProof)
        =>new XElement("blockChildSaved",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",command.Digest),new XAttribute("regionProof",regionProof),
            new XElement("actualEffect",Convert.ToBase64String(stamp))).ToString(SaveOptions.DisableFormatting);
    private static bool ValidSaved(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,
        RebirthRemoteResourceBlockFootprintAdmission.BoundCell cell,byte[] bytes,out string digest)
    {
        digest=null;
        try
        {
            if(bytes==null||bytes.Length>8192)return false;string text=new UTF8Encoding(false,true).GetString(bytes);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,8192);
            string proof=(string)body.Attribute("regionProof"),encoded=body.Element("actualEffect")?.Value;
            if(proof?.Length!=64||proof.Any(value=>!(value>='0'&&value<='9'||value>='a'&&value<='f'))||encoded==null||encoded.Length>5464)return false;
            byte[] stamp=Convert.FromBase64String(encoded);
            if(Convert.ToBase64String(stamp)!=encoded||!ReadEffect(world,original,command,cell,stamp,out _)||Saved(original,command,stamp,proof)!=text)return false;
            digest=RebirthRemoteResourceDurableJournal.Digest(text);return true;
        }
        catch{return false;}
    }
    private static bool TrySaved(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,
        RebirthRemoteResourceBlockFootprintAdmission.BoundCell cell,out string digest)
    {
        digest=null;var record=Journal(cell.Cell.Position).Read(original.Operation);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Operation&&record.OwnerCreation==original.Creation&&record.Revision==0&&
            record.Before.SequenceEqual(record.After)&&ValidSaved(world,original,command,cell,record.Before,out digest);
    }
    internal static bool TryReadSaved(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,out string proof)
    {
        proof=null;
        try
        {
            if(!RebirthRemoteResourceBlockFootprintAdmission.TryRead(original,command,out var cells))return false;var hashes=new List<string>();
            foreach(var cell in cells){if(!TrySaved(world,original,command,cell,out var digest))return false;hashes.Add(digest);}
            proof=RebirthRemoteResourceDurableJournal.Digest("block-child-publication|"+original.Digest+"|"+command.Digest+"|"+string.Join("|",hashes));return true;
        }
        catch{return false;}
    }
    internal static bool Retire(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command)
    {
        try
        {
            if(!TryReadSaved(world,original,command,out _)||!RebirthRemoteResourceBlockFootprintAdmission.TryRead(original,command,out var cells))return false;
            bool all=true;
            foreach(var cell in cells)
            {
                if(!RebirthRemoteResourceBlockSettlement.HasSavedTerminal(world,original.Operation,original.Digest,cell.Cell.Position,cell.Birth)){all=false;continue;}
                var chunk=world.GetChunkSync(World.toChunkXZ(cell.Cell.Position.x),World.toChunkXZ(cell.Cell.Position.z)) as Chunk;
                if(chunk==null||chunk.InProgressUnloading){all=false;continue;}
                var record=Journal(cell.Cell.Position).Read(original.Operation);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),8192);
                byte[] expected=Convert.FromBase64String(body.Element("actualEffect").Value);string key=Key(original.Operation,cell.Cell.Position);
                if(chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
                {
                    if(marker?.data==null||!marker.data.SequenceEqual(expected)||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null){all=false;continue;}
                    chunk.ChunkCustomData.Remove(key);chunk.isModified=true;
                }
                if(!RebirthOriginalNativeBlockIdentity.Retire(world,cell.Cell.Position,1,original.Operation,original.Digest,cell.Birth))all=false;
                string watchKey=WatchKey(original.Operation,cell.Cell.Position);
                if(watches.TryGetValue(watchKey,out var watch)){watch.Request?.Dispose();watches.Remove(watchKey);}
            }
            return all;
        }
        catch{return false;}
    }
    private static bool Current(Watch watch,Guid operation)
    {
        var cell=watch.Bound.Cell;var actual=watch.World.GetBlock(cell.Position);
        return watch.Current?.Invoke()==true&&actual.rawData==cell.After.rawData&&actual.damage==cell.After.damage&&watch.World.GetDensity(cell.Position)==cell.AfterDensity&&
            watch.Chunk.ChunkCustomData.dict.TryGetValue(Key(operation,cell.Position),out var marker)&&marker?.data!=null&&marker.data.SequenceEqual(watch.Stamp)&&
            marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null&&
            (RebirthOriginalNativeBlockIdentity.CopyMarker(watch.World,cell.Position)??Array.Empty<byte>()).SequenceEqual(watch.Birth);
    }
    private static bool PublishPositive(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,
        RebirthRemoteResourceBlockFootprintAdmission.BoundCell cell,byte[] stamp,byte[] birth,byte[] bytes)
    {
        if(!RebirthOriginalNativeBlockSnapshot.TryProve(bytes,World.toChunkXZ(cell.Cell.Position.x),World.toChunkXZ(cell.Cell.Position.z),cell.Cell.Position,
            cell.Cell.After.rawData,(ushort)cell.Cell.After.damage,Key(original.Operation,cell.Cell.Position),stamp,out var proof,cell.Cell.AfterDensity,
            RebirthOriginalNativeBlockIdentity.NativeKey(cell.Cell.Position),birth))return false;
        var journal=Journal(cell.Cell.Position);byte[] saved=Encoding.UTF8.GetBytes(Saved(original,command,stamp,proof));var prior=journal.Read(original.Operation);
        if(prior!=null)
        {
            if(prior.Vehicle!=original.Operation||prior.OwnerCreation!=original.Creation||prior.Revision!=0||!prior.Before.SequenceEqual(prior.After)||
                !ValidSaved(world,original,command,cell,prior.Before,out _))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(prior.Before),8192);
            if(body.Element("actualEffect")?.Value!=Convert.ToBase64String(stamp))return false;
            saved=prior.Before;
        }
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,saved,saved);
        if(!record.Before.SequenceEqual(saved)||!record.After.SequenceEqual(saved))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    // Sharing the exact positive native Request avoids competing watches for
    // the parent's chunk. An arbitrary caller byte image is not a witness API.
    internal static bool Observe(World world,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceBlockCommand.Accepted command,Chunk publishedChunk=null,RebirthStationSnapshotEvidence.Request publishedRequest=null)
    {
        try
        {
            Bind(world);if(!RebirthRemoteResourceBlockFootprintAdmission.TryRead(original,command,out var cells))return false;bool all=true;
            byte[] publishedBytes=null;
            if(publishedRequest!=null&&ReferenceEquals(publishedRequest.World,world)&&ReferenceEquals(publishedRequest.Chunk,publishedChunk)&&
                RebirthStationSnapshotEvidence.HasPublished(publishedRequest))RebirthStationSnapshotEvidence.TryCopy(publishedRequest,out publishedBytes);
            foreach(var cell in cells)
            {
                string key=WatchKey(original.Operation,cell.Cell.Position);
                if(TrySaved(world,original,command,cell,out _)){if(watches.TryGetValue(key,out var done)){done.Request?.Dispose();watches.Remove(key);}continue;}
                all=false;
                if(watches.TryGetValue(key,out var old)&&old.Current?.Invoke()!=true){old.Request?.Dispose();watches.Remove(key);}
                if(!watches.TryGetValue(key,out var watch))
                {
                    if(watches.Count>=120||!RebirthRemoteResourceBlockHost.Ready(world,cell.Cell.Position,out var chunk,out _,out var provider)||
                        !chunk.ChunkCustomData.dict.TryGetValue(Key(original.Operation,cell.Cell.Position),out var marker)||marker?.data==null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null||
                        !ReadEffect(world,original,command,cell,marker.data,out var birth))continue;
                    watch=new Watch{World=world,Chunk=chunk,Current=provider,Bound=cell,Stamp=(byte[])marker.data.Clone(),Birth=birth};watches[key]=watch;
                }
                if(ReferenceEquals(watch.Chunk,publishedChunk)&&publishedBytes!=null&&PublishPositive(world,original,command,cell,watch.Stamp,watch.Birth,publishedBytes))
                {watch.Request?.Dispose();watches.Remove(key);continue;}
                if(watch.Request!=null)
                {
                    if(RebirthStationSnapshotEvidence.HasPublished(watch.Request)&&RebirthStationSnapshotEvidence.TryCopy(watch.Request,out var bytes)&&
                        RebirthOriginalNativeBlockSnapshot.TryProve(bytes,watch.Chunk.X,watch.Chunk.Z,cell.Cell.Position,cell.Cell.After.rawData,(ushort)cell.Cell.After.damage,
                            Key(original.Operation,cell.Cell.Position),watch.Stamp,out var proof,cell.Cell.AfterDensity,RebirthOriginalNativeBlockIdentity.NativeKey(cell.Cell.Position),watch.Birth))
                    {
                        if(PublishPositive(world,original,command,cell,watch.Stamp,watch.Birth,bytes)){watch.Request.Dispose();watches.Remove(key);}continue;
                    }
                    if(UnityEngine.Time.realtimeSinceStartup-watch.Started<15f)continue;watch.Request.Dispose();watch.Request=null;
                }
                if(!Current(watch,original.Operation))continue;
                watch.Request=RebirthStationSnapshotEvidence.Watch(world,watch.Chunk,watch.Chunk.X,watch.Chunk.Z,watch.Current);if(watch.Request==null)continue;
                watch.Started=UnityEngine.Time.realtimeSinceStartup;
                lock(watch.Chunk)
                {
                    if(!RebirthRemoteResourceBlockHost.Ready(world,cell.Cell.Position,out _,out var provider,out _)){watch.Request.Dispose();watch.Request=null;continue;}
                    watch.Chunk.InProgressSaving=true;try{provider.m_RegionFileManager.SaveChunkSnapshot(watch.Chunk,true);}finally{watch.Chunk.InProgressSaving=false;}
                }
            }
            return all;
        }
        catch{return false;}
    }
}
