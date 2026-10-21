using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Platform;

public static partial class RebirthNpcTradeService
{
    internal sealed class HostClosed
    {
        internal readonly HostPrepared Prepared;
        internal readonly string TerminalDigest,Image;
        private bool savedSocialCompletionObserved;
        private HostClosed(HostPrepared prepared)
        {
            Prepared=prepared;TerminalDigest=prepared.Terminal.Digest;
            Image=new XElement("npcTradeClosed",new XAttribute("version",1),
                new XAttribute("operation",prepared.Original.Operation.ToString("N")),new XAttribute("originalDigest",prepared.Original.Digest),
                new XAttribute("preparationDigest",RebirthRemoteResourceDurableJournal.Digest(prepared.Image)),
                new XAttribute("terminalDigest",TerminalDigest)).ToString(SaveOptions.DisableFormatting);
        }
        internal static bool TryClose(HostPrepared prepared,string terminal,NetPackage packet,PlatformUserIdentifierAbs user,out HostClosed closed)
        {
            closed=null;
            if(prepared?.Terminal==null||prepared.Terminal.Digest!=terminal)return false;
            return TryCloseQualified(prepared,()=>prepared.Terminal?.Digest==terminal&&prepared.ObserveAcknowledgement(packet,user),out closed);
        }
        internal static bool TryCloseLocal(OwnerAdmission.LocalPermission permission,out HostClosed closed)
        {
            closed=null;if(permission==null)return false;
            return TryCloseQualified(permission.Grant.Prepared,permission.AcknowledgementCurrent,out closed);
        }
        private static bool TryCloseQualified(HostPrepared prepared,Func<bool> acknowledgement,out HostClosed closed)
        {
            closed=null;
            try
            {
                if(prepared==null||prepared.ClosedRecord!=null||prepared.Terminal==null||
                    !prepared.Terminal.IsRetained()||!acknowledgement()||
                    RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(prepared.Original,prepared.NpcPlan,true)==null)return false;
                var next=new HostClosed(prepared);string path=prepared.PathName+".closed";Safe(path);
                if(!File.Exists(path))
                {
                    byte[] bytes=new UTF8Encoding(false,true).GetBytes(next.Image);
                    using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                }
                if(!next.IsRetained()||!acknowledgement()||
                    RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(prepared.Original,prepared.NpcPlan,true)==null)return false;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(prepared.Original.Operation,out var record)||!ReferenceEquals(record.OwnerPreparation,prepared)||
                        !record.Consumed||record.Resolution!=0)return false;
                    prepared.ClosedRecord=next;record.Resolution=1;
                }
                RebirthNpcTradeColdFence.ObserveClosed(next);closed=next;return true;
            }
            catch{return false;}
        }
        internal bool CanReply(NetPackage packet,PlatformUserIdentifierAbs user)=>Prepared.MetadataCurrent(packet,user)&&IsRetained();
        internal bool CanRetireFromQuote=>IsRetained();
        internal bool TrySaveSocialCompletion()
        {
            try
            {
                if(!ThreadManager.IsMainThread()||!IsRetained()||
                    GameManager.Instance?.World?.IsRemote()!=false||GameManager.Instance.World.worldState?.Guid!=Prepared.Original.World)return false;
                if(!TrySaveClosedSocial(Prepared.Original,Prepared.PathName,TerminalDigest)||!IsRetained())return false;
                if(!HasSavedSocialCompletion)return false;
                savedSocialCompletionObserved=true;
                RebirthNpcSaveQuarantineBundle.ObserveServerTradeCompletion(this);return HasSavedSocialCompletion;
            }
            catch{return false;}
        }
        internal bool HasSavedSocialCompletion=>ThreadManager.IsMainThread()&&IsRetained()&&IsClosedSocialSaved(Prepared.Original,TerminalDigest);
        // Historical correlation only. Set exclusively after this exact closure's
        // current atomic social readback; never substitutes for live permission.
        internal bool HasObservedSavedSocialCompletion=>ThreadManager.IsMainThread()&&savedSocialCompletionObserved&&IsRetained();
        private bool IsRetained()
        {
            try
            {
                if(Prepared.Terminal==null||Prepared.Terminal.Digest!=TerminalDigest||!Prepared.Terminal.IsRetained())return false;
                string path=Prepared.PathName+".closed";Safe(path);
                if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)return false;
                byte[] bytes=new UTF8Encoding(false,true).GetBytes(Image);
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length!=bytes.Length||stream.Length>4096)return false;
                    byte[] actual=new byte[bytes.Length];int offset=0;
                    while(offset<actual.Length){int read=stream.Read(actual,offset,actual.Length-offset);if(read<=0)return false;offset+=read;}
                    if(stream.ReadByte()!=-1||!actual.SequenceEqual(bytes))return false;
                }
                Safe(path);return true;
            }
            catch{return false;}
        }
        private static void Safe(string path)
        {
            for(var directory=new DirectoryInfo(Path.GetDirectoryName(path));directory!=null;directory=directory.Parent)
                if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Trade close redirected");
        }
    }
    internal static bool TrySaveClosedSocial(RebirthNpcTradeOriginal original,string path,string terminal)
    {
        if(original==null||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||
            GameManager.Instance.World.worldState?.Guid!=original.World)return false;
        string directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory)||path!=Path.Combine(Path.GetFullPath(directory),"RebirthData","NpcTradeOriginals",
            Guid.Parse(original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(original.Account),original.Operation.ToString("N")+".xml"))return false;
        var quote=original.CopyQuote();var eventId=Derive(quote.QuoteId,7);
        string identity=original.Operation.ToString("N")+":"+original.Digest+":"+terminal;
        return RebirthNpcSocialPersistenceStore.TrySaveTradeCompletion(eventId,identity,()=>
            RebirthNpcSocialGameplayGateway.Publish(eventId,quote.NpcId,quote.ActorId,"trade",RebirthNpcSocialEventKind.Trade,
                Math.Min(1f,0.25f+quote.Quantity*0.02f),1f));
    }
    internal static bool IsClosedSocialSaved(RebirthNpcTradeOriginal original,string terminal)
        =>original!=null&&ThreadManager.IsMainThread()&&GameManager.Instance?.World?.IsRemote()==false&&
            GameManager.Instance.World.worldState?.Guid==original.World&&
            RebirthNpcSocialPersistenceStore.IsTradeCompletionSaved(Derive(original.CopyQuote().QuoteId,7),
                original.Operation.ToString("N")+":"+original.Digest+":"+terminal);
}
