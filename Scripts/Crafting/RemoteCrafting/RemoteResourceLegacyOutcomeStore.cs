using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Legacy removal write-ahead fence and retained observations. These files are
// descriptive originals, NOT saved native source/owner receipts. A cold file
// never authorizes another removal, grant, refund or native callback.
internal static class RemoteResourceLegacyOutcomeStore
{
    private const int MaximumBytes=16*1024*1024,MaximumRows=2048;
    private const long MaximumRetainedBytes=256L*1024*1024;
    private static readonly HashSet<Entry> live=new HashSet<Entry>();
    private static long retainedBytes;
    private static bool reading;
    private sealed class Publication
    {
        internal string Path;internal byte[] Bytes;internal bool Created,Saved;
        private string digest;private int length;
        internal void Compact(){if(Saved){digest=Hash(Bytes);length=Bytes.Length;Bytes=null;}}
        internal byte[] ReadExact()
        {
            if(!Saved)throw new InvalidDataException();Safe(Path);
            using(var stream=new FileStream(Path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                int expected=Bytes?.Length??length;if(expected<1||expected>MaximumBytes||stream.Length!=expected)throw new InvalidDataException();
                var data=new byte[expected];int offset=0;
                while(offset<data.Length){int count=stream.Read(data,offset,data.Length-offset);if(count<1)throw new EndOfStreamException();offset+=count;}
                if(Hash(data)!=(Bytes!=null?Hash(Bytes):digest))throw new InvalidDataException();return data;
            }
        }
        internal bool Publish(Entry owner)
        {
            if(Saved)return owner.MetadataScope()&&(Bytes!=null?Exact(Path,Bytes):ExactDigest(Path,digest,length));
            if(!owner.MetadataScope())return false;
            try
            {
                Safe(Path);string temporary=Path+".tmp";
                if(!Created)
                {
                    if(Node(Path)||Node(temporary)||Node(Path+".bak"))return false;
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));Safe(Path);
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {Created=true;stream.Write(Bytes,0,Bytes.Length);stream.Flush(true);}
                }
                if(Node(temporary))
                {
                    // Only the exact complete file created by this private
                    // publisher may retry fsync/rename. Never repair partials.
                    if(!Exact(temporary,Bytes)||Node(Path))return false;
                    using(var stream=new FileStream(temporary,FileMode.Open,FileAccess.ReadWrite,FileShare.None))stream.Flush(true);
                    if(!owner.MetadataScope()||!Exact(temporary,Bytes))return false;
                    File.Move(temporary,Path);
                }
                if(!Created||!owner.MetadataScope()||!Exact(Path,Bytes))return false;
                Saved=true;return true;
            }
            catch{return false;}
        }
    }
    internal sealed class Entry
    {
        private World world;private EntityPlayer actor;private readonly WeakReference<World> origin;
        private ConnectionManager manager;private ClientInfo peer;private INetConnection connection;
        private readonly string root,worldKey,worldGuid,owner,canonical,creation,key;private readonly int deaths;
        private byte[] requirements;private readonly Publication before,entered;
        private Publication observed;private RemoteResourceTransactionOutcome pending;
        private bool invoked,retired,released;private long charge;
        private XElement original;
        private Entry(World world,EntityPlayer actor,PlatformUserIdentifierAbs user,ulong epoch,ulong request,RemoteResourceClientOperation kind,
            string root,string worldKey,byte[] requirements)
        {
            this.world=world;this.actor=actor;origin=new WeakReference<World>(world);this.root=root;this.worldKey=worldKey;this.requirements=requirements;
            manager=SingletonMonoBehaviour<ConnectionManager>.Instance;peer=manager.Clients?.ForEntityId(actor.entityId);
            connection=peer?.netConnection?.Length>0?peer.netConnection[0]:null;
            worldGuid=world.worldState?.Guid;canonical=user.CombinedString.Trim();owner=Hash(Encoding.UTF8.GetBytes(canonical));
            creation=RebirthCamperCharacterAccess.CurrentCreation(actor)??string.Empty;deaths=actor.Died;
            key=Hash(Encoding.UTF8.GetBytes(worldKey+"|"+owner+"|"+epoch.ToString(CultureInfo.InvariantCulture)+"|"+request.ToString(CultureInfo.InvariantCulture)+"|"+(byte)kind));
            original=new XElement("legacyResourceOriginal",new XAttribute("version",1),new XAttribute("world",worldKey),new XAttribute("nativeWorld",worldGuid??string.Empty),
                new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("deaths",deaths),new XAttribute("actor",actor.entityId),
                new XAttribute("epoch",epoch.ToString(CultureInfo.InvariantCulture)),new XAttribute("request",request.ToString(CultureInfo.InvariantCulture)),new XAttribute("kind",(byte)kind),
                new XAttribute("requirementsHash",Hash(requirements)),new XElement("requirements",Convert.ToBase64String(requirements)));
            before=new Publication{Path=FileName("original"),Bytes=Encode(original)};
            entered=new Publication{Path=FileName("entered"),Bytes=Encode(new XElement("legacyResourceAttempt",new XAttribute("version",1),
                new XAttribute("original",Hash(before.Bytes)),new XElement(original)))};
        }
        private string DirectoryName=>System.IO.Path.Combine(root,"RebirthData","RemoteResources","LegacyOutcomes",worldKey,owner);
        private string FileName(string phase)=>System.IO.Path.Combine(DirectoryName,key+"."+phase+".xml");
        internal bool Current(EntityPlayer candidate)
        {
            try{return actor!=null&&!retired&&!released&&ReferenceEquals(actor,candidate)&&ThreadManager.IsMainThread()&&
            ReferenceEquals(GameManager.Instance?.World,world)&&world?.IsRemote()==false&&ReferenceEquals(actor.world,world)&&
            ReferenceEquals(world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deaths&&world.worldState?.Guid==worldGuid&&
            (RebirthCamperCharacterAccess.CurrentCreation(actor)??string.Empty)==creation&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&manager.IsServer&&
            System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==root&&
            GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId?.CombinedString?.Trim()==canonical&&
            (actor is EntityPlayerLocal?ReferenceEquals(world.GetPrimaryPlayer(),actor):peer!=null&&peer.loginDone&&!peer.disconnecting&&connection!=null&&!connection.IsDisconnected()&&
                ReferenceEquals(manager.Clients?.ForEntityId(actor.entityId),peer)&&peer.netConnection?.Length>0&&ReferenceEquals(peer.netConnection[0],connection));}
            catch{return false;}
        }
        internal bool TryEnter(EntityPlayer candidate)
        {
            if(invoked||pending!=null||observed!=null||!Current(candidate)||!before.Publish(this)||!entered.Publish(this)||!Current(candidate))return false;
            invoked=true;return true; // One warm invocation; never restored from XML.
        }
        internal ItemStack[] CopyRequirements()=>RebirthCamperInventorySnapshot.Restore((byte[])requirements.Clone());
        internal void Observe(RemoteResourceTransactionOutcome outcome)
        {
            if(pending!=null)return;
            if(!invoked&&(outcome.State!=RemoteResourceTransactionOutcomeState.Failed||outcome.Removed.Count!=0))
                throw new InvalidDataException("Uninvoked legacy original cannot describe a grant.");
            // Caller transfers its private returned/removal observation. It is
            // never a public reply list; serialization may retry after teardown.
            pending=outcome;
        }
        internal bool PublishObserved()
        {
            if(released)return false;
            if(observed?.Saved==true)return before.Publish(this)&&(!invoked||entered.Publish(this))&&observed.Publish(this);
            if(pending==null)return false;
            try
            {
                if(observed==null)
                {
                    var image=new XElement("legacyResourceObserved",new XAttribute("version",1),new XAttribute("original",Hash(before.Bytes)),
                        new XAttribute("nativeInvoked",invoked),new XAttribute("nativeSaved",false),new XAttribute("state",(byte)pending.State),
                        new XElement(original),new XElement("reason",pending.Reason??string.Empty),
                        new XElement("removed",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(pending.Removed.ToArray()))));
                    byte[] bytes=Encode(image);observed=new Publication{Path=FileName("observed"),Bytes=bytes};
                }
                if(!before.Publish(this)||invoked&&!entered.Publish(this)||!observed.Publish(this))return false;
                before.Compact();if(invoked)entered.Compact();else entered.Bytes=null;observed.Compact();
                requirements=null;original=null;pending=null;retainedBytes-=charge;charge=0;return true;
            }
            catch{return false;}
        }
        internal bool HasInvocation=>invoked;
        // Keep the originally captured body private. A lookup must not replace
        // it with a later entity that happens to reuse the same numeric ID.
        internal bool TryReadCurrent(out RemoteResourceTransactionOutcome outcome)=>TryReadWarm(actor,out outcome);
        internal bool TryReadWarm(EntityPlayer candidate,out RemoteResourceTransactionOutcome outcome)
        {
            outcome=null;const long workspace=4L*MaximumBytes;
            if(reading||!Current(candidate)||observed?.Saved!=true||!MetadataScope()||retainedBytes>MaximumRetainedBytes-workspace)return false;
            reading=true;retainedBytes+=workspace;
            try
            {
                // A returned observation cannot outlive the immutable original
                // and invocation fence that qualified its only native attempt.
                if(!before.Publish(this)||invoked&&!entered.Publish(this))return false;
                byte[] bytes=observed.ReadExact();
                using(var stream=new MemoryStream(bytes,false))
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
                {
                    var image=XElement.Load(reader,LoadOptions.None);
                    var removed=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image.Element("removed").Value));
                    var result=new RemoteResourceTransactionOutcome{State=(RemoteResourceTransactionOutcomeState)(int)image.Attribute("state"),
                        Reason=image.Element("reason").Value,Removed=removed.ToList()};
                    if(!Current(candidate)||!MetadataScope())return false;outcome=result;return true;
                }
            }
            catch{return false;}
            finally{retainedBytes-=workspace;reading=false;}
        }
        internal bool IsWorld(World candidate)=>origin.TryGetTarget(out var value)&&ReferenceEquals(value,candidate);
        // Archival can finish only in the originally captured save namespace.
        // Changing the current global save never redirects these bytes.
        internal bool MetadataScope()=>!released&&ThreadManager.IsMainThread()&&ReadWorldKey(root,out var actual)&&actual==worldKey;
        internal void Retire()
        {
            if(!invoked&&pending==null)Observe(new RemoteResourceTransactionOutcome{State=RemoteResourceTransactionOutcomeState.Failed,Reason="Original request ended before native removal."});
            retired=true;actor=null;world=null;manager=null;peer=null;connection=null;
            if(PublishObserved())Release();
        }
        internal bool IsRetired=>retired;
        internal void FinishRetiredArchive(){if(retired&&PublishObserved())Release();}
        private void Release()
        {
            if(released)return;released=true;live.Remove(this);
            retainedBytes-=charge;charge=0;
            before.Bytes=entered.Bytes=null;if(observed!=null)observed.Bytes=null;pending=null;
        }
        internal static bool Capture(World world,EntityPlayer actor,PlatformUserIdentifierAbs user,ulong epoch,ulong request,RemoteResourceClientOperation kind,ItemStack[] needs,out Entry entry,
            ClientInfo expectedPeer=null,INetConnection expectedConnection=null)
        {
            entry=null;
            try
            {
                if(live.Count>=MaximumRows||needs==null||needs.Length<1||needs.Length>4096||user?.CombinedString==null||user.CombinedString.Trim().Length<1||
                    user.CombinedString.Length>512||epoch==0||request==0||!RemoteResourceWorldIdentity.TryGet(world,out string worldKey))return false;
                string raw=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(raw))return false;string root=System.IO.Path.GetFullPath(raw);
                byte[] payload=RebirthCamperInventorySnapshot.Capture(needs);if(payload.Length>6*1024*1024)return false;
                var candidate=new Entry(world,actor,user,epoch,request,kind,root,worldKey,payload);
                if(expectedPeer!=null&&(!ReferenceEquals(candidate.peer,expectedPeer)||!ReferenceEquals(candidate.connection,expectedConnection)))return false;
                if(!candidate.Current(actor)||!candidate.MetadataScope()||candidate.creation.Length>512||candidate.worldGuid?.Length>128)return false;
                Safe(candidate.DirectoryName);int count=0;
                if(Directory.Exists(candidate.DirectoryName))foreach(string path in Directory.EnumerateFileSystemEntries(candidate.DirectoryName))
                {
                    if(++count>MaximumRows*6||System.IO.Path.GetFileName(path).StartsWith(candidate.key+".",StringComparison.Ordinal))return false;
                }
                // Reserve the complete maximum observation before any native
                // invocation, including XML/native clone/read workspace.
                long bytes=checked(4L*(candidate.before.Bytes.Length+candidate.entered.Bytes.Length)+2L*payload.Length+4L*MaximumBytes+65536);
                if(retainedBytes>MaximumRetainedBytes-bytes)return false;
                candidate.charge=bytes;
                retainedBytes+=bytes;live.Add(candidate);entry=candidate;return true;
            }
            catch{return false;}
        }
    }
    private static int archiveOffset;private static float nextArchive;
    internal static void PumpArchive()
    {
        if(!ThreadManager.IsMainThread()||UnityEngine.Time.realtimeSinceStartup<nextArchive)return;
        nextArchive=UnityEngine.Time.realtimeSinceStartup+1f;
        var entries=live.Where(value=>value.IsRetired).ToArray();int count=Math.Min(8,entries.Length);
        for(int index=0;index<count;index++)entries[(archiveOffset+index)%entries.Length].FinishRetiredArchive();
        archiveOffset=(archiveOffset+count)%Math.Max(1,entries.Length);
    }
    internal static void RetireWorld(World world)
    {if(ThreadManager.IsMainThread()&&world!=null)foreach(var entry in live.Where(value=>value.IsWorld(world)).ToArray())entry.Retire();}
    private static bool Node(string path)
    {try{File.GetAttributes(path);return true;}catch(FileNotFoundException){return false;}catch(DirectoryNotFoundException){return false;}catch{return true;}}
    private static void Safe(string path)
    {
        for(string value=System.IO.Path.GetFullPath(path);!string.IsNullOrEmpty(value);value=System.IO.Path.GetDirectoryName(value))
        {try{if((File.GetAttributes(value)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Legacy original path changed.");}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}
    }
    private static string Hash(byte[] value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(value)).Replace("-",string.Empty).ToLowerInvariant();}
    private static byte[] Encode(XElement value)
    {byte[] bytes=Encoding.UTF8.GetBytes(value.ToString(SaveOptions.DisableFormatting));if(bytes.Length<1||bytes.Length>MaximumBytes)throw new InvalidDataException("Legacy outcome bound.");return bytes;}
    private static bool Exact(string path,byte[] bytes)
    {
        try
        {
            Safe(path);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=bytes.Length)return false;byte[] buffer=new byte[8192];int offset=0,read;
                while((read=stream.Read(buffer,0,buffer.Length))>0){for(int i=0;i<read;i++)if(buffer[i]!=bytes[offset+i])return false;offset+=read;}
                return offset==bytes.Length;
            }
        }
        catch{return false;}
    }
    private static bool ExactDigest(string path,string expected,int length)
    {
        try
        {Safe(path);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=SHA256.Create())
            return stream.Length==length&&BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",string.Empty).ToLowerInvariant()==expected;}
        catch{return false;}
    }
    private static bool ReadWorldKey(string root,out string key)
    {
        key=null;
        try
        {
            string path=System.IO.Path.Combine(root,"RebirthData","RemoteResources","worldIdentity.xml");Safe(path);
            if(Node(path+".tmp"))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<1||stream.Length>4096)return false;
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))
                {
                    var node=XElement.Load(reader,LoadOptions.None);string value=(string)node.Attribute("key");
                    if(node.Name!="remoteResourceWorld"||node.Attributes().Count()!=2||(string)node.Attribute("version")!="1"||node.HasElements||
                        !string.IsNullOrWhiteSpace(node.Value)||value?.Length!=64||value.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
                    key=value;return true;
                }
            }
        }
        catch{return false;}
    }
}
