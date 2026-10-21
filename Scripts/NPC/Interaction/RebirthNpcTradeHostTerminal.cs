using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthNpcTradeService
{
    internal sealed class HostTerminal
    {
        internal readonly HostPrepared Prepared;
        internal readonly string Image,Digest,PathName;
        private HostTerminal(HostPrepared prepared,string saved)
        {
            Prepared=prepared;PathName=prepared.PathName+".terminal";
            Image=new XElement("npcTradeTerminal",new XAttribute("version",1),
                new XAttribute("operation",prepared.Original.Operation.ToString("N")),
                new XAttribute("originalDigest",prepared.Original.Digest),
                new XAttribute("preparationDigest",RebirthRemoteResourceDurableJournal.Digest(prepared.Image)),
                new XElement("savedNpcAfter",saved)).ToString(SaveOptions.DisableFormatting);
            Digest=RebirthRemoteResourceDurableJournal.Digest(Image);
        }
        private HostTerminal(HostPrepared prepared,string image,bool retained)
        {Prepared=prepared;PathName=prepared.PathName+".terminal";Image=image;Digest=RebirthRemoteResourceDurableJournal.Digest(image);}
        internal static bool TrySettle(OwnerAdmission.SavedAfter observation,out HostTerminal terminal)
        {
            terminal=null;
            try
            {
                if(observation==null||!observation.Current())return false;
                var prepared=observation.Grant.Prepared;
                var result=RebirthNpcInventoryTransactionService.ApplyTradeOwnerOriginal(observation,out _);
                if(result!=RebirthNpcInventoryTransactionResult.Applied&&result!=RebirthNpcInventoryTransactionResult.Replayed)return false;
                if(!observation.Current()||!RebirthNpcInventoryPersistenceStore.TryFlushOriginalBacking())return false;
                string saved=RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(prepared.Original,prepared.NpcPlan,true);
                if(saved==null||!observation.Current())return false;
                var next=new HostTerminal(prepared,saved);
                byte[] bytes=new UTF8Encoding(false,true).GetBytes(next.Image);
                if(bytes.Length>64*1024*1024)return false;
                Safe(next.PathName);
                if(File.Exists(next.PathName))
                {
                    next=ReadExisting(prepared);if(next==null)return false;
                }
                else
                {
                    if(!observation.Current())return false;
                    using(var stream=new FileStream(next.PathName,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                }
                if(!next.ReadExact()||!observation.Current()||
                    RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(prepared.Original,prepared.NpcPlan,true)==null)return false;
                terminal=next;return true;
            }
            catch{return false;}
        }
        internal static HostTerminal ReadExisting(HostPrepared prepared)
        {
            string path=prepared.PathName+".terminal";Safe(path);
            if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)return null;
            string image;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<1||stream.Length>64*1024*1024)return null;
                using var text=new StreamReader(stream,new UTF8Encoding(false,true),false,8192,true);image=text.ReadToEnd();
            }
            using var input=new StringReader(image);
            using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64*1024*1024});
            var root=XElement.Load(reader);
            if(root.Name!="npcTradeTerminal"||root.Attributes().Count()!=4||(int?)root.Attribute("version")!=1||
                (string)root.Attribute("operation")!=prepared.Original.Operation.ToString("N")||
                (string)root.Attribute("originalDigest")!=prepared.Original.Digest||
                (string)root.Attribute("preparationDigest")!=RebirthRemoteResourceDurableJournal.Digest(prepared.Image)||
                root.Elements().Count()!=1||root.Nodes().Count()!=1||root.Element("savedNpcAfter")==null||
                root.Element("savedNpcAfter").HasAttributes||root.Element("savedNpcAfter").HasElements||
                root.ToString(SaveOptions.DisableFormatting)!=image||
                !RebirthNpcInventoryPersistenceStore.MatchesTradeSavedImage(prepared.Original,prepared.NpcPlan,root.Element("savedNpcAfter").Value,true))return null;
            return new HostTerminal(prepared,image,true);
        }
        private bool ReadExact()
        {
            Safe(PathName);
            if((File.GetAttributes(PathName)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)return false;
            byte[] expected=new UTF8Encoding(false,true).GetBytes(Image),buffer=new byte[8192];
            using(var stream=new FileStream(PathName,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=expected.Length)return false;int offset=0;
                while(offset<expected.Length)
                {
                    int count=stream.Read(buffer,0,Math.Min(buffer.Length,expected.Length-offset));if(count<=0)return false;
                    for(int i=0;i<count;i++)if(buffer[i]!=expected[offset+i])return false;
                    offset+=count;
                }
                if(stream.ReadByte()!=-1)return false;
            }
            Safe(PathName);return true;
        }
        internal bool IsRetained(){try{return Prepared.ReadRetained()&&ReadExact();}catch{return false;}}
        private static void Safe(string path)
        {
            for(var directory=new DirectoryInfo(Path.GetDirectoryName(path));directory!=null;directory=directory.Parent)
                if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Trade terminal redirected");
        }
    }
}
