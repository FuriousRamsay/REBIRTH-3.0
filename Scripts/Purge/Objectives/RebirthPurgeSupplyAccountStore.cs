using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Save-scoped atomic receipts. Retained candidates are reconciled, never overwritten.
internal sealed class RebirthPurgeSupplyAccountStore
{
    private readonly RebirthPoiWorldBinding binding;
    private readonly string directory,path;
    private readonly Action<string> fault;
    private string original;
    private IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> published;
    internal IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> Published=>binding.IsCurrent?published:null;
    internal RebirthPurgeSupplyAccountStore(RebirthPoiWorldBinding binding,Action<string> fault=null)
    {
        this.binding=binding??throw new ArgumentNullException(nameof(binding));this.fault=fault;
        directory=Path.Combine(binding.SaveDirectory,"RebirthData","Purge","Supplies");path=Path.Combine(directory,"world.xml");
    }
    private bool Safe()
    {
        if(!binding.IsCurrent)return false;
        for(string at=directory;!string.IsNullOrEmpty(at);at=Path.GetDirectoryName(at))
            if(Directory.Exists(at)&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)return false;
        foreach(string file in new[]{path,path+".candidate",Path.Combine(directory,"writer.lock")})
            if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;
        return true;
    }
    private FileStream Lease()=>new FileStream(Path.Combine(directory,"writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    private static string Read(string path){if(new FileInfo(path).Length>4194304)throw new InvalidDataException();return File.ReadAllText(path,new UTF8Encoding(false,true));}
    private string Encode(Dictionary<string,RebirthPurgeSupplyAccount> accounts,string predecessor)
    {
        var root=new XElement("purge_supplies",new XAttribute("version",1),new XAttribute("world",binding.NativeWorldId),new XAttribute("predecessor",predecessor),
            accounts.Values.OrderBy(a=>a.Player,StringComparer.Ordinal).Select(a=>a.Write()));
        string text=root.ToString(SaveOptions.DisableFormatting);if(text.Length>1048576)throw new InvalidDataException();return text;
    }
    private Dictionary<string,RebirthPurgeSupplyAccount> Decode(string text,out string predecessor)
    {
        using(var input=new StringReader(text))
        using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))
        {
            var document=XDocument.Load(reader,LoadOptions.PreserveWhitespace);var root=document.Root;
            foreach(var item in document.Nodes())if(!ReferenceEquals(item,root)&&(!(item is XText)||!string.IsNullOrWhiteSpace(((XText)item).Value)))throw new FormatException();
            RebirthPoiClearanceCodec.Shape(root,"purge_supplies","version,world,predecessor","account");
            predecessor=RebirthPoiClearanceCodec.Text(root,"predecessor");
            if(RebirthPoiClearanceCodec.Int(root,"version")!=1||RebirthPoiClearanceCodec.Text(root,"world")!=binding.NativeWorldId||predecessor!=""&&!RebirthPoiSupplyCredits.ValidKey(predecessor))throw new FormatException();
            var values=new Dictionary<string,RebirthPurgeSupplyAccount>(StringComparer.Ordinal);
            foreach(var node in root.Elements()){var account=RebirthPurgeSupplyAccount.Read(node);if(values.Count>=16384||values.ContainsKey(account.Player))throw new FormatException();values.Add(account.Player,account);}
            return values;
        }
    }
    private bool FinishCandidate()
    {
        if(!Safe())return false;
        string candidate=Read(path+".candidate"),predecessor;
        var values=Decode(candidate,out predecessor);
        if(predecessor!=(original==null?"":RebirthPoiWorldStore.ContentHash(original))||(File.Exists(path)?Read(path):null)!=original)return false;
        if(published!=null&&published.Any(p=>!values.ContainsKey(p.Key)||!values[p.Key].IsSuccessorOf(p.Value)))return false;
        if(!Safe())return false;
        if(File.Exists(path))File.Replace(path+".candidate",path,null);else File.Move(path+".candidate",path);
        fault?.Invoke("afterPublished");
        if(!Safe()||Read(path)!=candidate)return false;
        original=candidate;published=new ReadOnlyDictionary<string,RebirthPurgeSupplyAccount>(values);return true;
    }
    internal bool TryOpen()
    {
        try
        {
            if(!Safe())return false;Directory.CreateDirectory(directory);if(!Safe())return false;
            using(var lease=Lease())
            {
                original=File.Exists(path)?Read(path):null;string ignored;
                var values=original==null?new Dictionary<string,RebirthPurgeSupplyAccount>(StringComparer.Ordinal):Decode(original,out ignored);
                published=new ReadOnlyDictionary<string,RebirthPurgeSupplyAccount>(values);
                if(File.Exists(path+".candidate")&&!FinishCandidate()){published=null;return false;}
                return Safe();
            }
        }
        catch{published=null;return false;}
    }
    internal bool TryCommit(IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected,RebirthPurgeSupplyAccount next)
    {
        try
        {
            if(!Safe()||published==null||!ReferenceEquals(expected,published)||next==null)return false;
            using(var lease=Lease())
            {
                if((File.Exists(path)?Read(path):null)!=original)return false;
                if(File.Exists(path+".candidate"))
                {
                    // Reconcile the original candidate, but acknowledge only the
                    // exact requested operation before callers perform native effects.
                    if(!FinishCandidate())return false;
                    RebirthPurgeSupplyAccount recovered;
                    return published.TryGetValue(next.Player,out recovered)&&
                        recovered.Write().ToString(SaveOptions.DisableFormatting)==next.Write().ToString(SaveOptions.DisableFormatting);
                }
                RebirthPurgeSupplyAccount old;published.TryGetValue(next.Player,out old);
                if(!next.IsSuccessorOf(old))return false;
                if(old!=null&&old.Write().ToString(SaveOptions.DisableFormatting)==next.Write().ToString(SaveOptions.DisableFormatting))return true;
                var values=published.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);values[next.Player]=next;if(values.Count>16384)return false;
                string candidate=Encode(values,original==null?"":RebirthPoiWorldStore.ContentHash(original));
                using(var stream=new FileStream(path+".candidate",FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {byte[] bytes=new UTF8Encoding(false,true).GetBytes(candidate);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                fault?.Invoke("afterCandidate");return FinishCandidate();
            }
        }
        catch{return false;}
    }
}