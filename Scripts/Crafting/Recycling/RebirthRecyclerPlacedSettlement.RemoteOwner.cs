using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class RemoteOwnerCheckpoint:IDisposable
    {
        internal readonly RemoteHostOriginal Source;internal readonly int Phase,Completed,WireLength;internal readonly string WireDigest;
        internal long AutomaticSequence;internal string FinancialWireDigest;
        internal readonly WeakReference<PlayerDataFile> Previous;internal readonly bool PreviousNull;
        internal PlayerDataFile Data;internal byte[] Native;internal string NativePath,ReceiptPath,ReceiptDigest;internal long ReceiptLength;
        internal WeakReference<PlayerDataFile> AcceptedHead;internal bool Entered,Returned,Published,Closed;internal volatile bool Saved;internal int Workers;
        private string previousReceipt,previousDigest;private long previousLength;
        private IDisposable memory;
        private RemoteOwnerCheckpoint(RemoteHostOriginal source,int phase,int completed,int length,string digest,IDisposable memory)
        {
            Source=source;Phase=phase;Completed=completed;WireLength=length;WireDigest=digest;this.memory=memory;
            FinancialWireDigest=digest;
            PreviousNull=source.Peer.latestPlayerData==null;if(!PreviousNull)Previous=new WeakReference<PlayerDataFile>(source.Peer.latestPlayerData);
        }
        internal static RemoteOwnerCheckpoint Begin(RemoteHostOriginal source,XElement node)
        {
            if(source?.Current()!=true||source.UnusedRequested||!ReferenceEquals(processingRecyclerRequest?.Sender,source.Peer)||node?.Name!="recyclerOwnerCheckpoint"||
                (string)node.Attribute("operation")!=source.Operation.ToString("N")||(string)node.Attribute("nonce")!=source.Nonce.ToString("N"))return null;
            int phase=(int?)node.Attribute("phase")??-1,count=(int?)node.Attribute("completed")??-1,length=(int?)node.Attribute("wireLength")??-1;
            string digest=(string)node.Attribute("wireDigest");
            if(!ValidOwnerCheckpointStage(phase,count,source.Slots.Length)||length<1||length>16*1024*1024||digest==null||Convert.FromBase64String(digest).Length!=32)return null;
            var previous=source.OwnerCheckpoint;
            if(previous!=null&&previous.Phase==phase&&previous.Completed==count)
                return previous.FinancialWireDigest==digest?previous:null;
            bool cold=source.ColdNeedsOwner&&previous==null;
            if(cold?(phase!=(source.ColdPhase==0?1:source.ColdPhase)||count!=source.ColdCompleted):
                phase==1?(previous!=null||source.AcceptedStation):!source.AcceptedStation||source.Publication?.SavedAfter()!=true||previous?.CurrentSaved()!=true||
                (phase==2?count!=previous.Completed+1:count!=previous.Completed))return null;
            if(previous!=null&&(previous.AcceptedHead==null||!previous.AcceptedHead.TryGetTarget(out var head)||!ReferenceEquals(head,source.Peer.latestPlayerData)))return null;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return null;
            var token=new RemoteOwnerCheckpoint(source,phase,count,length,digest,memory);
            if(previous!=null){token.previousReceipt=previous.ReceiptPath;token.previousLength=previous.ReceiptLength;token.previousDigest=previous.ReceiptDigest;}
            var oldBefore=source.OwnerBeforeCheckpoint;
            source.OwnerCheckpoint=token;if(phase==1)source.OwnerBeforeCheckpoint=token;
            if(cold)source.ColdNeedsOwner=false;
            if(phase==1&&!ReferenceEquals(oldBefore,previous)&&!ReferenceEquals(oldBefore,token))oldBefore?.Dispose();
            if(!ReferenceEquals(previous,source.OwnerBeforeCheckpoint))previous?.Dispose();return token;
        }
        internal static RemoteOwnerCheckpoint BeginAutomatic(RemoteHostOriginal source,XElement header,int length,string digest)
        {
            var previous=source?.OwnerCheckpoint;long sequence=(long?)header?.Attribute("sequence")??0;
            if(source?.AutomaticOwnerCurrent()!=true||source.UnusedRequested||previous?.CurrentSaved()!=true||sequence<=source.AutomaticOwnerSequence||
                (int?)header.Attribute("phase")!=previous.Phase||(int?)header.Attribute("completed")!=previous.Completed||
                (string)header.Attribute("basis")!=previous.FinancialWireDigest||length<1||length>16*1024*1024||
                previous.AcceptedHead==null||!previous.AcceptedHead.TryGetTarget(out var head)||!ReferenceEquals(head,source.Peer.latestPlayerData)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var memory))return null;
            var token=new RemoteOwnerCheckpoint(source,previous.Phase,previous.Completed,length,digest,memory)
            {AutomaticSequence=sequence,FinancialWireDigest=previous.FinancialWireDigest,previousReceipt=previous.ReceiptPath,previousLength=previous.ReceiptLength,previousDigest=previous.ReceiptDigest};
            source.OwnerCheckpoint=token;if(token.Phase==1)source.OwnerBeforeCheckpoint=token;
            previous.Dispose();return token;
        }
        internal static RemoteOwnerCheckpoint ReadHistoricalColdBefore(RemoteHostOriginal source)
        {
            if(source?.ColdStation?.Current()!=true||!source.AcceptedStation)return null;
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".remote-owner.1.0.xml");
            long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var memory))return null;
            try
            {
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=record?.Element("recyclerRemoteOwnerSaved");
                if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=source.Operation.ToString("N")||(string)record.Attribute("world")!=source.WorldKey||
                    !XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),XElement.Parse(Encoding.UTF8.GetString(source.Image)))||
                    (int?)saved?.Attribute("phase")!=1||(int?)saved.Attribute("completed")!=0||(string)saved.Attribute("account")!=source.Account)return null;
                byte[] native=ColdBytes(saved.Element("native"),16*1024*1024);string wire=(string)saved.Attribute("wireDigest");int wireLength=(int?)saved.Attribute("wireLength")??0;
                if(native==null||native.Length<6||native[0]!='t'||native[1]!='t'||native[2]!='p'||native[3]!=0||native[4]!=PlayerDataFile.cFileVersion||
                    wireLength<1||wireLength>16*1024*1024||Convert.FromBase64String(wire).Length!=32)return null;
                var token=new RemoteOwnerCheckpoint(source,1,0,wireLength,wire,memory){Entered=true,Returned=true,Published=true,Saved=true,
                    ReceiptPath=path,ReceiptLength=length,ReceiptDigest=RecyclerFileDigest(path)};
                if(!token.CurrentSaved())return null;
                RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,65536);memory=null;return token;
            }
            catch{return null;}
            finally{memory?.Dispose();}
        }
        internal bool PreviousCurrent(PlayerDataFile data)=>PreviousNull?data==null:Previous!=null&&Previous.TryGetTarget(out var expected)&&ReferenceEquals(expected,data);
        internal bool Matches(PlayerDataFile data)=>data!=null&&!data.bDead&&data.id==Source.Actor.entityId&&data.deaths==Source.Deaths&&
            PhaseBytes(data.buffData?.ToArray(),"rbRecyclerPlaced_"+Source.Operation.ToString("N"),out float phase)&&phase==Phase&&
            PhaseBytes(data.buffData?.ToArray(),"rbRecyclerSlot_"+Source.Operation.ToString("N"),out float count)&&count==Completed&&
            Source.MatchesOwnerCheckpoint(RebirthGearPlayerDataInventory.ReadSlots(data,true),RebirthGearPlayerDataInventory.ReadSlots(data,false),Completed);
        internal bool CurrentSaved()=>!Closed&&Published&&Returned&&Saved&&Source.SavedOwnerOriginCurrent()&&ReceiptLength>0&&
            RebirthWorkstationCompletionPublication.PublishedLength(ReceiptPath)==ReceiptLength&&RecyclerFileDigest(ReceiptPath)==ReceiptDigest;
        internal bool MatchesSavedNative(byte[] native)
        {
            if(native==null||native.Length>16*1024*1024||!TryCopyEvidence(out var record,out var lease))return false;
            using(lease)
            {
                var saved=record?.Element("recyclerRemoteOwnerSaved");
                return (int?)saved?.Attribute("phase")==Phase&&(int?)saved.Attribute("completed")==Completed&&
                    ColdBytes(saved.Element("native"),16*1024*1024).SequenceEqual(native)&&CurrentSaved();
            }
        }
        internal bool Publish()
        {
            if(CurrentSaved())return true;
            if(Closed||Workers!=0||!Returned||!Saved||Native==null||!Source.SavedOwnerOriginCurrent()||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(12L*(Native.Length+Source.Image.Length)+65536)))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Source.Operation.ToString("N")),new XAttribute("world",Source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(Source.Image))),new XElement("recyclerRemoteOwnerSaved",new XAttribute("phase",Phase),new XAttribute("completed",Completed),
                    new XAttribute("account",Source.Account),new XAttribute("path",NativePath),new XAttribute("wireDigest",WireDigest),new XAttribute("wireLength",WireLength),
                    new XAttribute("automaticSequence",AutomaticSequence),new XAttribute("financialWireDigest",FinancialWireDigest),
                    previousReceipt==null?null:new XElement("previousSaved",new XAttribute("path",previousReceipt),new XAttribute("length",previousLength),new XAttribute("digest",previousDigest)),new XElement("native",Convert.ToBase64String(Native))));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),Source.Operation.ToString("N")+(Source.ColdStation==null&&!Source.OwnerRejoined?"":".reentry."+Source.Nonce.ToString("N"))+".remote-owner."+Phase+"."+Completed+(AutomaticSequence>0?".native."+AutomaticSequence:"")+".xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return false;
            ReceiptPath=path;ReceiptLength=RebirthWorkstationCompletionPublication.PublishedLength(path);ReceiptDigest=RecyclerFileDigest(path);
            if(ReceiptLength<1||ReceiptDigest==null)return false;
            Published=true;if(AutomaticSequence>0)Source.AutomaticOwnerSequence=AutomaticSequence;
            Native=null;Data=null;RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,65536);return true;
        }
        internal bool TryCopyEvidence(out XElement image,out IDisposable lease)
        {
            image=null;lease=null;
            if(!CurrentSaved()||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*ReceiptLength+65536),out lease))return false;
            try{image=RebirthWorkstationCompletionPublication.ReadPrimary(ReceiptPath);if(image!=null&&CurrentSaved())return true;}
            catch{}
            image=null;lease.Dispose();lease=null;return false;
        }
        internal XElement CopySavedDescriptor()=>CurrentSaved()?new XElement("actualRemoteOwnerSaved",new XAttribute("operation",Source.Operation.ToString("N")),
            new XAttribute("phase",Phase),new XAttribute("completed",Completed),new XAttribute("path",ReceiptPath),new XAttribute("length",ReceiptLength),new XAttribute("digest",ReceiptDigest)):null;
        public void Dispose(){Closed=true;if(Workers!=0)return;Native=null;Data=null;memory?.Dispose();memory=null;}
    }
    private sealed class RemoteOwnerUpload {internal RemoteOwnerCheckpoint Checkpoint;internal NetPackagePlayerData Packet;}
    private sealed class RemoteOwnerWriting {internal RemoteOwnerCheckpoint Checkpoint;internal PlayerDataFile Data;internal string Path;internal Stream Stream;internal bool Wrote;internal RemoteOwnerWriting Previous;}
    private static readonly Dictionary<Guid,RemoteHostOriginal> remoteRecyclerOriginals=new Dictionary<Guid,RemoteHostOriginal>();
    private static readonly ConditionalWeakTable<PlayerDataFile,RemoteOwnerCheckpoint> remoteRecyclerOwnerData=new ConditionalWeakTable<PlayerDataFile,RemoteOwnerCheckpoint>();
    [ThreadStatic]private static RemoteOwnerUpload readingRecyclerOwner;
    [ThreadStatic]private static RemoteOwnerWriting writingRecyclerOwner;
    private static string RecyclerFileDigest(string path)
    {try{long length=RebirthWorkstationCompletionPublication.PublishedLength(path);if(length<1)return null;string digest;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=SHA256.Create())
        {if(file.Length!=length)return null;digest=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","");}
        return RebirthWorkstationCompletionPublication.PublishedLength(path)==length?digest:null;}catch{return null;}}
    private static bool RecyclerOwnerWireMatches(RemoteOwnerCheckpoint token,PlayerDataFile data)
    {
        if(!token.Matches(data))return false;
        byte[] wire=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(data);
        using(var hash=SHA256.Create())return wire.Length==token.WireLength&&Convert.ToBase64String(hash.ComputeHash(wire))==token.WireDigest;
    }
    internal static bool CanProcessRecyclerOwnerUpload(NetPackagePlayerData packet,World world,bool run)
    {
        if(!run||packet?.Sender==null||world?.IsRemote()!=false)return true;
        var known=remoteRecyclerOriginals.Values.Where(value=>ReferenceEquals(value.World,world)&&ReferenceEquals(value.Peer,packet.Sender)&&value.Actor.entityId==packet.playerDataFile?.id&&value.Deaths==packet.playerDataFile.deaths).ToArray();
        if(known.Length==0)return true;
        if(executingRecyclerOwnerEnvelope==null&&known.Length==1&&known[0].FinancialReleased&&known[0].OwnerCheckpoint is RemoteOwnerCheckpoint final&&final.Phase==3&&final.CurrentSaved()&&
            TryRecyclerReceivedConnection(packet,out var finalConnection)&&ReferenceEquals(finalConnection,known[0].Connection)&&
            known[0].OwnerCurrent()&&packet.ValidEntityIdForSender(packet.playerDataFile.id)&&final.Matches(packet.playerDataFile))return true;
        try{return executingRecyclerOwnerEnvelope!=null&&ReferenceEquals(executingRecyclerOwnerEnvelope.Native,packet)&&executingRecyclerOwnerEnvelope.ReadReturned&&
            known.Length==1&&TryRecyclerReceivedConnection(packet,out var received)&&ReferenceEquals(received,known[0].Connection)&&
            packet.ValidEntityIdForSender(packet.playerDataFile.id)&&known[0].OwnerCurrent()&&
            (executingRecyclerOwnerEnvelope.Automatic||known[0].HasOwnerAdmission())&&
            known[0].OwnerCheckpoint is RemoteOwnerCheckpoint token&&!token.Closed&&!token.Entered&&token.PreviousCurrent(packet.Sender.latestPlayerData)&&RecyclerOwnerWireMatches(token,packet.playerDataFile);}
        catch{return false;}
    }
    internal static void EnterRecyclerOwnerUpload(NetPackagePlayerData packet,World world,bool run,out object state)
    {
        state=null;if(!run||readingRecyclerOwner!=null||packet?.Sender==null||executingRecyclerOwnerEnvelope==null||!CanProcessRecyclerOwnerUpload(packet,world,run))return;
        var source=remoteRecyclerOriginals.Values.SingleOrDefault(value=>ReferenceEquals(value.Peer,packet.Sender)&&ReferenceEquals(value.World,world)&&value.Actor.entityId==packet.playerDataFile?.id);
        var token=source?.OwnerCheckpoint;if(token==null)return;
        token.Entered=true;token.Data=packet.playerDataFile;remoteRecyclerOwnerData.Remove(token.Data);remoteRecyclerOwnerData.Add(token.Data,token);
        readingRecyclerOwner=new RemoteOwnerUpload{Checkpoint=token,Packet=packet};state=readingRecyclerOwner;
    }
    internal static void ObserveRecyclerOwnerUpload(NetPackagePlayerData packet,World world,bool run,object state)
    {
        if(!run||state is not RemoteOwnerUpload read||!ReferenceEquals(readingRecyclerOwner,read)||!ReferenceEquals(read.Packet,packet))return;
        var token=read.Checkpoint;
        if(!token.Source.OwnerCurrent()||!ReferenceEquals(packet.playerDataFile,token.Data)||!ReferenceEquals(packet.Sender.latestPlayerData,token.Data)||
            !ReferenceEquals(token.Source.Actor.QuestJournal,token.Data.questJournal)||!RecyclerOwnerWireMatches(token,token.Data))return;
        token.Returned=true;token.AcceptedHead=new WeakReference<PlayerDataFile>(token.Data);token.Publish();
    }
    internal static void EndRecyclerOwnerUpload(object state)
    {if(state is RemoteOwnerUpload read&&ReferenceEquals(readingRecyclerOwner,read))readingRecyclerOwner=null;}
    private static object EnterRemoteRecyclerOwnerSave(PlayerDataFile data,string directory,string playerId,bool run)
    {
        if(!run||data==null||!remoteRecyclerOwnerData.TryGetValue(data,out var token)||token.Closed||!ReferenceEquals(token.Data,data)||!token.Entered||!token.Matches(data))return null;
        string path=Path.GetFullPath(Path.Combine(directory,playerId+"."+PlayerDataFile.EXT));
        if(playerId!=token.Source.Account||Path.GetFullPath(directory)!=Path.GetFullPath(GameIO.GetPlayerDataDir()))return null;
        foreach(string suffix in new[]{"",".tmp",".bak",".meta"})SafeOwnerPath(path+suffix);
        var image=NativeOwnerImage(data);if(token.Native!=null&&!token.Native.SequenceEqual(image))return null;
        token.Native=image;token.NativePath=path;System.Threading.Interlocked.Increment(ref token.Workers);
        var writing=new RemoteOwnerWriting{Checkpoint=token,Data=data,Path=path,Previous=writingRecyclerOwner};writingRecyclerOwner=writing;return writing;
    }
    private static void ObserveRemoteRecyclerOwnerSave(bool run,RemoteOwnerWriting write)
    {
        var token=write.Checkpoint;
        if(run&&!token.Closed&&write.Wrote&&token.Matches(write.Data)&&MatchesOwnerFile(write.Path,token.Native))
        {token.Saved=true;}
    }
    private static void EndRemoteRecyclerOwnerSave(RemoteOwnerWriting write)
    {
        if(ReferenceEquals(writingRecyclerOwner,write))writingRecyclerOwner=write.Previous;
        var token=write.Checkpoint;write.Stream=null;write.Data=null;write.Checkpoint=null;write.Previous=null;
        System.Threading.Interlocked.Decrement(ref token.Workers);if(token.Closed)token.Dispose();
    }
    private static void PumpRemoteRecyclerOwner(World world)
    {
        foreach(var source in remoteRecyclerOriginals.Values.ToArray())
        {
            if(!ReferenceEquals(source.World,world)){remoteRecyclerOriginals.Remove(source.Operation);source.Dispose();continue;}
            try{source.OwnerCheckpoint?.Publish();}catch{/* Retain actual native upload/save evidence; never resend. */}
        }
    }
}
