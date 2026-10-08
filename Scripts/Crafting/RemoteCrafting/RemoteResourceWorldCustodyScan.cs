using System;
using System.IO;
using System.Xml;

// First identity initialization only. Observable or unreadable prior custody forbids
// creating a new world key; this scan never repairs, deletes or changes player records.
internal static class RemoteResourceWorldCustodyScan
{
    internal static bool CanInitialize(string saveRoot)
    {
        try
        {
            string directory=Path.Combine(saveRoot,"RebirthData","Survivor","Players");
            if(!Directory.Exists(directory))return true;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            int count=0;long total=0;
            foreach(string path in Directory.EnumerateFiles(directory,"*.xml*",SearchOption.TopDirectoryOnly))
            {
                if(++count>4096||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<1||stream.Length>16777216||(total+=stream.Length)>67108864)return false;
                    var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16777216};
                    using(var reader=XmlReader.Create(stream,settings))
                    {
                        bool root=false;
                        while(reader.Read())
                        {
                            if(reader.NodeType!=XmlNodeType.Element)continue;
                            if(!root)
                            {
                                if(reader.Depth!=0||reader.Name!="rebirthWorldCharacter")return false;
                                root=true;
                            }
                            if(reader.LocalName=="remoteResourceRefunds")return false;
                        }
                        if(!root)return false;
                    }
                }
            }
            return true;
        }
        catch{return false;}
    }
}