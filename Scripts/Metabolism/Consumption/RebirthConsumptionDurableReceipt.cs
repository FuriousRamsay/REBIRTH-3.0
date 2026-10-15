using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Detached receipt format. No entity resolution, gameplay or repository activation.
internal sealed class RebirthConsumptionDurableReceipt
{
    internal const int MaximumWireBytes=1048576;
    internal const int MaximumXmlCharacters=1600000;
    internal readonly Guid Publication;
    internal readonly string Owner,StorageKey,Creation,WorldGuid,SaveKey;
    internal readonly int OriginalEntityId;
    internal readonly bool Acknowledged;
    private readonly byte[] wire;
    internal byte[] OriginalWire=>(byte[])wire.Clone();
    private RebirthConsumptionDurableReceipt(Guid id,string owner,string key,string creation,string world,string save,int entity,byte[] bytes,bool ack)
    {Publication=id;Owner=owner;StorageKey=key;Creation=creation;WorldGuid=world;SaveKey=save;OriginalEntityId=entity;wire=(byte[])bytes.Clone();Acknowledged=ack;}

    internal static bool TryCreate(Guid id,string owner,string key,string creation,string world,string save,int entity,byte[] bytes,bool ack,out RebirthConsumptionDurableReceipt receipt)
    {
        receipt=null;string normalized;Guid worldId;
        if(id==Guid.Empty||string.IsNullOrEmpty(owner)||owner.Length>512||owner.Trim()!=owner||owner.Any(char.IsControl)
            ||!HexKey(key)||key!=RebirthStablePlayerIdentity.ComputeStorageKey(owner)||!HexKey(save)
            ||!RebirthSurvivorRequestScope.TryNormalize(creation,out normalized)||normalized!=creation
            ||!Guid.TryParse(world,out worldId)||worldId==Guid.Empty||world!=worldId.ToString("N")
            ||entity<0||bytes==null||bytes.Length<2||bytes.Length>MaximumWireBytes)return false;
        receipt=new RebirthConsumptionDurableReceipt(id,owner,key,creation,world,save,entity,bytes,ack);return true;
    }
    internal RebirthConsumptionDurableReceipt WithAcknowledgement()
        =>new RebirthConsumptionDurableReceipt(Publication,Owner,StorageKey,Creation,WorldGuid,SaveKey,OriginalEntityId,wire,true);
    internal bool SameOriginal(RebirthConsumptionDurableReceipt other)
        =>other!=null&&Publication==other.Publication&&Owner==other.Owner&&StorageKey==other.StorageKey&&Creation==other.Creation
            &&WorldGuid==other.WorldGuid&&SaveKey==other.SaveKey&&OriginalEntityId==other.OriginalEntityId&&wire.SequenceEqual(other.wire);
    internal bool Exact(RebirthConsumptionDurableReceipt other)=>SameOriginal(other)&&Acknowledged==other.Acknowledged;
    internal XDocument Write()
        =>new XDocument(new XElement("consumptionPublication",new XAttribute("version",1),new XAttribute("publication",Publication.ToString("N")),
            new XAttribute("owner",Owner),new XAttribute("storageKey",StorageKey),new XAttribute("creation",Creation),
            new XAttribute("world",WorldGuid),new XAttribute("saveKey",SaveKey),new XAttribute("entity",OriginalEntityId),
            new XAttribute("state",Acknowledged?"acknowledged":"pending"),
            new XElement("wire",new XAttribute("sha256",Digest(wire)),Convert.ToBase64String(wire))));
    internal static bool TryRead(string xml,out RebirthConsumptionDurableReceipt receipt)
    {
        receipt=null;
        if(string.IsNullOrEmpty(xml)||xml.Length>MaximumXmlCharacters)return false;
        try
        {
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumXmlCharacters};
            using(var text=new StringReader(xml))
            using(var reader=XmlReader.Create(text,settings))
            {
                var document=XDocument.Load(reader,LoadOptions.None);var root=document.Root;
                string[] expected={"version","publication","owner","storageKey","creation","world","saveKey","entity","state"};
                if(root==null||root.Name!="consumptionPublication"||root.Attributes().Count()!=expected.Length
                    ||root.Attributes().Any(a=>!expected.Contains(a.Name.ToString()))||(string)root.Attribute("version")!="1"
                    ||root.Elements().Count()!=1||root.Nodes().Any(n=>!(n is XElement)&&(!(n is XText t)||!string.IsNullOrWhiteSpace(t.Value))))return false;
                var payload=root.Element("wire");
                if(payload==null||payload.HasElements||payload.Attributes().Count()!=1||payload.Attribute("sha256")==null
                    ||payload.Nodes().Any(n=>!(n is XText)))return false;
                string encoded=payload.Value;
                if(encoded.Length>((MaximumWireBytes+2)/3)*4)return false;
                byte[] bytes=Convert.FromBase64String(encoded);
                if(Convert.ToBase64String(bytes)!=encoded||Digest(bytes)!=(string)payload.Attribute("sha256"))return false;
                Guid id;int entity;string state=(string)root.Attribute("state");
                if(!Guid.TryParseExact((string)root.Attribute("publication"),"N",out id)
                    ||!int.TryParse((string)root.Attribute("entity"),out entity)||(state!="pending"&&state!="acknowledged"))return false;
                return TryCreate(id,(string)root.Attribute("owner"),(string)root.Attribute("storageKey"),(string)root.Attribute("creation"),
                    (string)root.Attribute("world"),(string)root.Attribute("saveKey"),entity,bytes,state=="acknowledged",out receipt);
            }
        }
        catch(Exception){receipt=null;return false;}
    }
    private static bool HexKey(string value)=>value!=null&&value.Length==64&&value.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'));
    private static string Digest(byte[] bytes)
    {using(var hash=SHA256.Create())return string.Concat(hash.ComputeHash(bytes).Select(b=>b.ToString("x2")));}
}
