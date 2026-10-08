using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static class RebirthPoiClearanceCodec
{
    public const int MaximumCharacters=4194304;
    public static string Write(RebirthPoiClearanceLedger ledger)
    {
        if(ledger==null) throw new ArgumentNullException("ledger");
        var root=new XElement("poiClearance",new XAttribute("version",1),new XAttribute("world",ledger.WorldId.ToString("N")),new XAttribute("revision",ledger.Revision));
        foreach(var record in ledger.Records.Values.OrderBy(r=>r.Identity.Key,StringComparer.Ordinal))
        {
            var p=record.Identity;
            var node=new XElement("poi",new XAttribute("prefab",p.Prefab),new XAttribute("biome",p.Biome),
                new XAttribute("x",p.X),new XAttribute("y",p.Y),new XAttribute("z",p.Z),new XAttribute("rotation",p.Rotation),
                new XAttribute("sx",p.SizeX),new XAttribute("sy",p.SizeY),new XAttribute("sz",p.SizeZ),
                new XAttribute("epoch",record.Epoch),new XAttribute("revision",record.Revision),new XAttribute("state",(int)record.State));
            if(record.Clear!=null) node.Add(Evidence("clear",record.Clear));
            if(record.ResetId!=Guid.Empty)
            {
                var reset=new XElement("reset",new XAttribute("id",record.ResetId.ToString("N")),new XAttribute("before",(int)record.BeforeReset));
                if(record.BeforeResetClear!=null) reset.Add(Evidence("clear",record.BeforeResetClear));
                if(record.ResetPlan!=null)reset.Add(RebirthPoiResetPlan.Write(record.ResetPlan));
                node.Add(reset);
            }
            if(record.LastRepopulation!=null) node.Add(new XElement("repopulation",new XAttribute("id",record.LastRepopulation.GenerationId.ToString("N")),new XAttribute("live",record.LastRepopulation.LiveNativeParticipants),new XAttribute("time",record.LastRepopulation.WorldTime)));
            if(record.LastResetId!=Guid.Empty) node.Add(new XElement("lastReset",new XAttribute("id",record.LastResetId.ToString("N")),new XAttribute("disposition",(int)record.LastResetDisposition)));
            if(record.Observations!=null)node.Add(RebirthPoiPartialObservation.Write(record.Observations));
            if(record.LastAuthoredReset!=null)node.Add(new XElement("authoredReset",new XAttribute("version",1),new XAttribute("epoch",record.LastAuthoredReset.OriginalEpoch),RebirthPoiResetPlan.Write(record.LastAuthoredReset.OriginalPlan),record.LastAuthoredReset.Write()));
            root.Add(node);
        }
        string result=root.ToString(SaveOptions.DisableFormatting);
        if(result.Length>MaximumCharacters) throw new ArgumentException("Ledger serialization exceeds limit.");
        return result;
    }
    private static XElement Evidence(string name,RebirthPoiClearEvidence e)
    {
        return new XElement(name,new XAttribute("id",e.ProofId.ToString("N")),new XAttribute("required",e.RequiredVolumes),
            new XAttribute("verified",e.VerifiedVolumes),new XAttribute("spawned",e.SpawnedParticipants),new XAttribute("time",e.WorldTime));
    }
    public static bool TryRead(string text,Guid expectedWorld,object originalWorldScope,out RebirthPoiClearanceLedger ledger)
    {
        ledger=null;
        if(text==null || text.Length==0 || text.Length>MaximumCharacters || originalWorldScope==null || expectedWorld==Guid.Empty) return false;
        try
        {
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumCharacters,IgnoreComments=false };
            using(var preflight=new StringReader(text)) using(var scan=XmlReader.Create(preflight,settings))
                while(scan.Read()) if(scan.Depth>8) return false;
            XElement root;
            using(var input=new StringReader(text)) using(var reader=XmlReader.Create(input,settings))             {
                XDocument document=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
                root=document.Root;
                foreach(var documentNode in document.Nodes())
                    if(!ReferenceEquals(documentNode,root) && (!(documentNode is XText) || !string.IsNullOrWhiteSpace(((XText)documentNode).Value))) return false;
                if(root==null) return false;
            }
            Shape(root,"poiClearance","version,world,revision","poi");
            if(Number(root,"version")!=1 || Id(root,"world")!=expectedWorld) return false;
            long revision=Number(root,"revision"); if(revision<0) return false;
            var values=new Dictionary<string,RebirthPoiClearanceRecord>(StringComparer.Ordinal);
            foreach(var node in root.Elements())
            {
                if(values.Count>=RebirthPoiClearanceLedger.MaximumRecords) return false;
                Shape(node,"poi","prefab,biome,x,y,z,rotation,sx,sy,sz,epoch,revision,state","clear,reset,lastReset,repopulation,observations,authoredReset");
                var identity=new RebirthPoiIdentity(Text(node,"prefab"),Int(node,"x"),Int(node,"y"),Int(node,"z"),Int(node,"rotation"),Int(node,"sx"),Int(node,"sy"),Int(node,"sz"),Text(node,"biome"));
                // Canonical encoding only: case aliases cannot silently rewrite durable identities.
                if(identity.Prefab!=Text(node,"prefab") || identity.Biome!=Text(node,"biome")) return false;
                XElement clear=Single(node,"clear"),reset=Single(node,"reset"),last=Single(node,"lastReset"),repopulation=Single(node,"repopulation");
                Guid resetId=Guid.Empty,lastId=Guid.Empty;
                var before=RebirthPoiClearanceState.Discovered;
                var disposition=RebirthPoiResetDisposition.None;
                RebirthPoiClearEvidence beforeClear=null;
                if(reset!=null)
                {
                    Shape(reset,"reset","id,before","clear,plan"); resetId=Id(reset,"id"); before=(RebirthPoiClearanceState)Int(reset,"before");
                    XElement saved=Single(reset,"clear"); if(saved!=null) beforeClear=ReadEvidence(saved);
                }
                if(last!=null)
                {
                    Shape(last,"lastReset","id,disposition",""); lastId=Id(last,"id"); disposition=(RebirthPoiResetDisposition)Int(last,"disposition");
                }
                                RebirthPoiRepopulationEvidence repopulationEvidence=null;
                if(repopulation!=null)
                {
                    Shape(repopulation,"repopulation","id,live,time",""); ulong repopulationTime;
                    if(!ulong.TryParse(Text(repopulation,"time"),NumberStyles.None,CultureInfo.InvariantCulture,out repopulationTime)) return false;
                    repopulationEvidence=new RebirthPoiRepopulationEvidence(Id(repopulation,"id"),Int(repopulation,"live"),repopulationTime);
                }
                RebirthPoiAuthoredResetBindings authoredReceipt=null;var authoredNode=Single(node,"authoredReset");
                if(authoredNode!=null){Shape(authoredNode,"authoredReset","version,epoch","plan,bindings");if(Int(authoredNode,"version")!=1)throw new FormatException();var originalPlan=RebirthPoiResetPlan.Read(Single(authoredNode,"plan"));authoredReceipt=RebirthPoiAuthoredResetBindings.Read(Single(authoredNode,"bindings"),originalPlan,Number(authoredNode,"epoch"));}
                var record=new RebirthPoiClearanceRecord(identity,Number(node,"epoch"),Number(node,"revision"),
                    (RebirthPoiClearanceState)Int(node,"state"),clear==null?null:ReadEvidence(clear),resetId,before,beforeClear,lastId,disposition,repopulationEvidence,Single(node,"observations")==null?null:RebirthPoiPartialObservation.Read(Single(node,"observations")),reset==null||Single(reset,"plan")==null?null:RebirthPoiResetPlan.Read(Single(reset,"plan")),authoredReceipt);
                if(values.ContainsKey(identity.Key)) return false;
                values.Add(identity.Key,record);
            }
            ledger=new RebirthPoiClearanceLedger(expectedWorld,originalWorldScope,revision,values); return true;
        }
        catch(ArgumentException) { return false; }
        catch(XmlException) { return false; }
        catch(FormatException) { return false; }
        catch(OverflowException) { return false; }
        catch(InvalidOperationException) { return false; }
    }
    private static RebirthPoiClearEvidence ReadEvidence(XElement node)
    {
        Shape(node,"clear","id,required,verified,spawned,time",""); ulong time;
        if(!ulong.TryParse(Text(node,"time"),NumberStyles.None,CultureInfo.InvariantCulture,out time)) throw new FormatException();
        return new RebirthPoiClearEvidence(Id(node,"id"),Int(node,"required"),Int(node,"verified"),Int(node,"spawned"),0,0,false,time);
    }
    internal static XElement Single(XElement parent,string name)
    {
        var nodes=parent.Elements(name).Take(2).ToArray(); if(nodes.Length>1) throw new FormatException(); return nodes.Length==0?null:nodes[0];
    }
    internal static void Shape(XElement node,string name,string attributes,string children)
    {
        if(node.Name!=name) throw new FormatException();
        var allowed=new HashSet<string>(attributes.Split(','),StringComparer.Ordinal);
        foreach(var a in node.Attributes()) if(a.Name.Namespace!=XNamespace.None || !allowed.Remove(a.Name.LocalName)) throw new FormatException();
        if(allowed.Count!=0 && !(allowed.Count==1 && allowed.Contains(""))) throw new FormatException();
        var childNames=new HashSet<string>(children.Split(','),StringComparer.Ordinal);
        foreach(var n in node.Nodes())
        {
            var e=n as XElement;
            if(e!=null) { if(e.Name.Namespace!=XNamespace.None || !childNames.Contains(e.Name.LocalName)) throw new FormatException(); }
            else if(!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)) throw new FormatException();
        }
    }
    internal static string Text(XElement node,string name) { var a=node.Attribute(name); if(a==null) throw new FormatException(); return a.Value; }
    internal static long Number(XElement node,string name) { long n; if(!long.TryParse(Text(node,name),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out n)) throw new FormatException(); return n; }
    internal static int Int(XElement node,string name) { return checked((int)Number(node,name)); }
    internal static Guid Id(XElement node,string name)
    {
        string text=Text(node,name); Guid id;
        if(!Guid.TryParseExact(text,"N",out id) || id==Guid.Empty || text!=id.ToString("N")) throw new FormatException(); return id;
    }
}

