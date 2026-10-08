using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
// Desired original release policy. Suspended process-local flags are not saved state.
internal sealed class RebirthNpcNativeBodyPolicy
{
 private readonly XElement image;private RebirthNpcNativeBodyPolicy(XElement node){image=new XElement(node);}
 internal bool HasDynamics=>(string)image.Attribute("version")=="2";
 internal XElement Write()=>new XElement(image);
 internal static bool TryRead(XElement node,out RebirthNpcNativeBodyPolicy policy)
 {
  policy=null;if(!Shape(node,"nativeBodyPolicy",4)||(node.Attribute("version")?.Value!="1"&&node.Attribute("version")?.Value!="2")||node.Attribute("required")?.Value!="1"||!Flag(node,"actor")||!Flag(node,"controller"))return false;
  var children=node.Elements().ToArray();if(children.Length>2048)return false;var keys=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
  foreach(var child in children)
  {
   string kind=child.Name.LocalName;int count=kind=="collider"?3:kind=="body"?4:0;
   if(count==0||!Shape(child,kind,count)||!Address((string)child.Attribute("path"))||!Index((string)child.Attribute("index"))||!keys.Add(kind+":"+(string)child.Attribute("path")+":"+(string)child.Attribute("index")))return false;
   if(kind=="collider"&&child.HasElements||kind=="body"&&((string)node.Attribute("version")=="1"?child.HasElements:child.Elements().Count()!=1||!RebirthNpcNativeRigidbodyState.TryRead(child.Elements().FirstOrDefault(),out var dynamics)||!dynamics.CompatibleWithKinematic((string)child.Attribute("kinematic")=="1")))return false;
   if(kind=="collider"?!Flag(child,"enabled"):!Flag(child,"kinematic")||!Flag(child,"collisions"))return false;
  }
  if(children.Count(n=>n.Name=="collider")>1024||children.Count(n=>n.Name=="body")>1024)return false;
  policy=new RebirthNpcNativeBodyPolicy(node);return true;
 }
 private static bool Flag(XElement node,string name)=>(string)node.Attribute(name)=="0"||(string)node.Attribute(name)=="1";
 private static bool Index(string value)=>int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var n)&&n>=0&&n<4096&&n.ToString(CultureInfo.InvariantCulture)==value;
 private static bool Address(string value)=>value=="."||!string.IsNullOrEmpty(value)&&value.Length<=768&&value.Split('/').Length<=128&&value.Split('/').All(Index);
 private static bool Shape(XElement node,string name,int attrs)=>node!=null&&node.Name==name&&node.Attributes().Count()==attrs&&!node.Attributes().Any(a=>a.IsNamespaceDeclaration||a.Name.NamespaceName.Length!=0)&&node.Nodes().All(n=>n is XElement||n is XText t&&string.IsNullOrWhiteSpace(t.Value));
}
