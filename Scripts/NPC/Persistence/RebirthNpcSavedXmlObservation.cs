using System;
using System.IO;
using System.Xml;

// Resource scope only. The supplied domain reader still validates the final
// native file and its own original receipt; XML admission grants no settlement.
internal static class RebirthNpcSavedXmlObservation
{
    private const long MaximumBytes=64L*1024*1024;
    internal static bool Observe(string path,Func<bool> positiveObservation)
    {
        if(!ThreadManager.IsMainThread()||string.IsNullOrEmpty(path)||positiveObservation==null)return false;
        IDisposable allocation=null;
        try
        {
            path=Path.GetFullPath(path);
            for(var parent=new DirectoryInfo(Path.GetDirectoryName(path));parent!=null;parent=parent.Parent)
                if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;
            // Deny write/delete sharing while both passes and the actual domain
            // reader run. The latter may open another read handle to this primary.
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length<1||input.Length>MaximumBytes)return false;
                // XmlReader can retain a token's attribute/name table before
                // returning Read(). Charge that parser workspace before its first
                // allocation; never assume a byte limit is a DOM heap limit.
                long parser=checked(1024*1024+32*input.Length);
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(parser,out allocation))return false;
                long nodes=0,attributes=0,characters=0;
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,
                    MaxCharactersInDocument=MaximumBytes,CloseInput=false};
                using(var reader=XmlReader.Create(input,settings))
                {
                    while(reader.Read())
                    {
                        if(reader.Depth>64)return false;
                        nodes=checked(nodes+1);attributes=checked(attributes+reader.AttributeCount);
                        characters=checked(characters+reader.Name.Length+reader.Value.Length);
                        if(reader.MoveToFirstAttribute())
                        {
                            do{characters=checked(characters+reader.Name.Length+reader.Value.Length);}while(reader.MoveToNextAttribute());
                            reader.MoveToElement();
                        }
                        // Covers DOM/LINQ node wrappers, attribute/name tables,
                        // duplicated UTF16 text and decoded record collections.
                        // Refusal leaves the original's custody unresolved; the
                        // shared ceiling itself is never increased here.
                        long graph=checked(1024*nodes+512*attributes+16*characters);
                        if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,checked(parser+graph)))return false;
                    }
                }
                // Preserve the reservation until the actual saved-record reader
                // AND its exact caller predicate have finished using that graph.
                return positiveObservation();
            }
        }
        catch{return false;}
        finally{allocation?.Dispose();}
    }
}
