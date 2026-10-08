using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable absolute native geometry. Hierarchy addresses belong to the original prefab;
// they are not asset catalogue IDs and cannot authorize creation or custody.
internal sealed class RebirthNpcNativeGeometry
{
    private readonly XElement image;
    private RebirthNpcNativeGeometry(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal static bool TryRead(XElement node,out RebirthNpcNativeGeometry value)
    {
        value=null;
        if(!Shape(node,"nativeGeometry",2)||(string)node.Attribute("version")!="1"||(string)node.Attribute("required")!="1")return false;
        var children=node.Elements().ToArray();
        if(children.Length<6||children.Length>1030)return false;
        string[] roles={"model","physics","physicsBody","head"};
        for(int i=0;i<4;i++)
        {
            var n=children[i];
            if(!Shape(n,"transform",12)||(string)n.Attribute("role")!=roles[i]||!Address((string)n.Attribute("path"))||!Numbers(n,"px","py","pz","qx","qy","qz","qw","sx","sy","sz"))return false;
            if((string)n.Attribute("path")=="."&&(Number(n,"px")!=0||Number(n,"py")!=0||Number(n,"pz")!=0||Number(n,"qx")!=0||Number(n,"qy")!=0||Number(n,"qz")!=0||Number(n,"qw")!=1))return false;
            if(Number(n,"sx")==0||Number(n,"sy")==0||Number(n,"sz")==0)return false;
            double q=0;foreach(var name in new[]{"qx","qy","qz","qw"})q+=Math.Pow(Number(n,name),2);
            if(q<0.99||q>1.01)return false;
        }
        if(!Shape(children[4],"native",7)||!Numbers(children[4],"baseHeight","height","lowerY","heightScale","headStandard","headBig")||!Integer((string)children[4].Attribute("headState"),out _))return false;
        var controller=children[5];
        string kind=(string)controller.Attribute("kind");
        if(kind=="none"){if(!Shape(controller,"controller",1))return false;}
        else if((kind!="unity"&&kind!="kinematic")||!Shape(controller,"controller",10)||!Address((string)controller.Attribute("path"))||(string)controller.Attribute("index")!="0"||!Numbers(controller,"x","y","z","height","radius","step","skin")||!Nonnegative(controller,"height","radius","step","skin"))return false;
        var keys=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var n in children.Skip(6))
        {
            string type=(string)n.Attribute("kind");
            string[] dims=type=="capsule"?new[]{"height","radius"}:type=="box"?new[]{"sx","sy","sz"}:type=="sphere"?new[]{"radius"}:type=="character"?new[]{"height","radius","step","skin","slope","minMove"}:null;
            int attrs=type=="capsule"?9:type=="box"?9:type=="sphere"?7:type=="character"?14:0;
            if(dims==null||!Shape(n,"collider",attrs)||!Address((string)n.Attribute("path"))||!Integer((string)n.Attribute("index"),out var index)||index<0||index>=1024||
                !keys.Add((string)n.Attribute("path")+":"+index)||!Numbers(n,"x","y","z")||!Numbers(n,dims)||!Nonnegative(n,dims))return false;
            if(type=="character"&&(((string)n.Attribute("collisions")!="0"&&(string)n.Attribute("collisions")!="1")||((string)n.Attribute("overlap")!="0"&&(string)n.Attribute("overlap")!="1")))return false;
            if(type=="capsule"&&(!Integer((string)n.Attribute("direction"),out var direction)||direction<0||direction>2))return false;
        }
        value=new RebirthNpcNativeGeometry(node);return true;
    }
    internal static float Number(XElement node,string name)=>float.Parse((string)node.Attribute(name),CultureInfo.InvariantCulture);
    internal static int Index(XElement node,string name)=>int.Parse((string)node.Attribute(name),CultureInfo.InvariantCulture);
    private static bool Numbers(XElement node,params string[] names)=>names.All(name=>Finite((string)node.Attribute(name)));
    private static bool Nonnegative(XElement node,params string[] names)=>names.All(name=>Number(node,name)>=0);
    private static bool Finite(string text)=>float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&!float.IsNaN(n)&&!float.IsInfinity(n)&&n.ToString("R",CultureInfo.InvariantCulture)==text;
    private static bool Integer(string text,out int value)=>int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)&&value.ToString(CultureInfo.InvariantCulture)==text;
    private static bool Address(string path)
    {
        if(path==".")return true;
        if(string.IsNullOrEmpty(path)||path.Length>768)return false;
        var parts=path.Split('/');return parts.Length<=128&&parts.All(p=>Integer(p,out var index)&&index>=0&&index<4096);
    }
    private static bool Shape(XElement node,string name,int attrs)=>node!=null&&node.Name==name&&node.Attributes().Count()==attrs&&!node.Attributes().Any(a=>a.IsNamespaceDeclaration||a.Name.NamespaceName.Length!=0)&&node.Nodes().All(n=>n is XElement||(n is XText t&&string.IsNullOrWhiteSpace(t.Value)))&&(name=="nativeGeometry"||!node.HasElements);
}
