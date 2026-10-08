using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

// Immutable composed reconstruction evidence; never grants custody or publishes an actor.
internal sealed class RebirthNpcNativeReconstruction
{
    internal const int MaximumBlobBytes = 1048576;
    internal const int MaximumSlots = 4096;
    private readonly XElement image;
    internal bool HasNativeBodyPolicy=>image.Element("nativeBodyPolicy")!=null;
    internal RebirthNpcNativeBodyPolicy BodyPolicy{get{RebirthNpcNativeBodyPolicy.TryRead(image.Element("nativeBodyPolicy"),out var policy);return policy;}}
    internal bool HasNativeHand => image.Element("nativeHand")!=null;
    internal RebirthNpcNativeHand Hand {get{RebirthNpcNativeHand.TryRead(image.Element("nativeHand"),out var hand);return hand;}}
    internal bool HasNativeGeometry => image.Element("nativeGeometry")!=null;
    internal RebirthNpcNativeGeometry Geometry {get{RebirthNpcNativeGeometry.TryRead(image.Element("nativeGeometry"),out var geometry);return geometry;}}
    internal bool HasNativeHeader => image.Element("nativeHeader")!=null;
    internal RebirthNpcNativeHeader Header {get{RebirthNpcNativeHeader.TryRead(image.Element("nativeHeader"),out var header);return header;}}
    internal string Owner => (string)image.Attribute("owner");
    internal string Profile => (string)image.Attribute("profile");
    internal uint Generation => (uint)image.Attribute("generation");
    internal uint InventoryRevision => (uint)image.Attribute("inventoryRevision");
    internal string NativeAssemblyHash => (string)image.Attribute("assembly");
    private RebirthNpcNativeReconstruction(XElement node) { image = new XElement(node); }
    internal XElement Write() => new XElement(image);
    internal bool Matches(RebirthNpcPersistentRecord person) => person?.Identity != null &&
        person.Profile != null && person.Presence != null && Owner == person.Identity.StableNpcId.ToString() &&
        Profile == person.Profile.ProfileId && Generation == person.Presence.EmbodimentGeneration;

    // Every occupied native slot must refer to exactly one existing canonical custody stack.
    // Unprojected owned stacks may remain in canonical custody; no new stack IDs are created here.
    internal bool TryValidateCustody(RebirthNpcNativeStackSet custody) => TryValidateCustody(custody,custody?.Revision??0);
    internal bool TryValidateCustody(RebirthNpcNativeStackSet custody,uint domainRevision)
    {
        var slots = image.Element("slots").Elements("slot").ToArray();
        if(InventoryRevision != domainRevision) return false;
        if(custody == null) return slots.Length == 0;
        if(custody.Owner != Owner || custody.Revision != InventoryRevision) return false;
        var records = custody.Write().Elements("nativeStack").ToDictionary(n => (string)n.Attribute("id"), StringComparer.Ordinal);
        foreach(var slot in slots)
        {
            if(!records.TryGetValue((string)slot.Attribute("stack"), out var record) ||
                (string)record.Attribute("payload") != (string)slot.Attribute("payload") ||
                (string)record.Attribute("count") != (string)slot.Attribute("count")) return false;
        }
        return true;
    }

