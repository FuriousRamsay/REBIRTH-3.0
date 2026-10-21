using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // A saved root is a candidate only. Actual region/factory/Persistency return
    // must bind this separate source before any authenticated reentry is issued.
    internal sealed class RemoteOutputHostSource:IDisposable
    {
        internal World World;internal string Save,Path,Hash,Viewer,Creation,PrefixIntentPath,PrefixSavedPath,InventoryPath,NativePath,NativeHash;
        internal Guid Operation,Nonce,Birth;internal Vector3i Position;internal int Deaths,PrefixCount,Offset,NativeLength;
        internal byte[] Image,PrefixIntent,PrefixSaved,Inventory;internal XElement Original,Before,Request,Saved;
        internal RebirthRecipeOutputIntegration.RemoteOutputHostNativeSource Native;internal IDisposable Memory;
        internal bool Closed,Admitted,FinalCallbackSaved;internal byte[] FinalQueueBody;internal int FinalQueueOffset;
        internal XElement FinalLocalPrefix;internal string FinalLocalIntentPath;internal byte[] FinalLocalIntent;
        internal Dictionary<string,byte[]> FinalDependencies;internal string FinalBeforeNativePath,FinalBeforeNativeHash;internal int FinalBeforeNativeLength;
        internal string FinalActiveBeforeNativePath,FinalActiveBeforeNativeHash;internal int FinalActiveBeforeNativeLength;
        internal XElement RootBefore,FinalContinuationRecord,FinalContinuationSavedRecord;internal string FinalBeforeGeneration,FinalContinuationPath;
        internal byte[] FinalContinuationImage,FinalContinuationSavedImage,FinalPriorBody;
        internal RebirthRecipeOutputIntegration.RemoteOutputHostNativeSource FinalQueueNative;
        internal byte[] DeferredAddBody;internal int DeferredAddOffset;
        internal byte[] DeferredOriginAddBody;internal int DeferredOriginAddOffset;
        internal XElement DeferredMembership;
        internal int? FinalViewerEntity;
        internal bool Current()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            PathEquals(Save,GameIO.GetSaveGameDir())&&RebirthWorkstationCompletionOutbox.CurrentGuid(World)==(string)Original.Attribute("world")&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(PrefixIntentPath,PrefixIntent)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(PrefixSavedPath,PrefixSaved)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(InventoryPath,Inventory)&&RemoteOutputNativeFileCurrent(NativePath,NativeHash,NativeLength)&&
            (!FinalCallbackSaved||FinalLocalIntent!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(FinalLocalIntentPath,FinalLocalIntent))&&
            (!(FinalCallbackSaved||FinalContinuationPath!=null||DeferredAddBody!=null)||FinalDependencies!=null&&FinalDependencies.All(pair=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(pair.Key,pair.Value))&&
                RemoteOutputNativeFileCurrent(FinalBeforeNativePath,FinalBeforeNativeHash,FinalBeforeNativeLength)&&
                RemoteOutputNativeFileCurrent(FinalActiveBeforeNativePath,FinalActiveBeforeNativeHash,FinalActiveBeforeNativeLength));
        internal bool Holds(TileEntityWorkstation station)=>!Closed&&!Admitted&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.GetTileEntity(station.ToWorldPos()),station)&&station.ToWorldPos()==Position&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,station,out var birth)&&birth==Birth.ToString("N");
        public void Dispose(){if(Closed)return;Closed=true;Native?.Dispose();Native=null;FinalQueueNative?.Dispose();FinalQueueNative=null;FinalQueueBody=DeferredAddBody=DeferredOriginAddBody=null;Image=PrefixIntent=PrefixSaved=Inventory=null;Original=Before=Request=Saved=DeferredMembership=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly List<RemoteOutputHostSource> remoteOutputHostSources=new List<RemoteOutputHostSource>();
    private sealed class RemoteOutputHostHold{internal World World;internal Guid Operation,Birth;internal Vector3i Position;}
    private static readonly List<RemoteOutputHostHold> remoteOutputHostHolds=new List<RemoteOutputHostHold>();
    private static bool HoldsRemoteOutputHostOriginal(TileEntityWorkstation station)
        =>remoteOutputHostHolds.Any(value=>ReferenceEquals(value.World,GameManager.Instance?.World)&&value.Position==station.ToWorldPos()&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(value.World,station,out var birth)&&birth==value.Birth.ToString("N")&&
            !nativeHostOriginals.ContainsKey(value.Operation));
    private static XElement RemoteOutputXml(string path)
    {try{return File.Exists(path)&&!File.Exists(path+".tmp")?RebirthWorkstationCompletionPublication.ReadPrimary(path):null;}catch{return null;}}
    private static string RemoteOutputRequestDigest(XElement request)
    {var copy=new XElement(request);copy.Attribute("challenge")?.Remove();return RebirthWorkstationCompletionOriginal.Hash(copy.ToString(SaveOptions.DisableFormatting));}
    private static bool PathEquals(string first,string second)=>string.Equals(System.IO.Path.GetFullPath(first),System.IO.Path.GetFullPath(second),RebirthStationCompletionOriginalScope.PathComparison);
    private static bool RemoteOutputNativeFileCurrent(string path,string hash,int length)
    {
        try
        {
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            if(length<1||length>16*1024*1024||File.Exists(path+".tmp"))return false;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
                return file.Length==length&&BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()==hash&&file.Length==length;
        }
        catch{return false;}
    }
    internal static void DiscoverRemoteOutputHostSources(RegionFileManager manager)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.snapshotUtil==null||manager.regionFileAccess is not RegionFileAccessSectorBased||
            !PathEquals(manager.saveDirectory,GameIO.GetSaveGameRegionDir()))return;
        foreach(var old in remoteOutputHostSources.Where(value=>!ReferenceEquals(value.World,world)).ToArray()){remoteOutputHostSources.Remove(old);old.Dispose();}
        remoteOutputHostHolds.RemoveAll(value=>!ReferenceEquals(value.World,world));
        string root=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative");
        RebirthWorkstationCompletionPublication.RequireSafeDirectory(root);if(!Directory.Exists(root))return;
        foreach(string path in Directory.EnumerateFiles(root,"*.xml",SearchOption.TopDirectoryOnly))
        {
            RemoteOutputHostSource source=null;
            try
            {
                if(!Guid.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(path),"N",out var operation)||
                    remoteOutputHostSources.Any(value=>!value.Closed&&ReferenceEquals(value.World,world)&&value.Operation==operation)||nativeHostOriginals.ContainsKey(operation))continue;
                var original=RemoteOutputXml(path);var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                var native=original?.Element("before")?.Element("remoteNativeOriginal");var before=native?.Element("nativeOwningBefore");
                if(bytes==null||bytes.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||before==null||(string)original.Attribute("operation")!=operation.ToString("N")||
                    (string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                    !Guid.TryParseExact((string)native.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||
                    !Guid.TryParseExact((string)native.Attribute("birth"),"N",out var birth)||birth==Guid.Empty)continue;
                string hash=HistoricalHash(bytes);
                var terminal=RemoteOutputXml(path+".owner-terminal");
                var closed=terminal?.Element("remoteNativeOwnerTerminal");
                bool knownTerminal=closed!=null&&(string)terminal.Attribute("operation")==operation.ToString("N")&&(string)terminal.Attribute("world")==((string)original.Attribute("world"))&&
                    (string)closed.Attribute("nonce")==nonce.ToString("N")&&(string)closed.Attribute("original")==hash&&XNode.DeepEquals(terminal.Element("before"),new XElement("before",new XElement(before)));
                var sameTerminal=RemoteOutputXml(path+".same-owner-terminal");var sameSaved=sameTerminal?.Element("before")?.Element("nativeSameOwnerSaved");
                if((string)sameTerminal?.Attribute("operation")==operation.ToString("N")&&(string)sameTerminal?.Attribute("world")==((string)original.Attribute("world"))&&
                    (int?)sameSaved?.Attribute("version")==1&&(string)sameSaved?.Attribute("nonce")==nonce.ToString("N")&&(string)sameSaved?.Attribute("source")==hash)knownTerminal=true;
                if(knownTerminal&&!Directory.EnumerateFiles(root,System.IO.Path.GetFileName(path)+".viewer.*.after.intent",SearchOption.TopDirectoryOnly).Any())continue;
                if(!knownTerminal&&!remoteOutputHostHolds.Any(value=>ReferenceEquals(value.World,world)&&value.Operation==operation))
                    remoteOutputHostHolds.Add(new RemoteOutputHostHold{World=world,Operation=operation,Birth=birth,
                        Position=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z"))});
                // Prefix counts are a per-application domain (primary +64 extras),
                // not a cumulative journal/history limit.
                foreach(string intentPath in Directory.EnumerateFiles(root,System.IO.Path.GetFileName(path)+".viewer.*.prefix-*.intent",SearchOption.TopDirectoryOnly))
                {
                    var intent=RemoteOutputXml(intentPath);var request=intent?.Element("before")?.Element("nativeViewer");
                    var prefix=request?.Element("nativeOutputPrefix");int count=(int?)prefix?.Attribute("count")??0;
                    string generation=(string)request?.Attribute("generation"),stage=(string)request?.Attribute("stage");
                    if(count<1||count>65||stage!="prefix-"+count||!RebirthWorkstationCompletionOriginal.ValidKey(generation)||
                        intentPath!=path+".viewer."+generation+".prefix-"+count+".intent"||(string)request.Attribute("nonce")!=nonce.ToString("N")||
                        (string)request.Attribute("source")!=hash)continue;
                    var activeBefore=request.Element("activeBefore")?.Elements().SingleOrDefault();
                    if(activeBefore==null||RebirthWorkstationCompletionOriginal.Hash(activeBefore.ToString(SaveOptions.DisableFormatting))!=generation)continue;
                    string savedPath=path+".viewer."+generation+".prefix-"+count;
                    var saved=RemoteOutputXml(savedPath);var receipt=saved?.Element("before")?.Element("nativeViewerSaved");
                    string inventoryPath=savedPath+".inventory";var inventory=RemoteOutputXml(inventoryPath);
                    var inventoryReceipt=inventory?.Element("before")?.Element("nativeViewerPrefixInventoryAccepted");
                    string digest=RemoteOutputRequestDigest(request);
                    if(receipt==null||inventoryReceipt==null||(string)saved.Attribute("operation")!=operation.ToString("N")||
                        (string)receipt.Attribute("source")!=hash||(string)receipt.Attribute("nonce")!=nonce.ToString("N")||
                        (string)receipt.Attribute("generation")!=generation||(string)receipt.Attribute("digest")!=digest||
                        (string)inventoryReceipt.Attribute("generation")!=generation||(int?)inventoryReceipt.Attribute("count")!=count||
                        (string)inventoryReceipt.Attribute("request")!=digest||!XNode.DeepEquals(receipt.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame"))||
                        !XNode.DeepEquals(inventoryReceipt.Element("accepted")?.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame")))continue;
                    var candidate=new RemoteOutputHostSource{World=world,Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),Path=path,Operation=operation,Nonce=nonce,Birth=birth,
                        Hash=hash,Image=bytes,Original=original,Before=activeBefore,RootBefore=before,Viewer=(string)native.Attribute("viewer"),Creation=(string)native.Attribute("creation"),Deaths=(int)native.Attribute("deaths"),
                        Position=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z")),Offset=(int)activeBefore.Attribute("nativeElapsedOffset"),
                        PrefixCount=count,Request=request,Saved=saved,PrefixIntentPath=intentPath,PrefixSavedPath=savedPath,InventoryPath=inventoryPath,
                        PrefixIntent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(intentPath),PrefixSaved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(savedPath),
                        Inventory=RebirthWorkstationCompletionPublication.CopyPublishedBytes(inventoryPath),NativePath=(string)receipt.Attribute("retainedNative"),
                        NativeHash=(string)receipt.Attribute("nativeDigest"),NativeLength=(int)receipt.Attribute("nativeLength")};
                    if(candidate.PrefixIntent==null||candidate.PrefixSaved==null||candidate.Inventory==null||
                        !BindRemoteOutputPrefixContinuation(candidate)||!candidate.Current()){candidate.Dispose();continue;}
                    if(source!=null&&source.PrefixCount>=count){candidate.Dispose();continue;}
                    source?.Dispose();source=candidate;
                }
                var final=DiscoverRemoteOutputFinalHostSource(world,path,operation,nonce,birth,hash,bytes,original,before);
                if(final!=null){source?.Dispose();source=final;source.Admitted=knownTerminal;}
                else if(knownTerminal){source?.Dispose();source=null;}
                if(source==null)continue;
                var body=Convert.FromBase64String(source.Before.Element("nativeBefore").Value);
                if(source.FinalCallbackSaved||source.FinalContinuationPath!=null)source.FinalPriorBody=body;
                long retained=checked(16L*(source.Image.Length+source.PrefixIntent.Length+source.PrefixSaved.Length+source.Inventory.Length+body.Length+(source.FinalLocalIntent?.Length??0)+(source.FinalQueueBody?.Length??0)+
                    (source.FinalDependencies?.Values.Sum(value=>(long)value.Length)??0)+(source.DeferredAddBody?.Length??0)+(source.DeferredOriginAddBody?.Length??0))+65536);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(retained,out source.Memory))continue;
                source.Native=RebirthRecipeOutputIntegration.RemoteOutputHostNativeSource.Register(source,source.DeferredAddBody??body,
                    source.DeferredAddBody==null?source.Offset:source.DeferredAddOffset);
                if(source.FinalCallbackSaved)source.FinalQueueNative=RebirthRecipeOutputIntegration.RemoteOutputHostNativeSource.Register(source,source.FinalQueueBody,source.FinalQueueOffset);
                if(RemoteOutputFinalQueueSaved(source)){source.Admitted=true;remoteOutputHostHolds.RemoveAll(value=>ReferenceEquals(value.World,world)&&value.Operation==source.Operation);}
                if(source.Native==null)continue;remoteOutputHostSources.Add(source);source=null;
            }
            catch{/* Parsed originals do not create native authority. */}
            finally{source?.Dispose();}
        }
    }
    internal static Newtonsoft.Json.Linq.JArray CaptureRemoteOutputHostAudit(EntityPlayer actor)
    {
        var rows=new Newtonsoft.Json.Linq.JArray();
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actor==null||!ReferenceEquals(actor.world,GameManager.Instance?.World)||actor.world.IsRemote())return rows;
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId);
        if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner))return rows;
        foreach(var source in remoteOutputHostSources.Where(value=>ReferenceEquals(value.World,actor.world)&&value.Viewer==owner.StorageKey&&
            value.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&value.Deaths==actor.Died).Take(5))
        {
            nativeHostOriginals.TryGetValue(source.Operation,out var original);
            rows.Add(new Newtonsoft.Json.Linq.JObject{["nativeOperation"]=source.Operation.ToString("N"),["nonce"]=source.Nonce.ToString("N"),["source"]=source.Hash,
                ["birth"]=source.Birth.ToString("N"),["acceptedPrefix"]=source.PrefixCount,["current"]=source.Current(),["nativeLoadedCurrent"]=source.Native?.Current()==true,
                ["nativeIdentityCurrent"]=source.Native?.IdentityCurrent()==true,["admitted"]=source.Admitted,["currentPeer"]=original?.IsRemoteOutputRestart(source)==true&&original.CurrentViewer(),
                ["finalCallbackSaved"]=source.FinalCallbackSaved,["queueTerminal"]=source.FinalCallbackSaved&&(source.FinalQueueNative?.Current()==true||RemoteOutputFinalQueueSaved(source)),
                ["nativeTerminalLoadedCurrent"]=source.FinalQueueNative?.Current()==true,
                ["queueOnly"]=source.FinalCallbackSaved&&source.Native?.Current()==true});
        }
#endif
        return rows;
    }
}

