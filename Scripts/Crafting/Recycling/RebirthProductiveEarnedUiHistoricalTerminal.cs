using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private const string CompletedSeal="terminal.archive-seal";
    // Issued only while the actual original still proves the entire saved
    // terminal chain. The seal grants historical archival, never native work,
    // receipt restoration, queue rotation, wear or owner progression.
    private static bool SealHistoricalTerminal(Original original)
    {
        if(!original.TerminalSaved||!original.Delivered||!original.ViewerSaved||!original.Current()||
            original.PreparationTools!=null||original.PreparationAllocation!=null)return false;
        string directory=Path.GetDirectoryName(original.Path);
        using(var memory=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(memory==null)return false;
            var files=ArchiveFiles(directory);if(files==null||!files.Contains(original.Path+".TerminalSaved",StringComparer.OrdinalIgnoreCase)||
                !files.Contains(original.Path+".Delivered",StringComparer.OrdinalIgnoreCase)||!files.Contains(original.Path+".ViewerSaved",StringComparer.OrdinalIgnoreCase))return false;
            var seal=new XElement("uiHistoricalTerminal",new XAttribute("version",1),new XAttribute("operation",original.Binding.Operation.ToString("N")),
                new XAttribute("world",original.Binding.WorldKey),new XAttribute("owner",original.Binding.Owner),new XAttribute("creation",original.Binding.Creation),
                new XAttribute("uio",original.Digest),files.Select(path=>new XElement("file",new XAttribute("name",Path.GetFileName(path)),
                    new XAttribute("length",new FileInfo(path).Length),new XAttribute("digest",ArchiveDigest(path)))));
            byte[] image=Encoding.UTF8.GetBytes(seal.ToString(SaveOptions.DisableFormatting));
            return image.Length<=65536&&original.Current()&&WriteExact(Path.Combine(directory,CompletedSeal),image)&&original.Current();
        }
    }
    private static string[] ArchiveFiles(string directory)
    {
        if(!Safe(directory)||Directory.EnumerateDirectories(directory).Any())return null;
        var files=Directory.EnumerateFiles(directory).Take(257).OrderBy(path=>Path.GetFileName(path),StringComparer.Ordinal).ToArray();
        if(files.Length>256||files.Any(path=>!Safe(path)||path.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)))return null;
        return files.Where(path=>Path.GetFileName(path)!=CompletedSeal).ToArray();
    }
    private static string ArchiveDigest(string path)
    {
        if(!Safe(path))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var sha=System.Security.Cryptography.SHA256.Create())
        {var value=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(!Safe(path))throw new InvalidDataException();return value;}
    }
    private static bool TryArchiveHistoricalTerminal(string directory,string world,string owner)
    {
        try
        {
            string path=Path.Combine(directory,CompletedSeal),originalPath=Path.Combine(directory,"original.uio");
            if(!Safe(path)||!File.Exists(path)||new FileInfo(path).Length>65536||new FileInfo(path).Length<1||
                !Safe(originalPath)||!File.Exists(originalPath)||new FileInfo(originalPath).Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return false;
            using(var memory=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(memory==null)return false;
                XElement seal;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                    seal=XElement.Load(reader,LoadOptions.None);
                if(seal.Name!="uiHistoricalTerminal"||(int?)seal.Attribute("version")!=1||seal.Attributes().Count()!=6||
                    (string)seal.Attribute("world")!=world||(string)seal.Attribute("owner")!=owner||
                    !Guid.TryParseExact((string)seal.Attribute("operation"),"N",out var operation)||Path.GetFileName(directory)!=operation.ToString("N"))return false;
                var files=ArchiveFiles(directory);var entries=seal.Elements().ToArray();
                if(files==null||entries.Length!=files.Length||entries.Any(value=>value.Name!="file"||value.HasElements||value.Attributes().Count()!=3)||
                    entries.Select(value=>(string)value.Attribute("name")).Distinct(StringComparer.Ordinal).Count()!=entries.Length)return false;
                foreach(var file in files)
                {
                    var entry=entries.SingleOrDefault(value=>(string)value.Attribute("name")==Path.GetFileName(file));
                    if(entry==null||(long?)entry.Attribute("length")!=new FileInfo(file).Length||(string)entry.Attribute("digest")!=ArchiveDigest(file))return false;
                }
                if(!files.Contains(originalPath+".TerminalSaved",StringComparer.OrdinalIgnoreCase)||!files.Contains(originalPath+".Delivered",StringComparer.OrdinalIgnoreCase)||
                    !files.Contains(originalPath+".ViewerSaved",StringComparer.OrdinalIgnoreCase)||ArchiveDigest(originalPath)!=(string)seal.Attribute("uio"))return false;
                var original=RebirthProductiveEarnedUiOriginal.ReadUiArtifact(File.ReadAllBytes(originalPath));
                if(original.Operation!=operation||original.World!=world||original.Owner!=owner||original.Creation!=(string)seal.Attribute("creation"))return false;
                // No native/current-state lookup is used to manufacture a
                // replacement source. Only the positively sealed old directory
                // becomes historical; all its complete evidence is retained.
                string archived=directory+".completed";
                if(!Safe(directory)||!Safe(archived)||Directory.Exists(archived))return false;
                Directory.Move(directory,archived);return true;
            }
        }
        catch{return false;}
    }
}
