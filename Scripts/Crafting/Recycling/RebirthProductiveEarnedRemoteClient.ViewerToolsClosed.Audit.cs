#if REBIRTH_DEBUG
using System;using System.IO;using System.Linq;using System.Text;using Newtonsoft.Json.Linq;
internal static partial class RebirthProductiveEarnedRemoteClient
{
    internal static JArray ViewerToolsFixtureStatus()
    {
        var rows=new JArray();
        foreach(var value in viewerToolsClosures.Values.Take(4))
            rows.Add(new JObject{["operation"]=value.Record.Operation.ToString("N"),["birth"]=value.Record.Birth.ToString("N"),["nonce"]=value.Record.Nonce.ToString("N"),
                ["digest"]=value.Record.OriginalDigest,["payer"]=value.Record.Owner,["deaths"]=value.Record.Deaths,["nativeViewerToolsOnly"]=true,
                ["metadataQuery"]=true,["nativeViewerToolsReleased"]=false,["nativeViewerToolsHeld"]=true,["unknown"]=viewerToolsClosureUnknown});
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(actor==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var owner))return rows;
        string world=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
        string folder=Path.Combine(Path.GetFullPath(GameIO.GetPlayerDataDir()),"RebirthProductiveViewerToolsOwner",world.Replace('/','_').Replace('+','-'),owner.StorageKey);
        foreach(string closed in RebirthProductiveEarnedClosedAudit.Paths(folder,".rew.viewerToolsClosed"))
        {
            try
            {
                string path=closed.Substring(0,closed.Length-".viewerToolsClosed".Length);
                if(!ReadViewerToolClosed(path,world,owner.StorageKey,creation,out var record))continue;
                var row=rows.OfType<JObject>().FirstOrDefault(item=>(string)item["operation"]==record.Operation.ToString("N"));if(row==null){row=new JObject();rows.Add(row);}
                row["operation"]=record.Operation.ToString("N");row["birth"]=record.Birth.ToString("N");row["nonce"]=record.Nonce.ToString("N");row["digest"]=record.OriginalDigest;
                row["payer"]=record.Owner;row["deaths"]=record.Deaths;row["nativeViewerToolsOnly"]=true;row["metadataQuery"]=false;
                row["nativeViewerToolsReleased"]=true;row["nativeViewerToolsHeld"]=viewerToolsClosures.ContainsKey(record.Operation);row["diskOnly"]=true;row["unknown"]=false;
            }
            catch{/* Read-only invalid receipt is never reported as released. */}
        }
        foreach(var value in originals.Values.Where(item=>item.ViewerToolsOnly).Take(4))
        {
            if(rows.OfType<JObject>().Any(row=>(string)row["operation"]==value.Frame.Operation.ToString("N")))continue;
            rows.Add(new JObject{["operation"]=value.Frame.Operation.ToString("N"),["birth"]=value.Frame.Birth.ToString("N"),["nonce"]=value.Nonce.ToString("N"),["digest"]=value.Digest,
                ["payer"]=value.Owner,["deaths"]=value.Deaths,["nativeViewerToolsOnly"]=true,["nativeViewerToolsReleased"]=false,["nativeViewerToolsHeld"]=true,
                ["ownerUpload"]=SavedNativeCloseStage(value.Path,new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=value.Frame.Operation,Nonce=value.Nonce,OriginalDigest=value.Digest},"nativeToolsOwnerUpload"),
                ["syncAttempt"]=value.Tools?.SyncEntered==true,["syncReturned"]=value.Tools?.SyncReturned==true,["savedEcho"]=value.Tools?.SavedEcho==true,["saved"]=value.Tools?.Saved==true});
        }
        return rows;
    }
}
internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static JArray ViewerToolsFixtureStatus()
    {
        var rows=new JArray();string root=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned","remote");
        foreach(string path in RebirthProductiveEarnedClosedAudit.Paths(root,".header"))
        {
            if(Path.GetFileName(Path.GetDirectoryName(path))!="native-viewer")continue;
            try
            {
                byte[] header=ReadNativeOutputMetadata(path,2048);string owner,creation,world,digest;Guid operation,birth,nonce;int deaths;
                using(var stream=new MemoryStream(header,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
                {
                    if(reader.ReadByte()!=1)continue;owner=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);creation=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512);deaths=reader.ReadInt32();
                    operation=new Guid(reader.ReadBytes(16));birth=new Guid(reader.ReadBytes(16));nonce=new Guid(reader.ReadBytes(16));world=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,44);digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                    reader.ReadInt32();reader.ReadInt32();reader.ReadInt32();if(stream.Position!=stream.Length||world!=RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance?.World))continue;
                }
                string original=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned",birth.ToString("N"),operation.ToString("N")+".rew");
                byte[] hold=File.Exists(original+".viewer-tools-hold")?ReadNativeOutputMetadata(original+".viewer-tools-hold",4096):new byte[0];
                var fields=new UTF8Encoding(false,true).GetString(hold).Split('\n');
                bool admitted=fields.Length==3&&fields[0]==path&&fields[1]==Digest(header)&&fields[2].Length==64&&fields[2].All(Uri.IsHexDigit)&&
                    !File.Exists(original+".viewer-tools-hold.tmp"),released=admitted&&
                    RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".viewer-tools-released",Encoding.ASCII.GetBytes(Digest(hold)));
                rows.Add(new JObject{["operation"]=operation.ToString("N"),["birth"]=birth.ToString("N"),["nonce"]=nonce.ToString("N"),["digest"]=digest,["payer"]=owner,["deaths"]=deaths,
                    ["nativeViewerToolsOnly"]=true,["nativeViewerToolsAdmitted"]=admitted,["nativeViewerToolsReleased"]=released,["nativeViewerToolsHeld"]=admitted&&!released,
                    ["diskOnly"]=!pending.ContainsKey(operation),["metadataOnly"]=true});
            }
            catch{/* No authority is created by projecting saved metadata. */}
        }
        return rows;
    }
}
#endif
