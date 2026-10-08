using System;using System.Globalization;using System.Linq;using System.Xml.Linq;
// Required original bare-hand and selected native/custody binding; no custody grants.
internal sealed class RebirthNpcNativeHand
{
    private readonly XElement image;
    private RebirthNpcNativeHand(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal int Selected=>int.Parse((string)image.Attribute("selected"),CultureInfo.InvariantCulture);
    internal int Mode=>int.Parse((string)image.Attribute("mode"),CultureInfo.InvariantCulture);
    internal string Stack=>(string)image.Attribute("stack");
    internal string BarePayload=>(string)image.Attribute("barePayload");
    internal static bool TryRead(XElement node,out RebirthNpcNativeHand value)
    {
        value=null;
        if(node==null||node.Name!="nativeHand"||node.HasElements||node.Attributes().Count()!=7||node.Attributes().Any(a=>a.IsNamespaceDeclaration||a.Name.NamespaceName.Length!=0)||node.Nodes().Any(n=>!(n is XText t&&string.IsNullOrWhiteSpace(t.Value)))||
            (string)node.Attribute("version")!="1"||(string)node.Attribute("required")!="1"||
            ((string)node.Attribute("mode")!="0"&&(string)node.Attribute("mode")!="1")||
            !int.TryParse((string)node.Attribute("selected"),NumberStyles.None,CultureInfo.InvariantCulture,out var selected)||selected<0||selected>=4096||selected.ToString(CultureInfo.InvariantCulture)!=(string)node.Attribute("selected"))return false;
        string stack=(string)node.Attribute("stack"),payload=(string)node.Attribute("barePayload"),hash=(string)node.Attribute("bareHash");
        if(stack==null||stack.Length>0&&(!Guid.TryParseExact(stack,"N",out var id)||id==Guid.Empty||id.ToString("N")!=stack)||
            string.IsNullOrEmpty(payload)||payload.Length>262144||hash==null||hash.Length!=64||hash.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
        try{var bytes=Convert.FromBase64String(payload);if(bytes.Length==0||Convert.ToBase64String(bytes)!=payload||RebirthNpcNativeReconstruction.Digest(bytes)!=hash)return false;}
        catch(FormatException){return false;}
        value=new RebirthNpcNativeHand(node);return true;
    }
    internal bool Matches(XElement slots,RebirthNpcNativeHeader header)
    {
        if(slots==null||header==null||header.Integer("holdingMode")!=Mode)return false;
        var entries=slots.Elements("slot").Where(n=>(string)n.Attribute("area")=="toolbelt"&&(string)n.Attribute("index")==Selected.ToString(CultureInfo.InvariantCulture)).ToArray();
        return Stack.Length==0?entries.Length==0:entries.Length==1&&(string)entries[0].Attribute("stack")==Stack;
    }
    internal static bool TryCapture(EntityRebirthHumanoidNPC npc,XElement slots,out RebirthNpcNativeHand hand)
    {
        hand=null;
        if(npc?.Hand==null||npc.inventory==null||!RebirthNpcIdleHandActionQualification.IsIdle(npc)||npc.Hand.IsSwitching||npc.Hand.mode==Hand.HoldingMode.Transient||
            npc.Hand.BareHandItemValue==null||npc.Hand.BareHandItemValue.IsEmpty()||npc.Hand.BareHandItemValue.ItemClass==null)return false;
        int selected=npc.inventory.SelectedSlot;if(selected<0||selected>=npc.inventory.SlotCount)return false;
        var entries=slots.Elements("slot").Where(n=>(string)n.Attribute("area")=="toolbelt"&&(string)n.Attribute("index")==selected.ToString(CultureInfo.InvariantCulture)).ToArray();if(entries.Length>1)return false;
        string payload=RebirthNativeItemCodec.Encode(npc.Hand.BareHandItemValue.Clone());if(payload.Length>262144)return false;
        return TryRead(new XElement("nativeHand",new XAttribute("version",1),new XAttribute("required",1),new XAttribute("mode",(int)npc.Hand.mode),new XAttribute("selected",selected),new XAttribute("stack",entries.Length==0?"":(string)entries[0].Attribute("stack")),new XAttribute("barePayload",payload),new XAttribute("bareHash",RebirthNpcNativeReconstruction.Digest(Convert.FromBase64String(payload)))),out hand);
    }
}
