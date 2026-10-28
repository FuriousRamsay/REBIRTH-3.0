// Actual bounded streaming expected-frame seal. NOT_RUN; no native authority.
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
static class RebirthDeferredOwnerFrameHashFixture
{
    private const int Maximum=16*1024*1024;
    // PRODUCTION_FRAME_HASH_STREAM
    // PRODUCTION_FRAME_HASH
    static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        var original=new XElement("nativeOwnerFrame",new XAttribute("version",1),new XElement("value","a\nb"));
        string seal=Frame(original);Need(seal!=null&&seal==Frame(new XElement(original)),"Exact immutable frame changed seal");
        var changed=new XElement(original);changed.Element("value").Value="different";
        Need(seal!=Frame(changed),"Changed expected frame retained seal");
        Need(Frame(null)==null,"Missing original acquired seal");
        Need(Frame(new XElement("oversized",new string('x',Maximum)))==null,"Encoded frame byte cap exceeded");
        Need(Frame(original)==seal,"Failed oversized frame changed later hash state");
        Need(original.Element("value").Value=="a\nb","Sealing mutated the original");
    }
}
