using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RebirthNpcTradeService
{
    internal static bool BlocksOwnerPreparedNpc(RebirthNpcStableId npc)
    {
        if(RebirthNpcTradeColdFence.Blocks(npc))return true;
        lock(Sync)
            return Quotes.Values.Any(value=>value.OwnerPreparation!=null&&value.OwnerPreparation.ClosedRecord==null&&value.OwnerPreparation.Original.CopyQuote().NpcId==npc);
    }
    // Retained preparation only. Publication is not a payment permission and
    // does not establish an owner After or an applied NPC transaction.
    internal sealed partial class HostPrepared
    {
        internal readonly RebirthNpcTradeOriginal Original;
        internal readonly RebirthNpcTradeNpcPlan NpcPlan;
        internal readonly string Image,PathName;
        internal HostTerminal Terminal;
        internal HostClosed ClosedRecord;
        private readonly OwnerAdmission admission;
        private HostPrepared(OwnerAdmission admission,string path,string image)
        {this.admission=admission;Original=admission.Original;NpcPlan=admission.NpcPlan;PathName=path;Image=image;}
        internal static bool TryRecoverLocalPublication(OwnerAdmission admission,out HostPrepared prepared)
        {
            prepared=null;
            if(admission==null)return false;
            lock(Sync)
            {
                if(!Quotes.TryGetValue(admission.Original.Operation,out var record)||record.OwnerPreparation==null||
                    !ReferenceEquals(record.OwnerPreparation.admission,admission)||!admission.CurrentReserved(record.OwnerPreparation)||!record.OwnerPreparation.ReadRetained())return false;
                prepared=record.OwnerPreparation;return true;
            }
        }

        internal static bool TryRetain(OwnerAdmission admission,out HostPrepared prepared)
        {
            prepared=null;
            try
            {
                if(admission==null||!admission.Current())return false;
                var original=admission.Original;var npc=original.CopyQuote().NpcId;
                if(admission.NpcPlan.CopySnapshot(false).Reservations.Count!=0||RebirthNpcMetabolismCustody.Blocks(npc)||RemoteResourceTransactions.ActionBlocksNpc(npc)||
                    !RebirthNpcOriginalEffectAdmission.CanRecoverNpcStores(GameManager.Instance.World,new[]{npc},out _))return false;
                string root=Path.GetFullPath(GameIO.GetSaveGameDir());
                string account=RebirthRemoteResourceDurableJournal.Digest(original.Account);
                string directory=Path.Combine(root,"RebirthData","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),account);
                Safe(directory);
                string path=Path.Combine(directory,original.Operation.ToString("N")+".xml");
                string image=new XElement("npcTradePrepared",new XAttribute("version",1),
                    new XAttribute("originalDigest",original.Digest),
                    new XElement("original",original.Image),new XElement("savedNpcBefore",admission.SavedNpcBefore))
                    .ToString(SaveOptions.DisableFormatting);
                byte[] bytes=new UTF8Encoding(false,true).GetBytes(image);
                if(bytes.Length>64*1024*1024)return false;
                var next=new HostPrepared(admission,path,image);
                lock(Sync)
                {
                    if(!admission.Current()||!Quotes.TryGetValue(original.Operation,out var quote)||quote.OwnerPreparation!=null||
                        Quotes.Values.Count(value=>value.OwnerPreparation!=null&&value.OwnerPreparation.ClosedRecord==null)>=4||
                        Quotes.Values.Any(value=>value.OwnerPreparation!=null&&value.OwnerPreparation.ClosedRecord==null&&value.OwnerPreparation.Original.CopyQuote().NpcId==npc))return false;
                    // Retain custody before any write. Failure leaves the exact
                    // original consumed; it must not become a fresh quote.
                    quote.OwnerPreparation=next;quote.Consumed=true;
                    if(quote.Issued==null||!quote.Issued.TryEnterPermission())return false;
                }
                if(!admission.CurrentReserved(next))return false;
                Directory.CreateDirectory(directory);Safe(directory);
                if(Path.GetFullPath(GameIO.GetSaveGameDir())!=root||!admission.CurrentReserved(next))return false;
                // Never replace a partial or foreign preparation.
                using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(!next.IsRetained())return false;
                prepared=next;return true;
            }
            catch{return false;}
        }
        internal bool IsRetained()
            =>admission.CurrentReserved(this)&&ReadRetained();
        internal bool TryObserveAfter(HostGrant grant,NetPackage packet,PlatformUserIdentifierAbs user,out OwnerAdmission.SavedAfter observation)
            =>OwnerAdmission.SavedAfter.TryCapture(admission,grant,packet,user,out observation);
        internal bool ObserveAcknowledgement(NetPackage packet,PlatformUserIdentifierAbs user)=>admission.ObserveAcknowledgement(this,packet,user);
        internal bool MetadataCurrent(NetPackage packet,PlatformUserIdentifierAbs user)=>admission.MetadataCurrent(packet,user);
        internal bool ReadRetained()
        {
            try
            {
                Safe(Path.GetDirectoryName(PathName));
                if((File.GetAttributes(PathName)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)return false;
                using(var stream=new FileStream(PathName,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    byte[] expected=new UTF8Encoding(false,true).GetBytes(Image);
                    if(stream.Length!=expected.Length)return false;
                    byte[] buffer=new byte[8192];int offset=0;
                    while(offset<expected.Length)
                    {
                        int count=stream.Read(buffer,0,Math.Min(buffer.Length,expected.Length-offset));
                        if(count<=0)return false;
                        for(int i=0;i<count;i++)if(buffer[i]!=expected[offset+i])return false;
                        offset+=count;
                    }
                    if(stream.ReadByte()!=-1)return false;
                }
                Safe(Path.GetDirectoryName(PathName));
                return true;
            }
            catch{return false;}
        }
        internal bool TryIssueGrant(out HostGrant grant)
        {
            grant=null;
            try
            {
                if(!IsRetained())return false;
                var next=HostGrant.Create(this);
                if(!File.Exists(next.PathName))
                {
                    if(!admission.CanIssueMissingGrant)return false;
                    byte[] bytes=new UTF8Encoding(false,true).GetBytes(next.Image);
                    Safe(Path.GetDirectoryName(next.PathName));
                    if(!IsRetained())return false;
                    using(var stream=new FileStream(next.PathName,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                }
                if(!IsRetained()||!next.IsRetained())return false;
                grant=next;return true;
            }
            catch{return false;}
        }
        internal sealed class HostGrant
        {
            internal readonly HostPrepared Prepared;
            internal readonly string PathName,Image;
            private HostGrant(HostPrepared prepared)
            {
                Prepared=prepared;PathName=prepared.PathName+".grant";
                Image=new XElement("npcTradeOwnerGrant",new XAttribute("version",1),
                    new XAttribute("operation",prepared.Original.Operation.ToString("N")),
                    new XAttribute("originalDigest",prepared.Original.Digest),
                    new XAttribute("preparationDigest",RebirthRemoteResourceDurableJournal.Digest(prepared.Image)))
                    .ToString(SaveOptions.DisableFormatting);
            }
            internal static HostGrant Create(HostPrepared prepared)=>new HostGrant(prepared);
            internal bool IsRetained()
            {
                try
                {
                    if(!Prepared.ReadRetained())return false;
                    Safe(Path.GetDirectoryName(PathName));
                    if((File.GetAttributes(PathName)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)return false;
                    byte[] expected=new UTF8Encoding(false,true).GetBytes(Image);
                    using(var stream=new FileStream(PathName,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {
                        if(stream.Length!=expected.Length||expected.Length>4096)return false;
                        byte[] bytes=new byte[expected.Length];int offset=0;
                        while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<=0)return false;offset+=read;}
                        return stream.ReadByte()==-1&&bytes.SequenceEqual(expected);
                    }
                }
                catch{return false;}
            }
        }
        private static void Safe(string directory)
        {
            for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)
                if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Trade preparation redirected");
        }
    }
}
