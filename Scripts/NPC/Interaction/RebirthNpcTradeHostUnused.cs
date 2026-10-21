using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Platform;

public static partial class RebirthNpcTradeService
{
    // Positive refusal of an actual retained, never-consumed quotation. A
    // missing preparation/grant file is never used as evidence of no use.
    internal sealed class HostUnused
    {
        internal readonly RebirthNpcTradeOriginal Original;
        internal readonly string Image,PathName,Digest;
        private HostUnused(RebirthNpcTradeOriginal original,string reason)
        {
            Original=original;
            PathName=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","NpcTradeOriginals",
                Guid.Parse(original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(original.Account),original.Operation.ToString("N")+".unused");
            Image=new XElement("npcTradeUnused",new XAttribute("version",1),new XAttribute("reason",reason),
                new XAttribute("originalDigest",original.Digest),new XElement("original",original.Image)).ToString(SaveOptions.DisableFormatting);
            Digest=RebirthRemoteResourceDurableJournal.Digest(Image);
        }
        internal static bool TryRetainLocal(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,out HostUnused unused)
        {
            unused=null;
            try
            {
                if(source==null||!source.Matches(original)||!source.TryStage(out int step,out int phase)||phase!=1||step!=0)return false;
                lock(Sync)
                    if(Quotes.TryGetValue(original.Operation,out var current)&&(current.OwnerPreparation!=null||!original.MatchesQuote(current.Quote)||
                        current.OwnerUnused!=null&&current.OwnerUnused.Original.Image!=original.Image))return false;
                string prefix=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),
                    RebirthRemoteResourceDurableJournal.Digest(original.Account),original.Operation.ToString("N")+".xml");
                // Absence is only an additional conflict fence. The positive
                // issued journal's never-entered -> rejected transition is the
                // sole no-permission proof; uncertainty cannot pass it.
                if(new[]{"",".grant",".terminal",".closed"}.Any(suffix=>File.Exists(prefix+suffix)||File.Exists(prefix+suffix+".tmp"))||
                    !HostQuote.TryMarkLocalUnused(source,original,out _))return false;
                var value=new HostUnused(original,"expired");
                if(!value.Retain()||!value.ReadRetained()||!source.Matches(original))return false;
                lock(Sync)
                    if(Quotes.TryGetValue(original.Operation,out var record))
                    {if(record.OwnerPreparation!=null||!original.MatchesQuote(record.Quote))return false;record.OwnerUnused=value;record.Consumed=true;record.Resolution=2;}
                RebirthNpcSaveQuarantineBundle.ObserveServerUnusedTrade(value);unused=value;return true;
            }
            catch{return false;}
        }
        internal static bool TryRetainLocalWarm(RebirthNpcTradeOwnerExchange.LocalUnusedBefore source,out HostUnused unused)
        {
            unused=null;
            try
            {
                if(source?.Current()!=true)return false;
                var original=source.Original;
                lock(Sync)
                    if(!Quotes.TryGetValue(original.Operation,out var quote)||quote.OwnerPreparation!=null||!original.MatchesQuote(quote.Quote)||
                        quote.OwnerUnused!=null&&quote.OwnerUnused.Original.Image!=original.Image)return false;
                string prefix=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),
                    RebirthRemoteResourceDurableJournal.Digest(original.Account),original.Operation.ToString("N")+".xml");
                if(new[]{"",".grant",".terminal",".closed"}.Any(suffix=>File.Exists(prefix+suffix)||File.Exists(prefix+suffix+".tmp"))||
                    !HostQuote.TryMarkLocalUnused(source,out _))return false;
                var value=new HostUnused(original,"expired");
                if(!value.Retain()||!value.ReadRetained()||!source.Current())return false;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(original.Operation,out var retainedQuote)||retainedQuote.OwnerPreparation!=null||!original.MatchesQuote(retainedQuote.Quote))return false;
                    retainedQuote.OwnerUnused=value;retainedQuote.Consumed=true;retainedQuote.Resolution=2;
                }
                RebirthNpcSaveQuarantineBundle.ObserveServerUnusedTrade(value);unused=value;return true;
            }
            catch{return false;}
        }
        internal static bool TryRetain(NetPackage packet,PlatformUserIdentifierAbs user,uint connection,int player,Guid operation,string digest,out HostUnused unused)
        {
            unused=null;
            try
            {
                if(!OwnerAdmission.TryDescribeSavedBefore(packet,user,connection,RebirthNpcNetworkEpoch.GetServerEpoch(),player,operation,digest,out var original))return false;
                HostUnused next;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(operation,out var quote)||quote.OwnerPreparation!=null||!original.MatchesQuote(quote.Quote))return false;
                    next=quote.OwnerUnused;
                    if(next==null)
                    {
                        if(quote.Consumed||quote.Resolution!=0||Quotes.Values.Count(value=>value.OwnerUnused!=null)>=4)return false;
                        string reason=null;
                        if(quote.Quote.ExpiresClockTicks<=DateTime.UtcNow.Ticks)reason="expired";
                        else
                        {
                            var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(quote.Quote.NpcId);
                            if(snapshot!=null&&snapshot.NpcId==quote.Quote.NpcId&&snapshot.Revision!=quote.Quote.ExpectedNpcInventoryRevision)reason="stockChanged";
                        }
                        if(reason==null)return false;
                        if(quote.Issued==null||!quote.Issued.TryMarkUnused())return false;
                        next=new HostUnused(original,reason);
                        // Reserve the refusal before disk publication. A failed
                        // write cannot race back into HostPrepared.TryRetain.
                        quote.OwnerUnused=next;quote.Consumed=true;
                    }
                    if(next.Original.Digest!=digest)return false;
                }
                if(!next.Retain())return false;
                // Reobserve the actual accepted AND independently saved Before
                // after persistence, before returning a response-scoped release.
                if(!OwnerAdmission.TryDescribeSavedBefore(packet,user,connection,RebirthNpcNetworkEpoch.GetServerEpoch(),player,operation,digest,out var current)||
                    current.Image!=next.Original.Image||!next.ReadRetained())return false;
                lock(Sync)
                {
                    if(!Quotes.TryGetValue(operation,out var retained)||!ReferenceEquals(retained.OwnerUnused,next)||retained.OwnerPreparation!=null)return false;
                    retained.Resolution=2;
                }
                RebirthNpcSaveQuarantineBundle.ObserveServerUnusedTrade(next);unused=next;return true;
            }
            catch{return false;}
        }
        private static void Safe(string directory)
        {for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
        private bool Retain()
        {
            string directory=Path.GetDirectoryName(PathName);Safe(directory);Directory.CreateDirectory(directory);Safe(directory);
            if(!File.Exists(PathName))
            {
                byte[] bytes=new UTF8Encoding(false,true).GetBytes(Image);if(bytes.Length>32*1024*1024)return false;
                using(var stream=new FileStream(PathName,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            }
            return ReadRetained();
        }
        internal bool ReadRetained()
        {
            try
            {
                Safe(Path.GetDirectoryName(PathName));
                if((File.GetAttributes(PathName)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)return false;
                byte[] expected=new UTF8Encoding(false,true).GetBytes(Image);
                using(var stream=new FileStream(PathName,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length!=expected.Length||expected.Length>32*1024*1024)return false;
                    byte[] buffer=new byte[8192];int offset=0;
                    while(offset<expected.Length){int count=stream.Read(buffer,0,Math.Min(buffer.Length,expected.Length-offset));if(count<=0)return false;for(int i=0;i<count;i++)if(buffer[i]!=expected[offset+i])return false;offset+=count;}
                    if(stream.ReadByte()!=-1)return false;
                }
                Safe(Path.GetDirectoryName(PathName));return true;
            }
            catch{return false;}
        }
    }
}
