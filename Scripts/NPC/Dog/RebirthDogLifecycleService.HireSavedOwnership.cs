using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthDogLifecycleService
{
    private static bool HireSameFiles(string left,string right)
    {
        RequireHirePath(Path.GetDirectoryName(left));RequireHirePath(Path.GetDirectoryName(right));
        var a=new FileInfo(left);var b=new FileInfo(right);
        if(!a.Exists||!b.Exists||a.Length!=b.Length||a.Length>16*1024*1024||(a.Attributes&FileAttributes.ReparsePoint)!=0||(b.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        using(var x=new FileStream(left,FileMode.Open,FileAccess.Read,FileShare.Read))using(var y=new FileStream(right,FileMode.Open,FileAccess.Read,FileShare.Read))
        {var first=new byte[65536];var second=new byte[65536];int read;while((read=x.Read(first,0,first.Length))>0){int total=0;while(total<read){int n=y.Read(second,total,read-total);if(n<1)return false;total+=n;}for(int i=0;i<read;i++)if(first[i]!=second[i])return false;}return y.ReadByte()==-1;}
    }
    private static bool ArchiveHireOwnership(HireSettlement original,string phase)
    {
        if(!HireCurrent(original)||!RebirthNpcPersistenceCoordinator.LastSaveSucceeded)return false;
        string directory=Path.Combine(original.Directory,phase);RequireHirePath(directory);Directory.CreateDirectory(directory);
        var manifest=new XElement("dogHireSavedOwnership",new XAttribute("version",2),new XElement("original",original.Original));long bytes=0;
        foreach(string name in RebirthNpcPersistenceTimelineService.GetSidecarNames())
        {
            if(Path.GetFileName(name)!=name)return false;string source=Path.Combine(original.Save,name),destination=Path.Combine(directory,name);bool exists=File.Exists(source);
            var member=new XElement("file",new XAttribute("name",name),new XAttribute("exists",exists));manifest.Add(member);
            if(!exists)continue;
            RequireHirePath(Path.GetDirectoryName(source));var info=new FileInfo(source);bytes=checked(bytes+info.Length);
            if(info.Length>16*1024*1024||bytes>32*1024*1024||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if(!File.Exists(destination))using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)){input.CopyTo(output,65536);output.Flush(true);}
            if(!HireSameFiles(source,destination))return false;
            member.Add(new XAttribute("length",info.Length),new XAttribute("integrity",HireArchiveIntegrity(destination)));
        }
        string path=Path.Combine(directory,"manifest.xml");byte[] payload=Encoding.UTF8.GetBytes(manifest.ToString(SaveOptions.DisableFormatting));
        if(File.Exists(path)){if(!File.ReadAllBytes(path).SequenceEqual(payload))return false;}
        else using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(payload,0,payload.Length);output.Flush(true);}
        return HireCurrent(original)&&File.ReadAllBytes(path).SequenceEqual(payload);
    }
    private static string HireArchiveIntegrity(string path)
    {
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var digest=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(digest.ComputeHash(input)).Replace("-",string.Empty);
    }
    private static bool HasHireHistoricalOwnershipSeal(string directory,string phase,string original)
    {
        directory=Path.Combine(directory,phase);var manifest=ReadHireXml(Path.Combine(directory,"manifest.xml"));var names=RebirthNpcPersistenceTimelineService.GetSidecarNames();var files=manifest.Elements("file").ToArray();
        if(manifest.Name!="dogHireSavedOwnership"||manifest.Attributes().Count()!=1||(int?)manifest.Attribute("version")!=2||manifest.Elements().Count()!=files.Length+1||(string)manifest.Element("original")!=original||
            files.Length!=names.Length||files.Select(row=>(string)row.Attribute("name")).Distinct(StringComparer.Ordinal).Count()!=names.Length)return false;
        long total=0;
        foreach(var row in files)
        {
            string name=(string)row.Attribute("name");if(!names.Contains(name)||Path.GetFileName(name)!=name||row.HasElements)return false;
            string path=Path.Combine(directory,name);bool exists=(bool)row.Attribute("exists");if(!exists){if(row.Attributes().Count()!=2||File.Exists(path))return false;continue;}
            var file=new FileInfo(path);if(row.Attributes().Count()!=4||!file.Exists||(file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||file.Length>16*1024*1024||file.Length!=(long?)row.Attribute("length")||(total=checked(total+file.Length))>32*1024*1024)return false;
            // Integrity accompanies the retained complete native saved files; it
            // does not grant any new owner/order/debit/refund mutation.
            if(HireArchiveIntegrity(path)!=(string)row.Attribute("integrity"))return false;
        }
        return true;
    }
    private static XElement ReadHireXml(string path)
    {
        RequireHirePath(Path.GetDirectoryName(path));var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>1048576||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Dog hire record unavailable");
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))return XElement.Load(reader);
    }
    private static bool HasHireStage(string directory,string stage,string original)
    {
        var node=ReadHireXml(Path.Combine(directory,stage+".xml"));return node.Name=="dogHireStage"&&node.Attributes().Count()==2&&(int?)node.Attribute("version")==1&&(string)node.Attribute("stage")==stage&&node.Elements().Count()==1&&(string)node.Element("original")==original;
    }
    private static bool MatchesHireSavedOwnership(string directory,string phase,string original,string save)
    {
        directory=Path.Combine(directory,phase);var manifest=ReadHireXml(Path.Combine(directory,"manifest.xml"));var files=manifest.Elements("file").ToArray();var names=RebirthNpcPersistenceTimelineService.GetSidecarNames();
        int version=(int?)manifest.Attribute("version")??0;
        if(manifest.Name!="dogHireSavedOwnership"||(version!=1&&version!=2)||(string)manifest.Element("original")!=original||files.Length!=names.Length||files.Select(f=>(string)f.Attribute("name")).Distinct(StringComparer.Ordinal).Count()!=names.Length)return false;
        foreach(var file in files)
        {
            string name=(string)file.Attribute("name");bool exists=(bool)file.Attribute("exists");if(!names.Contains(name)||file.Attributes().Count()!=(version==2&&exists?4:2)||file.HasElements)return false;
            string live=Path.Combine(save,name),captured=Path.Combine(directory,name);
            if(exists?!HireSameFiles(live,captured):File.Exists(live)||File.Exists(captured))return false;
        }
        return true;
    }
}
