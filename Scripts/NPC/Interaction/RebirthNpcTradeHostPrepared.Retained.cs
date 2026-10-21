using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Platform;

public static partial class RebirthNpcTradeService
{
    internal sealed partial class HostPrepared
    {
        // A typed own preparation-file source, not a detached DTO. Only the
        // actual authenticated native request and its bound path can create it.
        internal sealed class RetainedSource
        {
            internal readonly RebirthNpcTradeOriginal Original;
            internal readonly RebirthNpcTradeNpcPlan NpcPlan;
            internal readonly string Image,PathName,SavedNpcBefore;
            private RetainedSource(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,string before,string image,string path)
            {Original=original;NpcPlan=plan;SavedNpcBefore=before;Image=image;PathName=path;}
            internal static bool TryCaptureLocal(RebirthNpcTradeLocalOwnerLoad.Source ownerSource,out RetainedSource source)
            {
                source=null;
                try
                {
                    var player=ownerSource?.Player;
                    if(ownerSource==null||!TradeScope()||!ownerSource.Current(player)||
                        !RebirthNpcTradeOwnerOriginalStore.TryReadRetained(player,ownerSource.Operation,out var owned)||!ownerSource.Matches(owned))return false;
                    string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","NpcTradeOriginals",Guid.Parse(owned.World).ToString("N"),
                        RebirthRemoteResourceDurableJournal.Digest(owned.Account),owned.Operation.ToString("N")+".xml");
                    string image=ReadSource(path);
                    using var text=new StringReader(image);
                    using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64*1024*1024});
                    var root=XElement.Load(reader);var original=RebirthNpcTradeOriginal.Read(root.Element("original")?.Value);string before=root.Element("savedNpcBefore")?.Value;
                    if(original.Image!=owned.Image||!ownerSource.Matches(original)||
                        image!=new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest",original.Digest),
                            new XElement("original",original.Image),new XElement("savedNpcBefore",before)).ToString(SaveOptions.DisableFormatting)||
                        !RebirthNpcInventoryPersistenceStore.TryReadTradePlanFromSavedBefore(original,before,out var plan))return false;
                    var value=new RetainedSource(original,plan,before,image,path);
                    if(!value.IsRetained()||!ownerSource.Matches(original))return false;source=value;return true;
                }
                catch{return false;}
            }
            internal static bool TryCapture(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,Guid operation,string digest,out RetainedSource source)
            {
                source=null;
                try
                {
                    var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=packet?.Sender;
                    if(!TradeScope()||world?.IsRemote()!=false||manager?.IsServer!=true||peer==null||user==null||
                        !packet.ValidEntityIdForSender(player)||!packet.ValidUserIdForSender(user)||!ReferenceEquals(manager.Clients.ForEntityId(player),peer)||
                        peer.entityId!=player||peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||
                        !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||
                        world.GetEntity(player) is not EntityPlayer actor||actor is EntityPlayerLocal||!ReferenceEquals(actor.world,world)||
                        actor.IsDead()||!actor.IsSpawned()||!Guid.TryParse(world.worldState?.Guid,out var worldId))return false;
                    string selected=GameIO.GetSaveGameDir(),creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
                    if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected))return false;
                    string path=Path.Combine(Path.GetFullPath(selected),"RebirthData","NpcTradeOriginals",worldId.ToString("N"),
                        RebirthRemoteResourceDurableJournal.Digest(owner.CanonicalId),operation.ToString("N")+".xml");
                    string image=ReadSource(path);
                    using var text=new StringReader(image);
                    using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64*1024*1024});
                    var root=XElement.Load(reader);var original=RebirthNpcTradeOriginal.Read(root.Element("original")?.Value);
                    string before=root.Element("savedNpcBefore")?.Value;
                    if(original.Operation!=operation||original.Digest!=digest||original.Account!=owner.CanonicalId||original.Creation!=creation||
                        original.World!=world.worldState.Guid||original.Deaths!=actor.Died||original.CopyQuote().ActorId!=user.ToString()||
                        image!=new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest",digest),
                            new XElement("original",original.Image),new XElement("savedNpcBefore",before)).ToString(SaveOptions.DisableFormatting)||
                        !RebirthNpcInventoryPersistenceStore.TryReadTradePlanFromSavedBefore(original,before,out var plan))return false;
                    var next=new RetainedSource(original,plan,before,image,path);
                    if(!ReferenceEquals(world,GameManager.Instance.World)||GameIO.GetSaveGameDir()!=selected||!ReferenceEquals(world.GetEntity(player),actor)||
                        RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||!next.IsRetained())return false;
                    source=next;return true;
                }
                catch{return false;}
            }
            internal bool IsRetained(){try{return ReadSource(PathName)==Image;}catch{return false;}}
            internal bool TryClosedHistory(out string terminal)
            {
                terminal=null;
                try
                {
                    if(!IsRetained()||!RebirthNpcTradeClosedHistory.IsClosedPreparation(PathName,Original,Image))return false;
                    string image=ReadSource(PathName+".terminal");terminal=RebirthRemoteResourceDurableJournal.Digest(image);
                    return IsRetained()&&RebirthNpcTradeClosedHistory.IsClosedPreparation(PathName,Original,Image);
                }
                catch{return false;}
            }
            private static string ReadSource(string path)
            {
                Safe(Path.GetDirectoryName(path));
                if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new InvalidDataException();
                string image;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    long length=stream.Length;if(length<1||length>64*1024*1024)throw new InvalidDataException();
                    using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,8192,true);image=reader.ReadToEnd();
                    if(stream.Length!=length||stream.Position!=length||Encoding.UTF8.GetByteCount(image)!=length)throw new InvalidDataException();
                }
                Safe(Path.GetDirectoryName(path));return image;
            }
        }
        internal static bool TryReattach(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,Guid operation,string digest)
        {
            try
            {
                lock(Sync)
                {
                    if(Quotes.TryGetValue(operation,out var record))
                    {
                        if(record.OwnerPreparation==null||record.OwnerUnused!=null||record.Resolution!=0)return false;
                        // Metadata identity alone cannot keep the original UI
                        // admission alive after window close/expiry or owner
                        // phase advance. Reattach that same saved original
                        // instead of stranding it behind its expired session.
                        if(record.OwnerPreparation.MetadataCurrent(packet,user)&&record.OwnerPreparation.IsRetained())return true;
                    }
                }
                if(!RetainedSource.TryCapture(packet,user,player,connection,operation,digest,out var source)||
                    File.Exists(source.PathName+".closed")||!OwnerAdmission.TryCaptureRetained(packet,user,player,connection,source,out var admission))return false;
                var next=new HostPrepared(admission,source.PathName,source.Image);
                if(!next.ReadRetained())return false;
                // Full native NPC state must remain one exact original leg.
                var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(source.Original.CopyQuote().NpcId);
                if(!source.NpcPlan.Matches(snapshot,false)&&!source.NpcPlan.Matches(snapshot,true))return false;
                if(File.Exists(source.PathName+".grant")&&!HostGrant.Create(next).IsRetained())return false;
                if(File.Exists(source.PathName+".terminal"))
                {next.Terminal=HostTerminal.ReadExisting(next);if(next.Terminal==null)return false;}
                if(!source.IsRetained()||!admission.MetadataCurrent(packet,user)||!admission.BindRetained(next))return false;
                return true;
            }
            catch{return false;}
        }
        internal static bool TryReattachLocal(RebirthNpcTradeLocalOwnerLoad.Source ownerSource,out OwnerAdmission admission,out HostPrepared prepared)
        {
            admission=null;prepared=null;
            try
            {
                if(!RetainedSource.TryCaptureLocal(ownerSource,out var source)||File.Exists(source.PathName+".closed")||
                    !OwnerAdmission.TryCaptureLocalRetained(ownerSource,source,out var candidate))return false;
                var value=new HostPrepared(candidate,source.PathName,source.Image);
                if(!value.ReadRetained())return false;
                var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(source.Original.CopyQuote().NpcId);
                if(!source.NpcPlan.Matches(snapshot,false)&&!source.NpcPlan.Matches(snapshot,true))return false;
                if(File.Exists(source.PathName+".grant")&&!HostGrant.Create(value).IsRetained())return false;
                if(File.Exists(source.PathName+".terminal")){value.Terminal=HostTerminal.ReadExisting(value);if(value.Terminal==null)return false;}
                if(!source.IsRetained()||!ownerSource.Matches(source.Original)||!candidate.BindRetained(value))return false;
                admission=candidate;prepared=value;return true;
            }
            catch{return false;}
        }
    }
}
