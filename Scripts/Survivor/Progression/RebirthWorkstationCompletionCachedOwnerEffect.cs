using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // A saved original native progression event, independent of physical output.
    // In particular, a station snapshot alone never issues this witness.
    internal sealed class CachedOwnerEffectPublication
    {
        private readonly CachedOwnerEffect source;
        private CachedOwnerEffectPublication(CachedOwnerEffect value){source=value;}
        internal Guid Operation=>source.Frame.Observation.Operation;
        internal World World=>source.Frame.Observation.World;
        internal TileEntityWorkstation Station=>source.Frame.Observation.Station;
        internal EntityPlayerLocal Viewer=>source.Frame.Observation.Viewer;
        internal long NativeVersion=>source.Version;
        internal long MeasureImage()=>source.Record==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Record);
        internal bool Represents(CachedOwnerEffect value)=>ReferenceEquals(source,value);
        internal XElement Image=>new XElement(source.Record);
        internal bool Current()=>source.Current()&&source.SavedCurrent();
        internal static CachedOwnerEffectPublication Issue(CachedOwnerEffect value)=>new CachedOwnerEffectPublication(value);
    }
    internal sealed class CachedOwnerEffect:IDisposable
    {
        internal Frame Frame;internal long Version,Session;internal XElement Full,Owner,Record;
        internal string Root,Name,NativePath,ReceiptPath,OwnerKey,Creation;internal int Deaths;
        internal byte[] Native,Receipt;internal IDisposable Allocation;internal bool Closed,Published;internal int Workers;
        internal readonly List<CachedEffectCapture> Captures=new List<CachedEffectCapture>();
        internal bool Current()
        {
            var o=Frame?.Observation;return !Closed&&o!=null&&!Frame.Closed&&!o.World.IsRemote()&&
                ReferenceEquals(GameManager.Instance?.World,o.World)&&o.StartingOwnerEntityId==o.Viewer.entityId&&
                o.FullBoundary&&!o.Continued&&!o.Accepted&&Frame.ConfirmedOriginal==null&&o.RanUpdate&&
                Frame.SavedFullBoundaryVersion==Version&&o.NativeVersionAfter==Version&&o.Current()&&o.RecipeMatches()&&
                RebirthRecipeOutputIntegration.IsCurrentOwningNativeVersion(o.Card,Version)&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==OwnerKey&&identity.CanonicalId==Name&&
                o.Viewer.Died==Deaths&&o.ViewerCreation==Creation&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(o.Window,out var session)&&session==Session&&
                XNode.DeepEquals(Full,o.FullBoundaryEvidence)&&
                XNode.DeepEquals(Owner,RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(o.Viewer,new[]{o.Recipe.GetName()}));
        }
        internal bool SavedCurrent()=>Published&&Native!=null&&Receipt!=null&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(NativePath,Native)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(ReceiptPath,Receipt);
        public void Dispose(){Closed=true;if(Workers!=0)return;foreach(var capture in Captures){capture.Native=null;capture.Source=null;}Captures.Clear();Native=Receipt=null;Full=Owner=Record=null;Allocation?.Dispose();Allocation=null;Frame=null;}
    }
    internal sealed class CachedEffectCapture{internal CachedOwnerEffect Source;internal byte[] Native;}
    private sealed class CachedEffectSave{internal CachedOwnerEffect Source;internal byte[] Native;internal string Path;}
    private static readonly Dictionary<Frame,CachedOwnerEffect> cachedOwnerEffects=new Dictionary<Frame,CachedOwnerEffect>();
    private static readonly ConditionalWeakTable<PlayerDataFile,CachedEffectCapture> cachedEffectCaptures=new ConditionalWeakTable<PlayerDataFile,CachedEffectCapture>();
    private sealed class CachedEffectRetirement{internal long Version;}
    private static readonly ConditionalWeakTable<Frame,CachedEffectRetirement> copiedCachedEffects=new ConditionalWeakTable<Frame,CachedEffectRetirement>();
    private static readonly object cachedEffectGate=new object();
    internal static bool TryGetSavedCachedOwnerEffect(Frame frame,out CachedOwnerEffectPublication proof)
    {
        proof=null;lock(cachedEffectGate)if(cachedOwnerEffects.TryGetValue(frame,out var source)&&source.Current()&&source.SavedCurrent())
        {proof=CachedOwnerEffectPublication.Issue(source);return true;}return false;
    }
    internal static void RetireCopiedCachedOwnerEffect(Frame frame,CachedOwnerEffectPublication proof)
    {
        lock(cachedEffectGate)if(frame!=null&&proof!=null&&cachedOwnerEffects.TryGetValue(frame,out var source)&&proof.Represents(source)&&proof.Current())
        {copiedCachedEffects.Remove(frame);copiedCachedEffects.Add(frame,new CachedEffectRetirement{Version=source.Version});cachedOwnerEffects.Remove(frame);source.Dispose();}
    }
    private static void PumpCachedOwnerEffects(World world)
    {
        lock(cachedEffectGate)
        {
            foreach(var pair in cachedOwnerEffects.Where(pair=>pair.Key.Closed||!ReferenceEquals(pair.Key.Observation.World,world)).ToArray())
            {cachedOwnerEffects.Remove(pair.Key);pair.Value.Dispose();}
            if(world.IsRemote())return;
            foreach(var frame in frames.Values.ToArray())try
            {
                var o=frame.Observation;
                if(frame.Closed||o==null||!o.FullBoundary||!o.RanUpdate||o.Continued||o.Accepted||frame.ConfirmedOriginal!=null||
                    o.StartingOwnerEntityId!=o.Viewer.entityId||frame.SavedFullBoundaryVersion!=o.NativeVersionAfter||!o.Current()||!o.RecipeMatches())continue;
                if(HasPersistedCachedOwnerEffect(frame)||copiedCachedEffects.TryGetValue(frame,out var copied)&&copied.Version==o.NativeVersionAfter)continue;
                if(cachedOwnerEffects.TryGetValue(frame,out var old)&&old.Version!=o.NativeVersionAfter)
                {cachedOwnerEffects.Remove(frame);old.Dispose();}
                if(!cachedOwnerEffects.TryGetValue(frame,out var source))
                {
                    if(cachedOwnerEffects.Count>=4||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                        !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(o.Window,out var session)||
                        !RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(o.Viewer,new[]{o.Recipe.GetName()},out int measured)||measured>8*1024*1024||
                        !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*measured+96L*1024*1024),out var allocation))continue;
                    source=new CachedOwnerEffect{Frame=frame,Version=o.NativeVersionAfter,Session=session,Allocation=allocation,
                        Root=Path.GetFullPath(GameIO.GetPlayerDataDir()),Name=owner.CanonicalId,OwnerKey=owner.StorageKey,Creation=o.ViewerCreation,Deaths=o.Viewer.Died,
                        Full=new XElement(o.FullBoundaryEvidence),Owner=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(o.Viewer,new[]{o.Recipe.GetName()}),
                        ReceiptPath=frame.Path+".cached-owner."+o.NativeVersionAfter,NativePath=frame.Path+".cached-owner."+o.NativeVersionAfter+".native"};
                    if(!source.Current()){source.Dispose();continue;}cachedOwnerEffects.Add(frame,source);
                }
                if(!source.Current())continue;
                if(!source.Published){GameManager.Instance.SaveLocalPlayerData();continue;}
                if(source.Receipt==null)
                {
                    if(source.Native==null||!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(source.NativePath,source.Native))continue;
                    source.Record=new XElement("cachedOwnerEffect",new XAttribute("version",1),new XAttribute("operation",o.Operation.ToString("N")),
                        new XAttribute("world",o.WorldGuid),new XAttribute("owner",source.OwnerKey),new XAttribute("creation",source.Creation),new XAttribute("deaths",source.Deaths),
                        new XAttribute("nativeVersion",source.Version),new XAttribute("receipt",source.ReceiptPath),new XAttribute("nativeFile",source.NativePath),new XAttribute("playerFile",Path.Combine(source.Root,source.Name+"."+PlayerDataFile.EXT)),
                        new XAttribute("nativeLength",source.Native.Length),new XAttribute("nativeDigest",RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(source.Native)),new XElement(source.Full),new XElement(source.Owner));
                    var receipt=new XElement("workstationCompletionIntent",new XAttribute("operation",o.Operation.ToString("N")),new XAttribute("world",o.WorldGuid),new XElement("before",source.Record));
                    if(!RebirthWorkstationCompletionPublication.Write(source.ReceiptPath,receipt))continue;
                    source.Receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.ReceiptPath);
                }
                if(source.SavedCurrent())ObserveCachedCustody(frame);
            }
            catch{/* Unknown native capture/save retains the original cached output; it never permits a repeated reward. */}
        }
    }
    internal static void ObserveCachedOwnerEffectFromPlayer(PlayerDataFile data,EntityPlayer player,bool run)
    {
        if(!run||!ThreadManager.IsMainThread()||data==null)return;
        lock(cachedEffectGate)try
        {
            var source=cachedOwnerEffects.Values.SingleOrDefault(value=>!value.Published&&ReferenceEquals(value.Frame?.Observation.Viewer,player)&&value.Current());
            if(source==null||data.id!=player.entityId||data.deaths!=source.Deaths||data.bDead||
                !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,source.Owner))return;
            if(cachedEffectCaptures.TryGetValue(data,out var prior)&&ReferenceEquals(prior.Source,source))return;
            if(source.Captures.Count>=8)return;
            var image=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);if(image.Length<1||image.Length>16*1024*1024)return;
            if(source.Native!=null&&!source.Native.SequenceEqual(image))return;
            source.Native=image;cachedEffectCaptures.Remove(data);var capture=new CachedEffectCapture{Source=source,Native=image};source.Captures.Add(capture);cachedEffectCaptures.Add(data,capture);
        }catch{}
    }
    internal static void BeforeCachedOwnerEffectSave(PlayerDataFile data,string directory,string name,bool run,out object state)
    {
        state=null;if(!run||data==null)return;lock(cachedEffectGate)try
        {
            if(!cachedEffectCaptures.TryGetValue(data,out var captured)||captured.Source==null||captured.Source.Closed||captured.Source.Published||captured.Source.Workers>=2)return;
            var source=captured.Source;string root=Path.GetFullPath(directory),path=Path.GetFullPath(Path.Combine(root,name+"."+PlayerDataFile.EXT));
            if(root!=source.Root||name!=source.Name||Path.GetDirectoryName(path)!=root||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path)||
                !captured.Native.SequenceEqual(RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data)))return;
            source.Workers++;state=new CachedEffectSave{Source=source,Native=captured.Native,Path=path};
        }catch{}
    }
    internal static void AfterCachedOwnerEffectSave(bool run,object state)
    {
        if(state is not CachedEffectSave saved)return;
        try{bool exact=run&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(saved.Path,saved.Native);
            lock(cachedEffectGate)if(exact&&!saved.Source.Closed&&ReferenceEquals(saved.Source.Native,saved.Native))saved.Source.Published=true;}
        finally{lock(cachedEffectGate){saved.Source.Workers--;if(saved.Source.Closed)saved.Source.Dispose();saved.Native=null;saved.Source=null;}}
    }
    // Historical corroboration only. The cache's actual saved chunk/load
    // lineage remains the issuer; this never adopts the current player file.
    internal static bool MatchesSavedCachedOwnerEffect(XElement evidence)
    {
        try
        {
            if(evidence?.Name!="cachedOwnerEffect"||(int?)evidence.Attribute("version")!=1||
                !Guid.TryParseExact((string)evidence.Attribute("operation"),"N",out var operation)||
                (long?)evidence.Attribute("nativeVersion") is not long version||version<1)return false;
            string receipt=Path.GetFullPath((string)evidence.Attribute("receipt")),native=Path.GetFullPath((string)evidence.Attribute("nativeFile"));
            string expected=Path.GetFullPath(IntentPath("server",operation))+".cached-owner."+version;
            if(receipt!=expected||native!=expected+".native"||
                !XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(receipt)?.Element("before")?.Elements().SingleOrDefault(),evidence)||
                !RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(native)||File.Exists(native+".tmp"))return false;
            int length=(int)evidence.Attribute("nativeLength");if(length<1||length>16*1024*1024||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(3L*length+65536),out var allocation))return false;
            using(allocation)using(var input=new FileStream(native,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length!=length)return false;var bytes=new byte[length];int offset=0;
                while(offset<length){int read=input.Read(bytes,offset,length-offset);if(read<1)return false;offset+=read;}
                return input.ReadByte()==-1&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(native)&&
                    RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(bytes)==(string)evidence.Attribute("nativeDigest");
            }
        }
        catch{return false;}
    }
}
