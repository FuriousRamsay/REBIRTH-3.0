using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Platform;

// Durable no-use disposition query only. No permission, item setter, NPC
// transaction or native owner restoration is reachable from this type.
internal sealed class RebirthNpcTradeUnusedHistory
{
    internal readonly Guid Operation;
    internal readonly string OriginalDigest,UnusedDigest;
    private RebirthNpcTradeUnusedHistory(Guid operation,string original,string unused)
    {Operation=operation;OriginalDigest=original;UnusedDigest=unused;}
    internal static bool TryRead(NetPackage packet,PlatformUserIdentifierAbs user,int player,uint connection,Guid operation,string originalDigest,
        out RebirthNpcTradeUnusedHistory history)
    {
        history=null;
        try
        {
            var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=packet?.Sender;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.IsServer!=true||packet==null||user==null||peer==null||
                operation==Guid.Empty||!packet.ValidEntityIdForSender(player)||!packet.ValidUserIdForSender(user)||
                !ReferenceEquals(manager.Clients.ForEntityId(player),peer)||peer.entityId!=player||peer.disconnecting||!peer.loginDone||!peer.bAttachedToEntity||
                peer.netConnection==null||peer.netConnection.Length<1||peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected()||
                !RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||
                world.GetEntity(player) is not EntityPlayer actor||!ReferenceEquals(actor.world,world))return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),selected=GameIO.GetSaveGameDir();
            if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected)||!Guid.TryParse(world.worldState?.Guid,out var worldId))return false;
            string directory=Path.GetFullPath(selected),path=Path.Combine(directory,"RebirthData","NpcTradeOriginals",worldId.ToString("N"),
                RebirthRemoteResourceDurableJournal.Digest(owner.CanonicalId),operation.ToString("N")+".unused");
            string image=Read(path);var original=Describe(image,out _);
            if(original.Operation!=operation||original.Digest!=originalDigest||original.Account!=owner.CanonicalId||original.Creation!=creation||
                original.World!=world.worldState.Guid||original.CopyQuote().ActorId!=user.ToString())return false;
            // This record can only be published after the private quote is
            // irrevocably consumed as unused. Current owner slots or an absent
            // grant file do not supply the historical no-use proof.
            if(!ReferenceEquals(world,GameManager.Instance?.World)||Path.GetFullPath(GameIO.GetSaveGameDir())!=directory||
                !ReferenceEquals(actor,world.GetEntity(player))||RebirthCamperCharacterAccess.CurrentCreation(actor)!=creation||
                !ReferenceEquals(manager.Clients.ForEntityId(player),peer)||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(player,connection)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var actual)||actual.CanonicalId!=owner.CanonicalId||Read(path)!=image)return false;
            history=new RebirthNpcTradeUnusedHistory(operation,originalDigest,RebirthRemoteResourceDurableJournal.Digest(image));return true;
        }
        catch{return false;}
    }
    internal static RebirthNpcTradeOriginal Describe(string image,out string reason)
    {
        if(image==null||image.Length<1||image.Length>32*1024*1024)throw new InvalidDataException();
        using var text=new StringReader(image);
        using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32*1024*1024});
        var root=XElement.Load(reader);reason=(string)root.Attribute("reason");
        if(reason!="expired"&&reason!="stockChanged")throw new InvalidDataException();
        var original=RebirthNpcTradeOriginal.Read(root.Element("original")?.Value);
        if(image!=new XElement("npcTradeUnused",new XAttribute("version",1),new XAttribute("reason",reason),
            new XAttribute("originalDigest",original.Digest),new XElement("original",original.Image)).ToString(SaveOptions.DisableFormatting))throw new InvalidDataException();
        return original;
    }
    private static string Read(string path)
    {
        Safe(path);
        if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new InvalidDataException();
        string image;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long length=stream.Length;if(length<1||length>32*1024*1024)throw new InvalidDataException();
            using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,8192,true);image=reader.ReadToEnd();
            if(stream.Length!=length||stream.Position!=length||Encoding.UTF8.GetByteCount(image)!=length)throw new InvalidDataException();
        }
        Safe(path);return image;
    }
    private static void Safe(string path)
    {for(var parent=new DirectoryInfo(Path.GetDirectoryName(path));parent!=null;parent=parent.Parent)if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();}
}
