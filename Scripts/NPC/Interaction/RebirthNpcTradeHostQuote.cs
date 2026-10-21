using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Platform;

public static partial class RebirthNpcTradeService
{
    // Own quote-purpose journal. The existing binary journal is only a durable
    // byte/phase codec; no vehicle/logistics permission is consumed here.
    // Prepared=issued, Uncertain=permission entered, Rejected=positively unused.
    internal sealed class HostQuote
    {
        internal readonly string Image,DirectoryName,Account,Creation,World;
        internal readonly RebirthNpcTradeQuote Quote;
        private HostQuote(RebirthNpcTradeQuote quote,string image,string directory,string account,string creation,string world)
        {Quote=quote;Image=image;DirectoryName=directory;Account=account;Creation=creation;World=world;}
        private static string DirectoryFor(string save,string world,string account)=>Path.Combine(Path.GetFullPath(save),"RebirthData","NpcTradeQuotes",
            Guid.Parse(world).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(account));
        private static void Safe(string directory)
        {for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
        private static RebirthCamperTransferJournal.Record ReadJournal(string directory,Guid operation)
        {
            Safe(directory);
            for(int phase=0;phase<4;phase++)
            {
                string path=Path.Combine(directory,operation.ToString("N")+"."+phase+".rcj");
                if(!File.Exists(path))continue;
                var file=new FileInfo(path);
                if((file.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0||file.Length<1||file.Length>40960)
                    throw new InvalidDataException("Quote journal head unavailable");
            }
            var record=RebirthCamperTransferJournal.ReadExisting(directory,operation);Safe(directory);return record;
        }
        private RebirthCamperTransferJournal.Record Read()
        {return ReadJournal(DirectoryName,Quote.QuoteId);}
        private bool Matches(RebirthCamperTransferJournal.Record record)=>record!=null&&record.Operation==Quote.QuoteId&&record.Vehicle==Guid.Parse(World)&&
            record.OwnerCreation==Creation&&record.Revision==Quote.ExpectedNpcInventoryRevision&&
            record.Before.SequenceEqual(Encoding.UTF8.GetBytes(Image))&&record.After.SequenceEqual(Encoding.UTF8.GetBytes(Image));
        internal bool HasSavedUnusedState
        {get{try{var saved=Read();return Matches(saved)&&saved.State==RebirthCamperTransferJournal.Phase.Rejected;}catch{return false;}}}
        internal bool MatchesSavedUnused(HostUnused unused)
        {
            try
            {
                var original=unused?.Original;
                return ThreadManager.IsMainThread()&&original!=null&&original.Account==Account&&
                    original.Creation==Creation&&original.World==World&&original.Operation==Quote.QuoteId&&
                    Image==new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",original.Account),
                        new XAttribute("creation",original.Creation),new XAttribute("world",original.World),
                        new XElement("quote",Serialize(original.CopyQuote()))).ToString(SaveOptions.DisableFormatting)&&unused.ReadRetained()&&HasSavedUnusedState;
            }
            catch{return false;}
        }
        internal static bool TryIssue(RebirthNpcTradeQuote quote,out HostQuote issued)
        {
            issued=null;
            try
            {
                if(!TradeScope()||quote?.EndpointId==null||!quote.EndpointId.StartsWith("player:",StringComparison.Ordinal)||
                    !int.TryParse(quote.EndpointId.Substring(7),out int player)||
                    GameManager.Instance.World.GetEntity(player) is not EntityPlayer actor||actor.IsDead()||!actor.IsSpawned()||
                    !ReferenceEquals(actor.world,GameManager.Instance.World)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;
                string world=actor.world.worldState.Guid,creation=RebirthCamperCharacterAccess.CurrentCreation(actor),save=GameIO.GetSaveGameDir();
                if(!Guid.TryParse(world,out var worldId)||string.IsNullOrWhiteSpace(creation)||string.IsNullOrWhiteSpace(save)||!Path.IsPathRooted(save))return false;
                string image=new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",owner.CanonicalId),
                    new XAttribute("creation",creation),new XAttribute("world",world),new XElement("quote",Serialize(quote))).ToString(SaveOptions.DisableFormatting);
                if(Encoding.UTF8.GetByteCount(image)>16384)return false;
                var next=new HostQuote(quote,image,DirectoryFor(save,world,owner.CanonicalId),owner.CanonicalId,creation,world);
                Safe(next.DirectoryName);var journal=new RebirthCamperTransferJournal(next.DirectoryName);
                byte[] bytes=Encoding.UTF8.GetBytes(image);var record=journal.Prepare(quote.QuoteId,worldId,creation,quote.ExpectedNpcInventoryRevision,bytes,bytes);
                if(!next.Matches(record)||record.State!=RebirthCamperTransferJournal.Phase.Prepared||!next.Matches(next.Read())||
                    !ReferenceEquals(actor.world,GameManager.Instance.World)||GameIO.GetSaveGameDir()!=save)return false;
                issued=next;return true;
            }
            catch{return false;}
        }
        internal bool TryEnterPermission()
        {
            try
            {
                var record=Read();if(!Matches(record)||record.State!=RebirthCamperTransferJournal.Phase.Prepared)return false;
                var journal=new RebirthCamperTransferJournal(DirectoryName);
                record=journal.Advance(Quote.QuoteId,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
                var saved=Read();return Matches(record)&&Matches(saved)&&saved.State==RebirthCamperTransferJournal.Phase.Uncertain;
            }
            catch{return false;}
        }
        internal bool TryMarkUnused()
        {
            try
            {
                var record=Read();if(!Matches(record))return false;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
                    record=new RebirthCamperTransferJournal(DirectoryName).Advance(Quote.QuoteId,record.State,RebirthCamperTransferJournal.Phase.Rejected);
                var saved=Read();return Matches(record)&&Matches(saved)&&record.State==RebirthCamperTransferJournal.Phase.Rejected&&saved.State==record.State;
            }
            catch{return false;}
        }
        internal static bool TryReadEntered(RebirthNpcTradeOriginal original,out HostQuote issued)
        {
            issued=null;
            try
            {
                if(original==null||!TradeScope())return false;
                var quote=original.CopyQuote();
                string image=new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",original.Account),
                    new XAttribute("creation",original.Creation),new XAttribute("world",original.World),new XElement("quote",Serialize(quote))).ToString(SaveOptions.DisableFormatting);
                var next=new HostQuote(quote,image,DirectoryFor(GameIO.GetSaveGameDir(),original.World,original.Account),original.Account,original.Creation,original.World);
                var record=next.Read();if(!next.Matches(record)||record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
                issued=next;return true;
            }
            catch{return false;}
        }
        internal static bool TryMarkLocalUnused(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,out HostQuote issued)
        {
            issued=null;
            try
            {
                if(source==null||!TradeScope()||!source.Matches(original)||!source.TryStage(out int step,out int phase)||phase!=1||step!=0||
                    original.CopyQuote().ExpiresClockTicks>DateTime.UtcNow.Ticks||!source.PersistCurrent(original))return false;
                string image=new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",original.Account),
                    new XAttribute("creation",original.Creation),new XAttribute("world",original.World),new XElement("quote",Serialize(original.CopyQuote()))).ToString(SaveOptions.DisableFormatting);
                var value=new HostQuote(original.CopyQuote(),image,DirectoryFor(GameIO.GetSaveGameDir(),original.World,original.Account),original.Account,original.Creation,original.World);
                var record=value.Read();
                if(!value.Matches(record)||(record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Rejected)||
                    !value.TryMarkUnused()||!source.Matches(original))return false;
                issued=value;return true;
            }
            catch{return false;}
        }
        internal static bool TryMarkLocalUnused(RebirthNpcTradeOwnerExchange.LocalUnusedBefore source,out HostQuote issued)
        {
            issued=null;
            try
            {
                if(source?.Current()!=true||!TradeScope())return false;
                var original=source.Original;
                if(original.CopyQuote().ExpiresClockTicks>DateTime.UtcNow.Ticks)return false;
                string image=new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",original.Account),
                    new XAttribute("creation",original.Creation),new XAttribute("world",original.World),new XElement("quote",Serialize(original.CopyQuote()))).ToString(SaveOptions.DisableFormatting);
                var value=new HostQuote(original.CopyQuote(),image,DirectoryFor(GameIO.GetSaveGameDir(),original.World,original.Account),original.Account,original.Creation,original.World);
                var record=value.Read();
                if(!value.Matches(record)||(record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Rejected)||
                    !value.TryMarkUnused()||!source.Current())return false;
                issued=value;return true;
            }
            catch{return false;}
        }
        internal static bool TryRestoreExpiredUnused(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,Guid operation)
        {
            try
            {
                lock(Sync)if(Quotes.ContainsKey(operation)||Quotes.Count>=MaxQuotes)return false;
                if(!TradeScope()||packet==null||user==null||!packet.ValidEntityIdForSender(player)||!packet.ValidUserIdForSender(user)||
                    !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||
                    GameManager.Instance.World.GetEntity(player) is not EntityPlayer actor||actor.IsDead()||!actor.IsSpawned()||
                    packet.Sender==null||packet.Sender.entityId!=player||packet.Sender.disconnecting||!packet.Sender.loginDone||!packet.Sender.bAttachedToEntity||
                    packet.Sender.netConnection==null||packet.Sender.netConnection.Length<1||packet.Sender.netConnection[0]==null||packet.Sender.netConnection[0].IsDisconnected()||
                    !RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var owner)||
                    !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player),packet.Sender))return false;
                var actualWorld=actor.world;var peer=packet.Sender;var transport=peer.netConnection[0];
                string world=actualWorld.worldState.Guid,creation=RebirthCamperCharacterAccess.CurrentCreation(actor),save=GameIO.GetSaveGameDir();
                string directory=DirectoryFor(save,world,owner.CanonicalId);Safe(directory);
                var record=ReadJournal(directory,operation);
                if(record==null||record.Before.Length>16384||!record.Before.SequenceEqual(record.After))return false;
                string image=new UTF8Encoding(false,true).GetString(record.Before);
                using var text=new StringReader(image);
                using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384});
                var root=XElement.Load(reader);string serialized=root.Element("quote")?.Value;
                var fields=serialized.Split(';').Select(part=>part.Split(new[]{'='},2)).ToDictionary(part=>part[0],part=>part[1],StringComparer.Ordinal);
                if(!fields.TryGetValue("npc",out var npc)||!fields.TryGetValue("endpoint",out var endpoint)||
                    !RebirthNpcTradeQuoteWire.TryRead(fields,user.ToString(),Decode(npc),Decode(endpoint),out var quote)||quote.QuoteId!=operation||
                    quote.ExpiresClockTicks>DateTime.UtcNow.Ticks||
                    image!=new XElement("npcTradeIssuedQuote",new XAttribute("version",1),new XAttribute("account",owner.CanonicalId),
                        new XAttribute("creation",creation),new XAttribute("world",world),new XElement("quote",Serialize(quote))).ToString(SaveOptions.DisableFormatting))return false;
                var issued=new HostQuote(quote,image,directory,owner.CanonicalId,creation,world);
                if(!issued.Matches(record)||!issued.TryMarkUnused())return false;
                lock(Sync)
                {
                    if(Quotes.ContainsKey(operation)||Quotes.Count>=MaxQuotes||!TradeScope()||
                        !ReferenceEquals(GameManager.Instance.World,actualWorld)||!ReferenceEquals(actualWorld.GetEntity(player),actor)||
                        GameIO.GetSaveGameDir()!=save||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||
                        !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player),peer)||
                        peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||peer.netConnection==null||peer.netConnection.Length<1||
                        !ReferenceEquals(peer.netConnection[0],transport)||transport.IsDisconnected()||
                        !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||
                        !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var currentOwner)||currentOwner.CanonicalId!=owner.CanonicalId)return false;
                    // Reentry permits only HostUnused; its durable rejected
                    // state can never enter HostPrepared permission admission.
                    Quotes.Add(operation,new QuoteRecord{Quote=quote,Issued=issued});QuoteOrder.Add(operation);return true;
                }
            }
            catch{return false;}
        }
    }
}