    internal bool TryValidateControllers(RebirthNpcControllerFragmentSet fragments, Func<string,int,bool> supports)
    {
        var stored=image.Element("controllers").Elements("fragment").ToArray();
        var source=fragments?.Fragments ?? new RebirthNpcControllerFragment[0];
        if(source.Length != stored.Length || (fragments != null && fragments.FragmentCount != source.Length)) return false;
        var types=new HashSet<string>(StringComparer.Ordinal);
        foreach(var fragment in source)
        {
            if(fragment==null || !types.Add(fragment.ControllerTypeId??"") || fragment.Bytes==null ||
                fragment.FragmentLength!=fragment.Bytes.Length || fragment.Bytes.Length==0 || fragment.Bytes.Length>MaximumBlobBytes ||
                fragment.FragmentVersion<=0 || Digest(fragment.Bytes)!=fragment.FragmentChecksum) return false;
            var entry=stored.SingleOrDefault(n=>(string)n.Attribute("type")==fragment.ControllerTypeId);
            if(entry==null || (string)entry.Attribute("version")!=fragment.FragmentVersion.ToString(CultureInfo.InvariantCulture) ||
                (string)entry.Attribute("required")!=(fragment.Required?"1":"0") ||
                (string)entry.Attribute("payload")!=Convert.ToBase64String(fragment.Bytes) ||
                (string)entry.Attribute("sha256")!=fragment.FragmentChecksum ||
                (fragment.Required && (supports==null || !supports(fragment.ControllerTypeId,fragment.FragmentVersion)))) return false;
        }
        return true;
    }
    internal static bool TryRead(XElement node, out RebirthNpcNativeReconstruction value)
    {
        value = null;
        if(!Shape(node,"nativeReconstruction",9) || ((string)node.Attribute("version") != "1" && (string)node.Attribute("version") != "2" && (string)node.Attribute("version") != "3" && (string)node.Attribute("version") != "4" && (string)node.Attribute("version") != "5" && (string)node.Attribute("version") != "6") ||
            (string)node.Attribute("required") != "1" || (string)node.Attribute("nativeVersion") != "38" ||
            !RebirthNpcStableId.TryParse((string)node.Attribute("owner"),out var owner) || owner.IsEmpty ||
            owner.ToString() != (string)node.Attribute("owner") || !Identifier((string)node.Attribute("profile")) ||
            !UInt((string)node.Attribute("generation"),out var generation) || generation == 0 ||
            !UInt((string)node.Attribute("inventoryRevision"),out _) || !Hash((string)node.Attribute("assembly")) ||
            (string)node.Attribute("complete") != "1") return false;
        if(node.DescendantsAndSelf().Sum(n => (long)n.Attributes().Sum(a => a.Value.Length)) > 8L*1048576) return false;
        var children=node.Elements().ToArray();
        string[] names={"actor","bodyDamage","stats","buffs","toolbelt","equipment","bag","slots","controllers"};
        bool withDynamics=(string)node.Attribute("version")=="6";
        bool withPolicy=withDynamics||(string)node.Attribute("version")=="5";
        bool withHand=withPolicy||(string)node.Attribute("version")=="4";
        bool withGeometry=withHand||(string)node.Attribute("version")=="3";
        bool withHeader=(string)node.Attribute("version")!="1";
        if(children.Length!=names.Length+(withHeader?1:0)+(withGeometry?1:0)+(withHand?1:0)+(withPolicy?1:0)||
            children.Take(names.Length).Where((n,i)=>n.Name!=names[i]).Any()||
            withHeader&&!RebirthNpcNativeHeader.TryRead(children[names.Length],out _)||
            withGeometry&&!RebirthNpcNativeGeometry.TryRead(children[names.Length+1],out _)||
            withPolicy&&(!RebirthNpcNativeBodyPolicy.TryRead(children[names.Length+3],out var policy)||withDynamics&&!policy.HasDynamics)||
            withHand&&(!RebirthNpcNativeHand.TryRead(children[names.Length+2],out var hand)||!hand.Matches(children[7],RebirthNpcNativeHeaderRead(children[names.Length]))))return false;
        for(int i=0;i<7;i++)
            if(!Shape(children[i],names[i],2) || children[i].HasElements ||
                !Blob((string)children[i].Attribute("payload"),(string)children[i].Attribute("sha256"))) return false;
        var slotsNode=children[7];
        if(!Shape(slotsNode,"slots",0)) return false;
        var positions=new HashSet<string>(StringComparer.Ordinal); var stackIds=new HashSet<Guid>(); int slotCount=0;
        foreach(var slot in slotsNode.Elements())
        {
            string area=(string)slot.Attribute("area"), index=(string)slot.Attribute("index");
            if(++slotCount > MaximumSlots || !Shape(slot,"slot",6) || slot.HasElements ||
                (area!="toolbelt" && area!="equipment" && area!="bag") || !UInt(index,out var slotIndex) || slotIndex>=MaximumSlots ||
                !positions.Add(area+":"+index) || !Guid.TryParseExact((string)slot.Attribute("stack"),"N",out var id) ||
                id==Guid.Empty || !stackIds.Add(id) || !UInt((string)slot.Attribute("count"),out var count) || count==0 || count>int.MaxValue ||
                !Blob((string)slot.Attribute("payload"),(string)slot.Attribute("sha256"))) return false;
        }
        var controllers=children[8]; if(!Shape(controllers,"controllers",0)) return false;
        var types=new HashSet<string>(StringComparer.Ordinal); int fragments=0;
        foreach(var f in controllers.Elements())
        {
            if(++fragments>128 || !Shape(f,"fragment",5) || f.HasElements || !Identifier((string)f.Attribute("type")) ||
                !types.Add((string)f.Attribute("type")) || !UInt((string)f.Attribute("version"),out var version) || version==0 ||
                ((string)f.Attribute("required")!="0" && (string)f.Attribute("required")!="1") ||
                !Blob((string)f.Attribute("payload"),(string)f.Attribute("sha256"))) return false;
        }
        value=new RebirthNpcNativeReconstruction(node); return true;
    }
    private static RebirthNpcNativeHeader RebirthNpcNativeHeaderRead(XElement node){RebirthNpcNativeHeader.TryRead(node,out var header);return header;}
    internal static bool TryReadAggregate(XmlElement record, out RebirthNpcNativeReconstruction value)
    {
        value=null; XmlElement found=null;
        foreach(XmlNode child in record.ChildNodes)
        {
            if(!(child is XmlElement e) || !string.Equals(e.LocalName,"nativeReconstruction",StringComparison.OrdinalIgnoreCase)) continue;
            if(found!=null || e.Name!="nativeReconstruction" || e.NamespaceURI.Length!=0) return false;
            found=e;
        }
        return found==null || TryRead(XElement.Parse(found.OuterXml),out value);
    }
    internal XmlElement WriteAggregate(XmlDocument document)
    {
        var parsed=new XmlDocument{XmlResolver=null}; parsed.LoadXml(image.ToString(SaveOptions.DisableFormatting));
        return (XmlElement)document.ImportNode(parsed.DocumentElement,true);
    }
    internal static string Digest(byte[] bytes)
    {
        using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    }
    private static bool Blob(string payload,string hash)
    {
        if(string.IsNullOrEmpty(payload) || payload.Length > ((MaximumBlobBytes+2)/3)*4 || !Hash(hash)) return false;
        try { byte[] bytes=Convert.FromBase64String(payload); return bytes.Length>0 && bytes.Length<=MaximumBlobBytes &&
            Convert.ToBase64String(bytes)==payload && Digest(bytes)==hash; } catch(FormatException) { return false; }
    }
    private static bool Hash(string value)=>value!=null && value.Length==64 && value.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'));
    private static bool UInt(string value,out uint result)=>uint.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out result) && result.ToString(CultureInfo.InvariantCulture)==value;
    private static bool Identifier(string value)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>128||value!=value.Trim()||value.Any(char.IsControl))return false;
        try{XmlConvert.VerifyXmlChars(value);return true;}catch(XmlException){return false;}
    }
    private static bool Shape(XElement node,string name,int attrs)=>node!=null && node.Name==name && node.Attributes().Count()==attrs &&
        node.Nodes().All(n=>n is XElement || (n is XText t && string.IsNullOrWhiteSpace(t.Value)));
}
