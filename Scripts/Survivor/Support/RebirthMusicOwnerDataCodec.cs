using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
// Pure bounded codecs only. Hashes/images are data commitments, never native authority.
internal static class RebirthMusicOwnerDataCodec
{
 internal const int MaxPayloadBytes=NetPackageRebirthMusicTransferOffer.MaxPayloadBytes;
 private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
 internal static bool Shape(XElement node,string name,string[] attributes,string[] children)
 {
  return node!=null&&node.Name==name&&node.Attributes().Count()==attributes.Length&&
   node.Attributes().All(a=>a.Name.NamespaceName.Length==0&&attributes.Contains(a.Name.LocalName))&&
   node.Elements().Select(x=>x.Name.ToString()).SequenceEqual(children)&&node.Nodes().All(n=>n is XElement);
 }
 internal static bool Integer(XElement node,string key,out int value)=>int.TryParse((string)node.Attribute(key),NumberStyles.None,CultureInfo.InvariantCulture,out value);
 internal static bool Long(XElement node,string key,out long value)=>long.TryParse((string)node.Attribute(key),NumberStyles.None,CultureInfo.InvariantCulture,out value);
 internal static bool GuidField(XElement node,string key,out Guid value)=>Guid.TryParseExact((string)node.Attribute(key),"N",out value)&&value!=Guid.Empty;
 internal static bool IsDigest(string value)
 {return value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
 internal static bool Data(string value,bool emptyAllowed)
 {
  if(value==null)return false;if(value.Length==0)return emptyAllowed;
  if(value.Length>262144)return false;
  try{var bytes=Convert.FromBase64String(value);return bytes.Length>0&&bytes.Length<=196608&&Convert.ToBase64String(bytes)==value;}catch{return false;}
 }
 internal static bool Copy(RebirthGearInventorySnapshot source,out RebirthGearInventorySnapshot copy)
 {copy=null;return source!=null&&RebirthGearInventorySnapshot.TryCaptureEncoded(source.Bag,source.Belt,source.OwnedBeltSlots,out copy);}
 internal static RebirthGearInventorySnapshot Copy(RebirthGearInventorySnapshot source)
 {if(!Copy(source,out var copy))throw new InvalidDataException("Invalid music owner image.");return copy;}
 internal static bool Equal(RebirthGearInventorySnapshot left,RebirthGearInventorySnapshot right)
 {
  if(!Copy(left,out var a)||!Copy(right,out var b)||a.OwnedBeltSlots!=b.OwnedBeltSlots||a.Bag.Length!=b.Bag.Length||a.Belt.Length!=b.Belt.Length)return false;
  for(int g=0;g<2;g++){var x=g==0?a.Bag:a.Belt;var y=g==0?b.Bag:b.Belt;for(int i=0;i<x.Length;i++)if(x[i].Count!=y[i].Count||x[i].ItemData!=y[i].ItemData)return false;}return true;
 }
 internal static XElement Image(string name,RebirthGearInventorySnapshot image)
 {
  var node=new XElement(name,new XAttribute("ownedBelt",image.OwnedBeltSlots),new XAttribute("bagSlots",image.Bag.Length),new XAttribute("beltSlots",image.Belt.Length));
  for(int g=0;g<2;g++){var cells=g==0?image.Bag:image.Belt;var area=new XElement(g==0?"bag":"belt");for(int i=0;i<cells.Length;i++)area.Add(new XElement("slot",new XAttribute("index",i),new XAttribute("count",cells[i].Count),new XAttribute("itemData",cells[i].ItemData)));node.Add(area);}return node;
 }
 internal static bool ReadImage(XElement node,string name,out RebirthGearInventorySnapshot image)
 {
  image=null;
  if(!Shape(node,name,new[]{"ownedBelt","bagSlots","beltSlots"},new[]{"bag","belt"})||!Integer(node,"ownedBelt",out var owned)||!Integer(node,"bagSlots",out var b)||!Integer(node,"beltSlots",out var t)||
     b<52||b>169||t<4||t>20||owned<4||owned>18||owned>t)return false;
  var bag=new RebirthGearInventoryPlan.Stack[b];var belt=new RebirthGearInventoryPlan.Stack[t];
  for(int g=0;g<2;g++)
  {
   var area=node.Element(g==0?"bag":"belt");var cells=g==0?bag:belt;
   if(area.Attributes().Any()||area.Nodes().Any(n=>!(n is XElement))||area.Elements().Count()!=cells.Length)return false;
   int i=0;foreach(var cell in area.Elements())
   {if(!Shape(cell,"slot",new[]{"index","count","itemData"},new string[0])||!Integer(cell,"index",out var index)||index!=i||!Integer(cell,"count",out var count))return false;cells[i++]=new RebirthGearInventoryPlan.Stack{Count=count,ItemData=(string)cell.Attribute("itemData")};}
  }
  return RebirthGearInventorySnapshot.TryCaptureEncoded(bag,belt,owned,out image);
 }
 internal static string Hash(XElement node)=>Hash(node.ToString(SaveOptions.DisableFormatting));
 internal static string Hash(string text)
 {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(text))).Replace("-",string.Empty).ToLowerInvariant();}
 internal static bool Encode(XElement node,out byte[] payload)
 {
  payload=null;
  try{var data=Utf8.GetBytes(node.ToString(SaveOptions.DisableFormatting));if(data.Length<1||data.Length>MaxPayloadBytes)return false;payload=data;return true;}catch{return false;}
 }
 internal static bool Decode(byte[] payload,out XElement node)
 {
  node=null;if(payload==null||payload.Length<1||payload.Length>MaxPayloadBytes)return false;
  try
  {
   string text=Utf8.GetString(payload);
   using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxPayloadBytes}))
   {var parsed=XElement.Load(reader,LoadOptions.PreserveWhitespace);if(reader.Read())return false;node=parsed;return true;}
  }
  catch{return false;}
 }
 internal static bool Canonical(XElement candidate,XElement original)
 {return Encode(candidate,out var a)&&Encode(original,out var b)&&a.SequenceEqual(b);}
 internal static bool Canonical(XElement candidate,byte[] original)
 {return Encode(candidate,out var a)&&original!=null&&a.SequenceEqual(original);}
}