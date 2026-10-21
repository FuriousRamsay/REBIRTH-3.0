using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthNpcMetabolismCustody
{
    private static World coldIndexWorld;
    private static string coldIndexSave;
    private static readonly System.Collections.Generic.Dictionary<RebirthNpcStableId,bool> coldIndex=new System.Collections.Generic.Dictionary<RebirthNpcStableId,bool>();
    private static void InvalidateColdIndex()
    {if(ThreadManager.IsMainThread())coldIndex.Clear();}
    private static int StageOrder(string stage)
    {
        switch(stage)
        {
            case "prepared":return 0;case "owner-before-saved":return 1;case "source-intent":return 2;
            case "source-returned":case "source-not-entered":return 3;case "source-saved":case "no-effect":return 4;
            case "ingestion-intent":return 5;case "ingestion-saved":return 6;case "secondary-intent":return 7;
            case "secondary-returned":return 8;case "secondary-state-saved":return 9;case "owner-secondary-intent":return 10;
            case "owner-secondary-returned":return 11;case "owner-secondary-saved":return 12;case "owner-effect-intent":return 13;
            case "owner-effect-returned":return 14;case "owner-after-saved":return 15;case "committed":return 16;
            default:throw new InvalidDataException("Unknown original consumption stage");
        }
    }
    private static void ConvergeExistingStageHead(Original original,string stage)
    {
        var head=ReadRecord(Path.Combine(original.Directory,"control.xml"));
        if(head.Name!="npcConsumptionHead"||head.Attributes().Count()!=2||(int?)head.Attribute("version")!=1||head.Elements().Count()!=1||(string)head.Element("original")!=original.Text)throw new InvalidDataException("Original consumption control changed");
        string current=(string)head.Attribute("stage");
        // Recover an interrupted file->head publication BEFORE its caller can
        // invoke a first effect, without rolling back a later attempted marker.
        if(StageOrder(current)<StageOrder(stage))AdvanceHead(original,stage);
    }
    private static XElement ReadRecord(string path)
    {
        RequirePath(Path.GetDirectoryName(path));var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>16*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("NPC consumption record unavailable");
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16*1024*1024}))return XElement.Load(reader);
    }
    private static bool Stage(string directory,string name,string text)
    {
        var row=ReadRecord(Path.Combine(directory,name+".xml"));return row.Name=="npcSourceConsumption"&&row.Attributes().Count()==2&&(int?)row.Attribute("version")==1&&(string)row.Attribute("stage")==name&&(string)row.Element("original")==text;
    }
    private static bool HasColdHold(RebirthNpcStableId id)
    {
        if(!ThreadManager.IsMainThread())return true;
        string save;
        try{save=Path.GetFullPath(GameIO.GetSaveGameDir());}catch{return true;}
        var world=GameManager.Instance?.World;if(world==null)return true;
        if(!ReferenceEquals(coldIndexWorld,world)||coldIndexSave!=save){coldIndex.Clear();coldIndexWorld=world;coldIndexSave=save;}
        if(coldIndex.TryGetValue(id,out bool retained))return retained;
        if(coldIndex.Count>=256)
        {
            // Negative entries are disposable scan results, not custody. A
            // lifetime of unrelated NPC lookups must not exhaust admission.
            foreach(var key in coldIndex.Where(pair=>!pair.Value).Select(pair=>pair.Key).ToArray())coldIndex.Remove(key);
            if(coldIndex.Count>=256)return true;
        }
        // Parse scratch is retained BEFORE loading bounded XML/full native images.
        // A failed allocation cannot turn missing observation into permission.
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var scratch))return true;
        using(scratch){bool result=ScanColdHold(id);coldIndex[id]=result;return result;}
    }
    private static bool ScanColdHold(RebirthNpcStableId id)
    {
        try
        {
            string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","NpcConsumption");if(!Directory.Exists(root))return false;RequirePath(root);
            var directories=Directory.EnumerateDirectories(root).Take(257).ToArray();if(directories.Length>256)return true;
            foreach(string directory in directories)
            {
                var prepared=ReadRecord(Path.Combine(directory,"prepared.xml"));string text=(string)prepared.Element("original");
                if(!Stage(directory,"prepared",text))return true;
                using var parser=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});var original=XElement.Load(parser);
                if(original.Name!="npcConsumptionOriginal"||!Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation)||Path.GetFileName(directory)!=operation.ToString("N")||
                    Path.GetFullPath((string)original.Attribute("save"))!=Path.GetFullPath(GameIO.GetSaveGameDir()))return true;
                if((string)original.Attribute("npc")!=id.ToString())continue;
                if(PositiveNoEffect(directory,text))continue;
                if(!IsPositiveTerminal(directory,text,original))return true;
            }
            return false;
        }
        catch{return true;}
    }
    private static string Integrity(byte[] bytes){using(var digest=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-",string.Empty);}
    private static bool HistoricalOwner(string directory,string phase,string text)
    {
        try{return ReadHistoricalOwnerBytes(directory,phase,text)!=null;}catch{return false;}
    }
    private static byte[] ReadHistoricalOwnerBytes(string directory,string phase,string text)
    {
        var stage=ReadRecord(Path.Combine(directory,"owner-"+phase+"-saved.xml"));if(!Stage(directory,"owner-"+phase+"-saved",text))throw new InvalidDataException("Original owner stage changed");
        var seal=stage.Element("native");string path=Path.Combine(directory,"owner-"+phase+".native");var file=new FileInfo(path);
        if(seal==null||seal.Attributes().Count()!=2||!file.Exists||(file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<6||file.Length>16*1024*1024||file.Length!=(long?)seal.Attribute("length"))throw new InvalidDataException("Original owner native seal unavailable");
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length<6||input.Length>16L*1024*1024||input.Length!=(long?)seal.Attribute("length"))throw new InvalidDataException("Original owner native size changed");
        var bytes=new byte[checked((int)input.Length)];int offset=0;
        while(offset<bytes.Length){int read=input.Read(bytes,offset,bytes.Length-offset);if(read<1)throw new EndOfStreamException();offset+=read;}
        if(input.ReadByte()!=-1||bytes[0]!='t'||bytes[1]!='t'||bytes[2]!='p'||bytes[3]!=0||bytes[4]!=PlayerDataFile.cFileVersion||Integrity(bytes)!=(string)seal.Attribute("integrity"))throw new InvalidDataException("Original complete owner native image changed");
        return bytes;
    }
    private static void AdvanceHead(Original original,string stage)
    {
        var image=new XDocument(new XElement("npcConsumptionHead",new XAttribute("version",1),new XAttribute("stage",stage),new XElement("original",original.Text)));
        string path=Path.Combine(original.Directory,"control.xml");if(!RebirthAtomicXmlFile.TryWrite(path,image,out var error)||!XNode.DeepEquals(image.Root,ReadRecord(path)))throw new IOException("NPC consumption current head unavailable: "+error);
    }
}
