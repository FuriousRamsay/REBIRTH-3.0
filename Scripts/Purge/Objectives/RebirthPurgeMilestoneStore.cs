using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Historical achievement custody is separate from current POI clearance. Receipts
// survive a reset; they are not native item/airdrop delivery assertions.
internal sealed class RebirthPurgeMilestoneStore
{
    internal sealed class Receipt
    {
        internal readonly string Biome,Id;internal readonly int Tier;
        internal string Key=>Biome.Length.ToString(CultureInfo.InvariantCulture)+":"+Biome+":"+Id;
        internal readonly int Percent,Eligible,Cleared;
        internal readonly long Revision,Generation;
        internal Receipt(string biome,int percent,int eligible,int cleared,long revision,long generation,string id="completion",int tier=-1)
        {
            if(RebirthPoiIdentity.Canonical(biome,128)!=biome || percent<1 || percent>100 || eligible<1 || eligible>200000 || cleared<RebirthPurgeObjectivePolicy.Required(eligible,percent) || cleared>eligible || revision<1 || generation<1)throw new ArgumentException("Invalid milestone receipt.");
            if(id!="completion")new RebirthPurgeDiscoveryPolicy.Definition(id,percent,tier);else if(tier!=-1)throw new ArgumentException("Invalid completion tier.");
            Id=id;Tier=tier;Biome=biome;Percent=percent;Eligible=eligible;Cleared=cleared;Revision=revision;Generation=generation;
        }
    }
    private readonly RebirthPoiWorldBinding binding;
    private readonly string directory,path;
    private readonly Action<string> fault;
    private string original;
    private Dictionary<string,Receipt> receipts=new Dictionary<string,Receipt>(StringComparer.Ordinal);
    private IReadOnlyDictionary<string,Receipt> publishedAll=new ReadOnlyDictionary<string,Receipt>(new Dictionary<string,Receipt>());
    private IReadOnlyDictionary<string,Receipt> publishedCompletions=new ReadOnlyDictionary<string,Receipt>(new Dictionary<string,Receipt>());
    internal IReadOnlyDictionary<string,Receipt> Published => binding.IsCurrent?publishedCompletions:null;
    internal IReadOnlyDictionary<string,Receipt> PublishedAll => binding.IsCurrent?publishedAll:null;
    private void SetValues(Dictionary<string,Receipt> values){receipts=values;publishedAll=new ReadOnlyDictionary<string,Receipt>(values);publishedCompletions=new ReadOnlyDictionary<string,Receipt>(values.Values.Where(r=>r.Id=="completion").ToDictionary(r=>r.Biome,StringComparer.Ordinal));}
    internal RebirthPurgeMilestoneStore(RebirthPoiWorldBinding scope,Action<string> inject=null)
    {
        binding=scope??throw new ArgumentNullException(nameof(scope));fault=inject;
        directory=Path.Combine(binding.SaveDirectory,"RebirthData","Purge","Milestones");path=Path.Combine(directory,"world.xml");
    }
    private bool Safe()
    {
        string at=directory;
        while(!string.IsNullOrEmpty(at))
        {
            if(Directory.Exists(at) && (File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)return false;
            string parent=Path.GetDirectoryName(at);if(parent==at)break;at=parent;
        }
        foreach(string file in new[]{path,path+".candidate",Path.Combine(directory,"writer.lock")})
            if(File.Exists(file) && (File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;
        return binding.IsCurrent;
    }
    private FileStream Lease()=>new FileStream(Path.Combine(directory,"writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    private static string Hash(string value)
    { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
    private static string Read(string file)
    { if(new FileInfo(file).Length>524288)throw new InvalidDataException("Oversized milestone file.");return File.ReadAllText(file,new UTF8Encoding(false,true)); }
    private string Encode(Dictionary<string,Receipt> values,string predecessor)
    {
        var root=new XElement("purge_milestones",new XAttribute("version",values.Values.Any(v=>v.Id!="completion")?2:1),new XAttribute("world",binding.NativeWorldId),new XAttribute("predecessor",predecessor));
        foreach(var r in values.Values.OrderBy(v=>v.Biome,StringComparer.Ordinal).ThenBy(v=>v.Id,StringComparer.Ordinal))
        {
            var node=new XElement(r.Id=="completion"?"completion":"discovery",new XAttribute("biome",r.Biome),new XAttribute("percent",r.Percent),new XAttribute("eligible",r.Eligible),new XAttribute("cleared",r.Cleared),new XAttribute("revision",r.Revision),new XAttribute("generation",r.Generation));
            if(r.Id!="completion")node.Add(new XAttribute("id",r.Id),new XAttribute("tier",r.Tier));root.Add(node);
        }
        return root.ToString(SaveOptions.DisableFormatting);
    }
    private bool Decode(string text,out Dictionary<string,Receipt> values,out string predecessor)
    {
        values=null;predecessor=null;
        try
        {
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=524288}))
            {
                var root=XElement.Load(reader);
                if(root.Name!="purge_milestones" || root.Attributes().Count()!=3 || ((string)root.Attribute("version")!="1"&&(string)root.Attribute("version")!="2") || (string)root.Attribute("world")!=binding.NativeWorldId)return false;
                predecessor=(string)root.Attribute("predecessor");if(predecessor!= "" && !RebirthPoiManifestCodec.LowerHex(predecessor,64))return false;
                var parsed=new Dictionary<string,Receipt>(StringComparer.Ordinal);
                foreach(var node in root.Elements())
                {
                    bool discovery=node.Name=="discovery";if(node.Name!="completion"&&!discovery||discovery&&(string)root.Attribute("version")!="2"||node.HasElements||parsed.Count>=1024)return false;
                    RebirthPoiClearanceCodec.Shape(node,discovery?"discovery":"completion",discovery?"biome,percent,eligible,cleared,revision,generation,id,tier":"biome,percent,eligible,cleared,revision,generation","");
                    string biome=(string)node.Attribute("biome");
                    var r=new Receipt(biome,int.Parse((string)node.Attribute("percent"),CultureInfo.InvariantCulture),int.Parse((string)node.Attribute("eligible"),CultureInfo.InvariantCulture),int.Parse((string)node.Attribute("cleared"),CultureInfo.InvariantCulture),long.Parse((string)node.Attribute("revision"),CultureInfo.InvariantCulture),long.Parse((string)node.Attribute("generation"),CultureInfo.InvariantCulture),discovery?(string)node.Attribute("id"):"completion",discovery?int.Parse((string)node.Attribute("tier"),CultureInfo.InvariantCulture):-1);
                    parsed.Add(r.Key,r);if(parsed.Values.Select(v=>v.Biome).Distinct(StringComparer.Ordinal).Count()>64)return false;
                }
                values=parsed;return true;
            }
        }
        catch(ArgumentException){return false;}catch(XmlException){return false;}catch(FormatException){return false;}catch(OverflowException){return false;}
    }
    private void PublishCandidate(string candidate)
    {
        if(!Safe())throw new InvalidOperationException("Replaced milestone world.");
        string now=File.Exists(path)?Read(path):null;
        if(now!=original)throw new InvalidOperationException("Changed milestone predecessor.");
        Dictionary<string,Receipt> values=null;string previous;
        if(!Decode(candidate,out values,out previous) || previous!=(original==null?"":Hash(original)))throw new InvalidDataException("Invalid original milestone candidate.");
        if(original!=null)
        {
            Dictionary<string,Receipt> before;string ignored;
            if(!Decode(original,out before,out ignored) || before.Any(p=>!values.ContainsKey(p.Key) || !Same(p.Value,values[p.Key])))throw new InvalidDataException("Milestone history cannot regress.");
            File.Replace(path+".candidate",path,null);
        }
        else File.Move(path+".candidate",path);
        fault?.Invoke("afterPublished");
        if(!Safe() || Read(path)!=candidate)throw new IOException("Unconfirmed milestone final.");
        original=candidate;SetValues(values);
    }
    private static bool Same(Receipt a,Receipt b)=>a.Biome==b.Biome&&a.Percent==b.Percent&&a.Eligible==b.Eligible&&a.Cleared==b.Cleared&&a.Revision==b.Revision&&a.Generation==b.Generation&&a.Id==b.Id&&a.Tier==b.Tier;
    internal bool TryOpen()
    {
        try
        {
            if(!Safe())return false;Directory.CreateDirectory(directory);if(!Safe())return false;
            using(var lease=Lease())
            {
                string final=File.Exists(path)?Read(path):null;
                Dictionary<string,Receipt> values=null;string previous;
                if(final!=null && !Decode(final,out values,out previous))return false;
                original=final;SetValues(final==null?new Dictionary<string,Receipt>(StringComparer.Ordinal):values);
                if(File.Exists(path+".candidate"))PublishCandidate(Read(path+".candidate"));
                return binding.IsCurrent;
            }
        }
        catch(Exception){return false;}
    }
    internal bool TryRecord(IEnumerable<Receipt> earned)
    {
        try
        {
            if(earned==null || !Safe())return false;
            using(var lease=Lease())
            {
                string final=File.Exists(path)?Read(path):null;
                // Lost final-write response can be reconciled only by a fresh open.
                if(final!=original)return false;
                if(File.Exists(path+".candidate")){PublishCandidate(Read(path+".candidate"));return true;}
                var candidateValues=new Dictionary<string,Receipt>(receipts,StringComparer.Ordinal);
                foreach(var receipt in earned)
                { if(receipt==null)return false;if(!candidateValues.ContainsKey(receipt.Key))candidateValues.Add(receipt.Key,receipt);if(candidateValues.Count>1024||candidateValues.Values.Select(v=>v.Biome).Distinct(StringComparer.Ordinal).Count()>64)return false; }
                if(candidateValues.Count==receipts.Count)return true;
                string candidate=Encode(candidateValues,original==null?"":Hash(original));
                using(var stream=new FileStream(path+".candidate",FileMode.CreateNew,FileAccess.Write,FileShare.None))
                { byte[] bytes=new UTF8Encoding(false,true).GetBytes(candidate);stream.Write(bytes,0,bytes.Length);stream.Flush(true); }
                fault?.Invoke("afterCandidate");PublishCandidate(candidate);return true;
            }
        }
        catch(Exception){return false;}
    }
}