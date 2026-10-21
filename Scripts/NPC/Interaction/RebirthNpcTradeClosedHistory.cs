using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Platform;

// Positive closed history is metadata only. This type cannot issue an owner
// permission, reserve stock or apply an inventory effect. Its only completion
// effect is the original's idempotent social projection in the atomic store.
internal sealed class RebirthNpcTradeClosedHistory
{
    internal readonly Guid Operation;
    internal readonly string OriginalDigest,TerminalDigest;
    private readonly RebirthNpcTradeOriginal original;
    private readonly string path;
    private RebirthNpcTradeClosedHistory(RebirthNpcTradeOriginal original,string path,string terminal)
    {this.original=original;this.path=path;Operation=original.Operation;OriginalDigest=original.Digest;TerminalDigest=terminal;}
    internal bool TrySaveSocialCompletion()
    {
        try
        {
            string preparation=Read(path,64*1024*1024);
            if(!IsClosedPreparation(path,original,preparation)||
                RebirthRemoteResourceDurableJournal.Digest(Read(path+".terminal",64*1024*1024))!=TerminalDigest||
                !RebirthNpcTradeService.TrySaveClosedSocial(original,path,TerminalDigest))return false;
            RebirthNpcSaveQuarantineBundle.ObserveServerTradeCompletion(this);return HasSavedSocialCompletion;
        }
        catch{return false;}
    }
    internal bool MatchesOriginal(RebirthNpcTradeOriginal candidate)=>candidate!=null&&candidate.Image==original.Image;
    internal bool HasSavedSocialCompletion
    {
        get
        {
            try{return IsClosedPreparation(path,original,Read(path,64*1024*1024))&&
                RebirthRemoteResourceDurableJournal.Digest(Read(path+".terminal",64*1024*1024))==TerminalDigest&&
                RebirthNpcTradeService.IsClosedSocialSaved(original,TerminalDigest);}
            catch{return false;}
        }
    }
    internal static bool TryReadLocal(RebirthNpcTradeLocalOwnerLoad.Source source,RebirthNpcTradeOriginal original,out RebirthNpcTradeClosedHistory history)
    {
        history=null;
        try
        {
            if(source==null||!source.Matches(original)||!source.PersistCurrent(original)||!source.TryStage(out int step,out int phase)||phase!=3||
                step!=RebirthNpcTradePrefix.Changes(original)*2||!RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(source.Player,original,out var ownerTerminal)||
                !RebirthNpcTradeService.HostPrepared.RetainedSource.TryCaptureLocal(source,out var saved)||saved.Original.Image!=original.Image||
                !saved.TryClosedHistory(out var terminal)||terminal!=ownerTerminal||!source.Matches(original))return false;
            string path=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),
                RebirthRemoteResourceDurableJournal.Digest(original.Account),original.Operation.ToString("N")+".xml");
            history=new RebirthNpcTradeClosedHistory(original,path,terminal);
            RebirthNpcTradeColdFence.ObserveClosed(history);return true;
        }
        catch{return false;}
    }
    internal static bool IsClosedPreparation(string path,RebirthNpcTradeOriginal original,string preparation)
    {
        try
        {
            if(original==null||Read(path,64*1024*1024)!=preparation)return false;
            var prepared=Parse(preparation,64*1024*1024);string before=prepared.Element("savedNpcBefore")?.Value;
            if(preparation!=new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest",original.Digest),
                new XElement("original",original.Image),new XElement("savedNpcBefore",before)).ToString(SaveOptions.DisableFormatting)||
                !RebirthNpcInventoryPersistenceStore.TryReadTradePlanFromSavedBefore(original,before,out var plan))return false;
            string digest=RebirthRemoteResourceDurableJournal.Digest(preparation),operation=original.Operation.ToString("N");
            string grant=new XElement("npcTradeOwnerGrant",new XAttribute("version",1),new XAttribute("operation",operation),
                new XAttribute("originalDigest",original.Digest),new XAttribute("preparationDigest",digest)).ToString(SaveOptions.DisableFormatting);
            if(Read(path+".grant",4096)!=grant)return false;
            string terminal=Read(path+".terminal",64*1024*1024),after=Parse(terminal,64*1024*1024).Element("savedNpcAfter")?.Value;
            if(terminal!=new XElement("npcTradeTerminal",new XAttribute("version",1),new XAttribute("operation",operation),
                new XAttribute("originalDigest",original.Digest),new XAttribute("preparationDigest",digest),new XElement("savedNpcAfter",after)).ToString(SaveOptions.DisableFormatting)||
                !RebirthNpcInventoryPersistenceStore.MatchesTradeSavedImage(original,plan,after,true))return false;
            string closed=new XElement("npcTradeClosed",new XAttribute("version",1),new XAttribute("operation",operation),
                new XAttribute("originalDigest",original.Digest),new XAttribute("preparationDigest",digest),
                new XAttribute("terminalDigest",RebirthRemoteResourceDurableJournal.Digest(terminal))).ToString(SaveOptions.DisableFormatting);
            return Read(path+".closed",4096)==closed&&Read(path,64*1024*1024)==preparation;
        }
        catch{return false;}
    }
    internal static bool TryRead(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,Guid operation,
        string originalDigest,string terminalDigest,out RebirthNpcTradeClosedHistory history)
    {
        history=null;
        try
        {
            var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=packet?.Sender;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.IsServer!=true||packet==null||user==null||peer==null||
                operation==Guid.Empty||!packet.ValidEntityIdForSender(player)||!packet.ValidUserIdForSender(user)||
                !ReferenceEquals(manager.Clients.ForEntityId(player),peer)||peer.entityId!=player||peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||
                !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||
                world.GetEntity(player) is not EntityPlayer actor||!ReferenceEquals(actor.world,world))return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),selected=GameIO.GetSaveGameDir();
            if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected)||!Guid.TryParse(world.worldState?.Guid,out var worldId))return false;
            string directory=Path.GetFullPath(selected),path=Path.Combine(directory,"RebirthData","NpcTradeOriginals",worldId.ToString("N"),
                RebirthRemoteResourceDurableJournal.Digest(owner.CanonicalId),operation.ToString("N")+".xml");
            // Read the small terminal disposition first; pending originals can
            // never become historical success through absent or equal frames.
            string closed=Read(path+".closed",4096);
            var close=Parse(closed,4096);
            if((string)close.Attribute("terminalDigest")!=terminalDigest||(string)close.Attribute("originalDigest")!=originalDigest)return false;
            string preparation=Read(path,64*1024*1024);var prepared=Parse(preparation,64*1024*1024);
            var original=RebirthNpcTradeOriginal.Read(prepared.Element("original")?.Value);
            string before=prepared.Element("savedNpcBefore")?.Value;
            if(original.Operation!=operation||original.Digest!=originalDigest||original.Account!=owner.CanonicalId||original.Creation!=creation||
                original.World!=world.worldState.Guid||original.CopyQuote().ActorId!=user.ToString()||
                new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest",originalDigest),
                    new XElement("original",original.Image),new XElement("savedNpcBefore",before)).ToString(SaveOptions.DisableFormatting)!=preparation||
                !RebirthNpcInventoryPersistenceStore.TryReadTradePlanFromSavedBefore(original,before,out var plan))return false;
            string preparedDigest=RebirthRemoteResourceDurableJournal.Digest(preparation);
            string expectedClosed=new XElement("npcTradeClosed",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("originalDigest",originalDigest),new XAttribute("preparationDigest",preparedDigest),new XAttribute("terminalDigest",terminalDigest))
                .ToString(SaveOptions.DisableFormatting);
            if(closed!=expectedClosed)return false;
            string expectedGrant=new XElement("npcTradeOwnerGrant",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("originalDigest",originalDigest),new XAttribute("preparationDigest",preparedDigest)).ToString(SaveOptions.DisableFormatting);
            if(Read(path+".grant",4096)!=expectedGrant)return false;
            string terminal=Read(path+".terminal",64*1024*1024);var terminalRoot=Parse(terminal,64*1024*1024);
            string savedAfter=terminalRoot.Element("savedNpcAfter")?.Value;
            if(RebirthRemoteResourceDurableJournal.Digest(terminal)!=terminalDigest||
                new XElement("npcTradeTerminal",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                    new XAttribute("originalDigest",originalDigest),new XAttribute("preparationDigest",preparedDigest),
                    new XElement("savedNpcAfter",savedAfter)).ToString(SaveOptions.DisableFormatting)!=terminal||
                !RebirthNpcInventoryPersistenceStore.MatchesTradeSavedImage(original,plan,savedAfter,true))return false;
            if(!ReferenceEquals(world,GameManager.Instance?.World)||Path.GetFullPath(GameIO.GetSaveGameDir())!=directory||
                !ReferenceEquals(actor,world.GetEntity(player))||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||
                !ReferenceEquals(manager.Clients.ForEntityId(player),peer)||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var actual)||actual.CanonicalId!=owner.CanonicalId||Read(path+".closed",4096)!=closed)return false;
            history=new RebirthNpcTradeClosedHistory(original,path,terminalDigest);
            RebirthNpcTradeColdFence.ObserveClosed(history);return true;
        }
        catch{return false;}
    }
    private static XElement Parse(string image,int maximum)
    {
        using var text=new StringReader(image);
        using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum});
        return XElement.Load(reader);
    }
    private static string Read(string path,int maximum)
    {
        Safe(path);
        if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new InvalidDataException();
        string result;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>maximum)throw new InvalidDataException();
            using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,8192,true);result=reader.ReadToEnd();
            if(Encoding.UTF8.GetByteCount(result)!=stream.Length)throw new InvalidDataException();
        }
        Safe(path);return result;
    }
    private static void Safe(string path)
    {
        for(var directory=new DirectoryInfo(Path.GetDirectoryName(path));directory!=null;directory=directory.Parent)
            if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Trade history redirected");
    }
}
