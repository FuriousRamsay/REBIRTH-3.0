using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// A save-owned identity survives relocation. Never regenerate an observable damaged
// final/backup/temp identity; no refund operation may manufacture a replacement world.
internal static class RemoteResourceWorldIdentity
{
    private static readonly object Gate=new object();
    private static bool Current(World world,string root)
    {
        if(world==null||!RebirthWorldCharacterRepository.IsServerAuthority||GameManager.Instance==null||
            !ReferenceEquals(GameManager.Instance.World,world))return false;
        string live=GameIO.GetSaveGameDir();
        return !string.IsNullOrEmpty(live)&&Path.GetFullPath(live)==root;
    }
    private static bool Read(string path,out string key)
    {
        key=null;
        try
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<1||stream.Length>4096)return false;
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096};
                using(var reader=XmlReader.Create(stream,settings))
                {
                    var doc=XDocument.Load(reader,LoadOptions.None);var node=doc.Root;
                    if(node==null||node.Name!="remoteResourceWorld"||node.Attributes().Count()!=2||
                        (string)node.Attribute("version")!="1"||node.HasElements||
                        node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                        doc.Nodes().Any(n=>!ReferenceEquals(n,node)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
                    var value=(string)node.Attribute("key");
                    if(!RemoteResourceRefundRecord.ValidStorageKey(value))return false;
                    key=value;return true;
                }
            }
        }
        catch{return false;}
    }
    internal static bool TryGet(World world,out string key)
    {
        key=null;
        lock(Gate)
        {
            string candidate=null;
            try
            {
                string raw=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(raw))return false;
                string root=Path.GetFullPath(raw);if(!Current(world,root))return false;
                string path=Path.Combine(root,"RebirthData","RemoteResources","worldIdentity.xml");
                if(File.Exists(path)){if(!Read(path,out var existing)||!Current(world,root))return false;key=existing;return true;}
                if(File.Exists(path+".bak")||File.Exists(path+".tmp"))return false;
                if(RebirthWorldCharacterRepository.HasRetainedRemoteResourceRefunds()||!RemoteResourceWorldCustodyScan.CanInitialize(root))return false;
                string created=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
                candidate=path+".new_"+Guid.NewGuid().ToString("N");
                var doc=new XDocument(new XElement("remoteResourceWorld",new XAttribute("version",1),new XAttribute("key",created)));
                // Unique staging plus a no-overwrite rename cannot replace an identity
                // concurrently installed after the initial absence check.
                RebirthAtomicXmlFile.TryWrite(candidate,doc,out _);
                if(!Read(candidate,out var staged)||staged!=created||!Current(world,root))return false;
                using(var flush=new FileStream(candidate,FileMode.Open,FileAccess.ReadWrite,FileShare.None))flush.Flush(true);
                if(RebirthWorldCharacterRepository.HasRetainedRemoteResourceRefunds()||File.Exists(path+".bak")||File.Exists(path+".tmp")||!Current(world,root))return false;
                File.Move(candidate,path);
                if(!Read(path,out var saved)||saved!=created||!Current(world,root))return false;key=saved;return true;
            }
            catch{key=null;return false;}
            finally
            {
                if(candidate!=null)
                {
                    try{if(File.Exists(candidate))File.Delete(candidate);}catch{}
                    try{if(File.Exists(candidate+".tmp"))File.Delete(candidate+".tmp");}catch{}
                }
            }
        }
    }
}