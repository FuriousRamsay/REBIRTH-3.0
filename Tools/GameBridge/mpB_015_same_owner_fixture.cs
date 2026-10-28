// Deferred extracted-production-method fixture. Native/network/save adapters
// below are explicit doubles; this is not native gameplay qualification.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
class PlayerDataFile {internal byte[] Wire;internal int id=7,deaths=2;internal bool FrameMatches=true;}
class EntityPlayer {internal int entityId=7;}
class ClientInfo {internal PlayerDataFile latestPlayerData;}
class NetPackagePlayerData {internal ClientInfo Sender;internal PlayerDataFile playerDataFile;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthWorkstationCompletionPublication
{
    internal static readonly Dictionary<string,byte[]> Files=new Dictionary<string,byte[]>();
    internal static bool MatchesPublishedBytes(string path,byte[] image)=>path!=null&&image!=null&&Files.TryGetValue(path,out var found)&&found.SequenceEqual(image);
}
static class RebirthProductiveEarnedUiOwnerFile {internal static bool MatchesDeferredOwnerFrame(PlayerDataFile data,XElement frame)=>data.FrameMatches;}
static class RebirthWorkstationCompletionPipeline
{
    internal class NativeHostOriginal
    {
        internal Guid Operation=Guid.NewGuid();internal bool Live=true,Saved=true,Admission=true;
        internal ClientInfo Sender;internal EntityPlayer Viewer=new EntityPlayer();internal int ViewerDeaths=2;
        internal byte[] NativeBefore={1};internal int BeforeOffset;
        // Final queue-prefix authority is qualified by its own complete cohort.
        internal bool HasRemoteOutputFinalQueuePrefix;internal byte[] RemoteOutputFinalQueuePrior;internal int RemoteOutputFinalQueuePriorOffset;
        internal bool CurrentViewer()=>Live;internal bool BeforeSaved=>Saved;internal bool AdmissionCurrent()=>Admission;
    }
    internal class NativeViewerEffectAdmission {internal NativeHostOriginal Source;internal int Phase;}
}
static class RebirthProductiveEarnedUiDeferredTransport
{
    internal class HostContribution {}
    internal class SameOwnerStationPublication
    {internal RebirthWorkstationCompletionPipeline.NativeHostOriginal Source;internal int Phase;internal XElement Physical;internal byte[] Body={2};internal int Offset;}
    static readonly Dictionary<Guid,SameOwnerStationPublication> sameOwnerNative=new Dictionary<Guid,SameOwnerStationPublication>();
    static bool TryAdmitRemoteNativeEvent(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,byte[] body,int offset,out HostContribution value)
    {value=null;throw new Exception("Same-owner source must never use deferred receipt admission");}
    // PRODUCTION_PHASE_ADMISSION
    internal static void Seed(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,int phase,XElement physical=null,bool foreign=false)
    {sameOwnerNative.Clear();if(phase!=0)sameOwnerNative[source.Operation]=new SameOwnerStationPublication{Source=foreign?new RebirthWorkstationCompletionPipeline.NativeHostOriginal():source,Phase=phase,Physical=physical};}
    internal static bool Admit(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,int phase,XElement physical=null)
    {return TryAdmitRemoteNativeEvent(source,new XElement("sameOwnerEffect",new XAttribute("phase",phase),physical),new byte[]{3},0,out var unused,out var effect);}
    internal static class NativeViewerPds
    {
        internal class ViewerSource
        {
            internal RebirthWorkstationCompletionPipeline.NativeHostOriginal Original;
            internal ClientInfo SourceSender=>Original?.Sender;
            internal EntityPlayer SourceViewer=>Original?.Viewer;internal int SourceViewerDeaths=>Original?.ViewerDeaths??-1;
            internal bool Closed,Live=true,Custody=true;internal SavedViewerFile Before,After,CachedEffect;
            internal bool Current()=>Live;internal bool SavedCustodyCurrent()=>Custody;
        }
        internal class SavedViewerFile
        {
            internal ViewerSource Source;internal bool Accepted,Published,PreviousHeadNull;
            internal WeakReference<PlayerDataFile> PreviousHead,AcceptedHead;
            internal XElement Frame=new XElement("nativeOwnerFrame");
            internal string ExpectedNative,AcceptedWire,ReceiptPath="receipt",NativePath="native";
            internal int ExpectedLength,AcceptedWireLength;internal byte[] Native,Receipt;
            // PRODUCTION_HEAD_MATCH
            // PRODUCTION_SAVED_CUSTODY
        }
        static readonly object gate=new object();
        static readonly Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>();
        // Intermediate output purposes are covered separately; this cohort owns legacy Before/After.
        static IEnumerable<SavedViewerFile> ViewerStages(ViewerSource source)=>new[]{source.Before,source.After,source.CachedEffect};
        static bool IsRetiredOutputPrefixUpload(NetPackagePlayerData packet,int length,string digest)=>false;
        static bool IsRetiredCachedOwnerPeer(NetPackagePlayerData packet)=>false;
        static byte[] WireImage(PlayerDataFile data)=>data.Wire;
        static string NativeDigest(byte[] data)=>Convert.ToBase64String(data);
        // PRODUCTION_UPLOAD_GATE
        internal static byte[] LastPrior;internal static int LastPhase;
        internal static bool TryValidateSameOwner(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement contribution,byte[] prior,int priorOffset,byte[] proposed,int proposedOffset,int phase,out RebirthWorkstationCompletionPipeline.NativeViewerEffectAdmission value)
        {LastPrior=prior;LastPhase=phase;value=new RebirthWorkstationCompletionPipeline.NativeViewerEffectAdmission{Source=source,Phase=phase};return true;}
        internal static SavedViewerFile Setup(ClientInfo peer,PlayerDataFile head,PlayerDataFile packet)
        {
            sources.Clear();peer.latestPlayerData=head;var source=new ViewerSource{Original=new RebirthWorkstationCompletionPipeline.NativeHostOriginal{Sender=peer}};
            var token=new SavedViewerFile{Source=source,ExpectedNative=NativeDigest(packet.Wire),ExpectedLength=packet.Wire.Length,
                PreviousHeadNull=head==null,PreviousHead=head==null?null:new WeakReference<PlayerDataFile>(head)};
            source.Before=token;sources.Add("original",source);return token;
        }
    }
}
class Fixture
{
    static int assertions;
    static void Require(bool condition,string name){assertions++;if(!condition)throw new Exception(name);}
    static void Main()
    {
        var source=new RebirthWorkstationCompletionPipeline.NativeHostOriginal();
        var physical=new XElement("physicalDelivery",new XElement("nativeOwningDelivery",new XAttribute("operation","same")));
        foreach(int phase in new[]{1,2,3,4})
        {
            RebirthProductiveEarnedUiDeferredTransport.Seed(source,0);
            Require(RebirthProductiveEarnedUiDeferredTransport.Admit(source,phase,physical)==(phase==2||phase==4),"absent phase "+phase);
        }
        foreach(int previous in new[]{2,3,4})foreach(int phase in new[]{1,2,3,4})
        {
            RebirthProductiveEarnedUiDeferredTransport.Seed(source,previous,physical);
            bool expected=previous==4&&(phase==2||phase==4)||previous==2&&phase==3;
            Require(RebirthProductiveEarnedUiDeferredTransport.Admit(source,phase,physical)==expected,"phase "+previous+" -> "+phase);
        }
        RebirthProductiveEarnedUiDeferredTransport.Seed(source,2,physical);
        Require(!RebirthProductiveEarnedUiDeferredTransport.Admit(source,3,new XElement("physicalDelivery",new XElement("changed"))),"phase3 changed physical");
        RebirthProductiveEarnedUiDeferredTransport.Seed(source,4,physical,true);
        Require(!RebirthProductiveEarnedUiDeferredTransport.Admit(source,2,physical),"foreign retained source");
        foreach(string missing in new[]{"live","saved","admission"})
        {
            RebirthProductiveEarnedUiDeferredTransport.Seed(source,0);source.Live=missing!="live";source.Saved=missing!="saved";source.Admission=missing!="admission";
            Require(!RebirthProductiveEarnedUiDeferredTransport.Admit(source,2,physical),"missing "+missing);
        }
        var peer=new ClientInfo();var head=new PlayerDataFile{Wire=new byte[]{1}};var data=new PlayerDataFile{Wire=new byte[]{2}};
        var token=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Setup(peer,head,data);
        var packet=new NetPackagePlayerData{Sender=peer,playerDataFile=data};int nativeRuns=0;
        if(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(packet,true))nativeRuns++;
        Require(nativeRuns==1,"exact original head permits native Process");
        peer.latestPlayerData=new PlayerDataFile{Wire=new byte[]{3}};
        if(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(packet,true))nativeRuns++;
        Require(nativeRuns==1,"stale known native PDS refused BEFORE native Process");
        peer.latestPlayerData=head;token.Published=true;
        Require(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(packet,true),"duplicate accepted native upload");
        token.Published=false;data.FrameMatches=false;
        Require(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(packet,true),"changed full native owner frame");
        data.FrameMatches=true;token.Source.Live=false;
        Require(!RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(packet,true),"lost live native source");
        data.Wire=new byte[]{9};Require(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.CanProcessUpload(packet,true),"unrelated upload preserved");
        token.Source.Live=false;token.Source.Custody=true;token.Accepted=token.Published=true;
        token.ExpectedNative=token.AcceptedWire="exact-actual-native-wire";token.ExpectedLength=token.AcceptedWireLength=4;
        token.Native=new byte[]{4};token.Receipt=new byte[]{5};
        RebirthWorkstationCompletionPublication.Files["native"]=new byte[]{4};RebirthWorkstationCompletionPublication.Files["receipt"]=new byte[]{5};
        Require(token.SavedCustodyCurrent(),"actual immutable saved proof survives live unlock");
        RebirthWorkstationCompletionPublication.Files["native"]=new byte[]{6};Require(!token.SavedCustodyCurrent(),"changed full retained native file refuses");
        RebirthWorkstationCompletionPublication.Files["native"]=new byte[]{4};token.Source.Custody=false;Require(!token.SavedCustodyCurrent(),"lost original station custody refuses");
        Console.WriteLine("PASS "+assertions+" actual-source assertions; native/save/validation adapters are doubles; no native gameplay qualification");
    }
}
