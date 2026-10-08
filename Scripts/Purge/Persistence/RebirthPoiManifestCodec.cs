using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static class RebirthPoiManifestCodec
{
    public const int MaximumCharacters=2097152;
    public static string Write(RebirthPoiWorldBinding binding,long revision,IEnumerable<RebirthPoiShardReference> references,string predecessorHash=null,string transaction=null)
    {
        var root=new XElement("poiWorld",new XAttribute("version",1),new XAttribute("nativeWorld",binding.NativeWorldId),new XAttribute("revision",revision));
        if(predecessorHash!=null || transaction!=null)
        { if(!LowerHex(predecessorHash,64) || !LowerHex(transaction,32) || revision<1) throw new ArgumentException("Invalid publication receipt."); root.Add(new XAttribute("predecessor",predecessorHash),new XAttribute("transaction",transaction)); }
        foreach(var r in references.OrderBy(r=>r.Id,StringComparer.Ordinal)) root.Add(new XElement("shard",new XAttribute("id",r.Id),new XAttribute("file",r.File),new XAttribute("sha256",r.Hash),new XAttribute("revision",r.Revision),new XAttribute("count",r.Count)));
        string text=root.ToString(SaveOptions.DisableFormatting); if(text.Length>MaximumCharacters) throw new ArgumentException("Manifest limit."); return text;
    }
    public static bool TryRead(string text,RebirthPoiWorldBinding binding,out long revision,out Dictionary<string,RebirthPoiShardReference> references)
    {
        revision=0; references=null;
        return binding!=null && TryRead(text,binding.NativeWorldId,out revision,out references);
    }
    public static bool TryRead(string text,string expectedNativeWorldId,out long revision,out Dictionary<string,RebirthPoiShardReference> references)
    {
        revision=0; references=null;
        Guid expectedGuid;
        if(expectedNativeWorldId==null || expectedNativeWorldId.Length!=32 || !Guid.TryParseExact(expectedNativeWorldId,"N",out expectedGuid) || expectedGuid==Guid.Empty ||
            !string.Equals(expectedGuid.ToString("N").ToUpperInvariant(),expectedNativeWorldId,StringComparison.Ordinal) || text==null || text.Length>MaximumCharacters) return false;
        try
        {
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumCharacters };
            using(var input=new StringReader(text)) using(var scan=XmlReader.Create(input,settings)) while(scan.Read()) if(scan.Depth>3) return false;
            XDocument doc; using(var input=new StringReader(text)) using(var reader=XmlReader.Create(input,settings)) doc=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
            var root=doc.Root;
            foreach(var node in doc.Nodes()) if(!ReferenceEquals(node,root) && (!(node is XText) || !string.IsNullOrWhiteSpace(((XText)node).Value))) return false;
            if(root==null || root.Name!="poiWorld" || (root.Attributes().Count()!=3 && root.Attributes().Count()!=5) || root.Attribute("version")?.Value!="1" || root.Attribute("nativeWorld")?.Value!=expectedNativeWorldId ||
                !long.TryParse(root.Attribute("revision")?.Value,NumberStyles.None,CultureInfo.InvariantCulture,out revision)) return false;
            if(root.Attributes().Count()==5 && (!LowerHex(root.Attribute("predecessor")?.Value,64) || !LowerHex(root.Attribute("transaction")?.Value,32) || revision<1)) return false;
            var parsed=new Dictionary<string,RebirthPoiShardReference>(StringComparer.Ordinal); var files=new HashSet<string>(StringComparer.Ordinal);
            foreach(var node in root.Nodes())
            {
                if(node is XText && string.IsNullOrWhiteSpace(((XText)node).Value)) continue;
                var e=node as XElement; long r; int count;
                if(e==null || e.Name!="shard" || e.Attributes().Count()!=5 || e.Nodes().Any() || parsed.Count>=RebirthPoiWorldSnapshot.MaximumShards) return false;
                string id=e.Attribute("id")?.Value,file=e.Attribute("file")?.Value,hash=e.Attribute("sha256")?.Value;
                if(!LowerHex(id,32) || !LowerHex(hash,64) || !ValidFile(file) || file.Substring(6,32)!=id || !files.Add(file) || parsed.ContainsKey(id) ||
                    !long.TryParse(e.Attribute("revision")?.Value,NumberStyles.None,CultureInfo.InvariantCulture,out r) || r<1 || r>revision ||
                    !int.TryParse(e.Attribute("count")?.Value,NumberStyles.None,CultureInfo.InvariantCulture,out count) || count<1 || count>RebirthPoiWorldSnapshot.RecordsPerShard) return false;
                parsed.Add(id,new RebirthPoiShardReference(id,file,hash,r,count));
            }
            if(revision==0 && parsed.Count!=0 || revision>0 && (parsed.Count==0 || parsed.Values.Max(r=>r.Revision)!=revision)) return false;
            references=parsed; return true;
        }
        catch(ArgumentException) { return false; } catch(XmlException) { return false; } catch(InvalidOperationException) { return false; }
    }


    internal static bool IsSuccessorOf(string candidate,string original)
    {
        try { return XElement.Parse(candidate).Attribute("predecessor")?.Value==RebirthPoiWorldStore.ContentHash(original); }
        catch { return false; }
    }    internal static bool LowerHex(string value,int length)
    { if(value==null || value.Length!=length) return false; foreach(char c in value) if(!(c>='0' && c<='9') && !(c>='a' && c<='f')) return false; return true; }
    private static bool ValidFile(string file)
    { return file!=null && file.Length==75 && file.StartsWith("shard-",StringComparison.Ordinal) && file[38]=='-' && file.EndsWith(".xml",StringComparison.Ordinal) && LowerHex(file.Substring(6,32),32) && LowerHex(file.Substring(39,32),32); }
}
