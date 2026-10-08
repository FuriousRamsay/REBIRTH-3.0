internal static class AtomicXmlFixture
{
    private static int passed;
    private static void Check(bool ok,string name)
    {if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    public static void Main(string[] args)
    {
        string path=Path.Combine(args[0],"character.xml"),error;
        XDocument first=new XDocument(new XElement("character",new XAttribute("revision",1),new XElement("progress","0.25")));
        XDocument second=new XDocument(new XElement("character",new XAttribute("revision",2),new XElement("progress","0.75")));
        Check(RebirthAtomicXmlFile.TryWrite(path,first,out error)&&XNode.DeepEquals(XDocument.Load(path),first),"first final contains exact serialized progress");
        Check(RebirthAtomicXmlFile.TryWrite(path,second,out error)&&XNode.DeepEquals(XDocument.Load(path),second),"replacement final contains latest progress");
        Check(File.Exists(path+".bak")&&XNode.DeepEquals(XDocument.Load(path+".bak"),first),"replacement retains preceding final as backup");
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
            Check(!RebirthAtomicXmlFile.TryWrite(path,first,out error)&&!string.IsNullOrEmpty(error),"locked final refuses write");
        Check(XNode.DeepEquals(XDocument.Load(path),second),"failed write preserves current final");
        Check(!File.Exists(path+".tmp"),"failed write cleans temporary candidate");
        Check(!RebirthAtomicXmlFile.TryWrite(path,null,out error)&&XNode.DeepEquals(XDocument.Load(path),second),"null candidate preserves current final");
        Console.WriteLine(passed+" actual filesystem checks passed");
    }
}