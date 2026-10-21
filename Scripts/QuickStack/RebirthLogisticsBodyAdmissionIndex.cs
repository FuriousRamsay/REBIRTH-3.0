using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Original-wide invocation authority. Explicit None exists before Header ACK;
// missing/corrupt Ready index is UNKNOWN, never a reason to recreate None.
internal static class RebirthLogisticsBodyAdmissionIndex
{
    private const int MaximumBytes=524288;
    private static string PathFor(string owner,Guid operation)
    {
        if(!Hex(owner,64)||operation==Guid.Empty)throw new InvalidDataException();
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsBodyAdmission"),directory=Path.Combine(root,owner);
        foreach(string candidate in new[]{root,directory})if(Directory.Exists(candidate)&&(new DirectoryInfo(candidate).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        return Path.Combine(directory,operation.ToString("N")+".xml");
    }
    private static XElement Empty(RebirthLogisticsNativeBodyIntent.Command c,string header)=>new XElement("bodyAdmission",new XAttribute("version",1),new XAttribute("operation",c.Operation.ToString("N")),new XAttribute("owner",c.Owner),new XAttribute("world",c.World),new XAttribute("creation",c.Creation),new XAttribute("deaths",c.Deaths),new XAttribute("command",c.Binding),new XAttribute("header",header),new XAttribute("state",0));
    private static bool Hex(string value,int length)=>value?.Length==length&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static XElement Read(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader);
        int state=(int)root.Attribute("state");
        if(root.Name!="bodyAdmission"||(string)root.Attribute("version")!="1"||state<0||state>2||root.Attributes().Count()!=(state==0?9:11)||root.Elements().Count()!=(state==0?0:1)||
            !Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||!Guid.TryParseExact((string)root.Attribute("header"),"N",out var header)||header==Guid.Empty||
            !Hex((string)root.Attribute("owner"),64)||!Hex((string)root.Attribute("world"),32)||!Hex((string)root.Attribute("command"),64)||(int)root.Attribute("deaths")<0||
            !RebirthOriginalActionScope.Validate((string)root.Attribute("owner"),(string)root.Attribute("creation")))throw new InvalidDataException();
        string[] names={"version","operation","owner","world","creation","deaths","command","header","state","nonce","binding"};
        if(root.Attributes().Any(a=>!names.Contains(a.Name.LocalName)||a.Name.Namespace!=XNamespace.None))throw new InvalidDataException();
        if(state!=0)
        {
            if(root.Element("native")==null||root.Element("native").HasAttributes||root.Element("native").HasElements||!Guid.TryParseExact((string)root.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||!Hex((string)root.Attribute("binding"),64))throw new InvalidDataException();
            if(!CanonicalNative(root.Element("native").Value))throw new InvalidDataException();
        }
        return root;
    }
    private static int Base64Digit(char c)
    {if(c>='A'&&c<='Z')return c-'A';if(c>='a'&&c<='z')return c-'a'+26;if(c>='0'&&c<='9')return c-'0'+52;return c=='+'?62:c=='/'?63:-1;}
    private static bool CanonicalNative(string text)
    {
        // Journal/header reads do not allocate a second native binary frame.
        // Actual decode is only inside the caller's charged original workspace.
        if(text==null||text.Length<4||text.Length>349528||text.Length%4!=0)return false;
        int padding=text[text.Length-1]=='='?(text[text.Length-2]=='='?2:1):0;
        int length=text.Length/4*3-padding;if(length<1||length>262144)return false;
        for(int index=0;index<text.Length-padding;index++)if(Base64Digit(text[index])<0)return false;
        if(padding==1&&(Base64Digit(text[text.Length-2])&3)!=0||padding==2&&(Base64Digit(text[text.Length-3])&15)!=0)return false;
        return true;
    }
    private static bool Bound(XElement root,RebirthLogisticsNativeBodyIntent.Command c,string header)
    {var empty=new XElement(root);empty.SetAttributeValue("state",0);empty.Attribute("nonce")?.Remove();empty.Attribute("binding")?.Remove();empty.RemoveNodes();return XNode.DeepEquals(empty,Empty(c,header));}
    private static bool Recover(string path)
    {
        if(File.Exists(path+".bak"))return false;if(!File.Exists(path+".tmp"))return true;if(!File.Exists(path))return false;
        var before=Read(path);var after=Read(path+".tmp");int state=(int)before.Attribute("state"),next=(int)after.Attribute("state");
        var normalized=new XElement(after);normalized.SetAttributeValue("state",state);
        if(state==0&&next==1){normalized.Attribute("nonce")?.Remove();normalized.Attribute("binding")?.Remove();normalized.RemoveNodes();}
        else if(state!=1||next!=2)return false;
        if(!XNode.DeepEquals(before,normalized))return false;
        // Durable Attempted temp is conservatively promoted, never reset.
        File.Replace(path+".tmp",path,null);return XNode.DeepEquals(Read(path),after);
    }
    private static string Existing(string path)
    {
        string archive=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(path)),"archive",Path.GetFileName(Path.GetDirectoryName(path)),Path.GetFileName(path));
        foreach(string directory in new[]{Path.GetDirectoryName(archive),Path.GetDirectoryName(Path.GetDirectoryName(archive))})
            if(Directory.Exists(directory)&&(new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        if(File.Exists(path)&&File.Exists(archive))throw new InvalidDataException();
        return File.Exists(archive)?archive:path;
    }
    private static bool Capacity(int incoming)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsBodyAdmission");if(!Directory.Exists(root))return true;int count=0;long bytes=incoming;
        foreach(string directory in Directory.EnumerateDirectories(root))
        {
            if((new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if(Path.GetFileName(directory)=="archive")continue;
            if(!Hex(Path.GetFileName(directory),64))return false;
            foreach(string path in Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly))
            {
                var value=Read(path);string owner=(string)value.Attribute("owner");Guid operation=Guid.ParseExact((string)value.Attribute("operation"),"N");
                if(Path.GetFileName(path)!=operation.ToString("N")+".xml"||Path.GetFileName(directory)!=owner)return false;
                if(RebirthLogisticsTransferJournal.TryRead(owner,operation,out var terminal)&&terminal.Phase==4&&RebirthLogisticsNativeBodyIntent.Command.From(terminal).Binding==(string)value.Attribute("command"))
                {
                    string archive=Path.Combine(root,"archive",owner,Path.GetFileName(path));if(File.Exists(archive))return false;
                    Directory.CreateDirectory(Path.GetDirectoryName(archive));File.Move(path,archive);continue;
                }
                if(++count>=4096)return false;bytes+=new FileInfo(path).Length;if(bytes>33554432)return false;
            }
        }
        return bytes<=33554432;
    }
    private static bool Publish(string path,XElement root,bool replace)
    {
        if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;byte[] bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumBytes||!Capacity(bytes.Length))return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(replace)File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);return XNode.DeepEquals(root,Read(path));
    }
    internal static bool Initialize(RebirthLogisticsNativeBodyIntent.Command command,string header)
    {
        try
        {
            string path=PathFor(command.Owner,command.Operation);var expected=Empty(command,header);
            // Called ONLY for persisted Initializing Header which has never ACKed.
            if(File.Exists(path+".bak"))return false;
            if(File.Exists(path+".tmp")){if(!XNode.DeepEquals(Read(path+".tmp"),expected)||File.Exists(path))return false;File.Move(path+".tmp",path);}
            return File.Exists(path)?XNode.DeepEquals(Read(path),expected):Publish(path,expected,false);
        }catch{return false;}
    }
    internal static bool Ready(RebirthLogisticsNativeBodyIntent.Command command,string header)
    {try{string path=PathFor(command.Owner,command.Operation);path=Existing(path);return Recover(path)&&Bound(Read(path),command,header);}catch{return false;}}
    internal static bool Prepare(RebirthLogisticsTransferJournal.Record original,string header,string nonce,string native)
    {
        try
        {
            var command=RebirthLogisticsNativeBodyIntent.Command.From(original);string path=PathFor(original.Owner,original.Operation);
            if(!Recover(path))return false;var root=Read(path);if(!Bound(root,command,header))return false;
            if((int)root.Attribute("state")!=0)return (int)root.Attribute("state")==1&&(string)root.Attribute("nonce")==nonce&&(string)root.Attribute("binding")==RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)&&root.Element("native")?.Value==native;
            root.SetAttributeValue("state",1);root.SetAttributeValue("nonce",nonce);root.SetAttributeValue("binding",RebirthLogisticsTransferJournal.ImmutablePlanDigest(original));root.Add(new XElement("native",native));return Publish(path,root,true);
        }catch{return false;}
    }
    internal static bool Attempt(RebirthLogisticsTransferJournal.Record original,string header,string nonce,string originalNative)
    {
        try
        {
            string path=PathFor(original.Owner,original.Operation);if(!Recover(path))return false;var root=Read(path);
            if(!Bound(root,RebirthLogisticsNativeBodyIntent.Command.From(original),header)||(int)root.Attribute("state")!=1||(string)root.Attribute("nonce")!=nonce||root.Element("native")?.Value!=originalNative||(string)root.Attribute("binding")!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original))return false;
            root.SetAttributeValue("state",2);return Publish(path,root,true);
        }catch{return false;}
    }
    internal static bool TryPrepared(RebirthLogisticsTransferJournal.Record original,string header,out string nonce,out string native)
    {
        nonce=null;native=null;try{string path=PathFor(original.Owner,original.Operation);if(!Recover(path))return false;var root=Read(path);if(!Bound(root,RebirthLogisticsNativeBodyIntent.Command.From(original),header)||(int)root.Attribute("state")!=1||(string)root.Attribute("binding")!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original))return false;nonce=(string)root.Attribute("nonce");native=root.Element("native").Value;return true;}catch{return false;}
    }
}
