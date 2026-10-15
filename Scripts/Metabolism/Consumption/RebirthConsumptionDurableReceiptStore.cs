using System;
using System.IO;
using System.Text;
using System.Xml;

// Caller-owned repository scope/lock lifetime remains an integration obligation.
// No save root inference, automatic load/reset or gameplay operation occurs here.
internal static class RebirthConsumptionDurableReceiptStore
{
    private static readonly object Sync=new object();
    internal static bool TryPersist(string ownedDirectory,RebirthConsumptionDurableReceipt receipt,
        Func<bool> currentAuthority,Func<RebirthConsumptionDurableReceipt,bool> validateOriginalPacket,out string error)
    {
        error=null;
        lock(Sync)
        {
            try
            {
                if(receipt==null||currentAuthority==null||validateOriginalPacket==null||!currentAuthority()
                    ||!validateOriginalPacket(receipt))throw new InvalidDataException("Current owner/packet witness unavailable.");
                string directory=Path.GetFullPath(ownedDirectory);
                if(!Path.IsPathRooted(ownedDirectory))throw new InvalidDataException("Absolute repository-owned directory required.");
                string path=Path.Combine(directory,receipt.Publication.ToString("N")+".xml");
                RebirthConsumptionDurableReceipt prior;
                if(File.Exists(path))
                {
                    if(!TryReadFinal(path,out prior)||!prior.SameOriginal(receipt))throw new InvalidDataException("Existing receipt conflicts or is unreadable.");
                    if(prior.Acknowledged&&!receipt.Acknowledged)throw new InvalidDataException("Terminal acknowledgement cannot regress.");
                    if(prior.Exact(receipt))return currentAuthority();
                }
                else if(receipt.Acknowledged||File.Exists(path+".bak"))
                    throw new InvalidDataException("Missing original pending final; no genesis ACK or backup fallback permitted.");
                if(!currentAuthority())throw new InvalidDataException("Owner scope changed before receipt write.");
                Directory.CreateDirectory(directory);
                string temporary=path+".tmp."+Guid.NewGuid().ToString("N");
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true,CloseOutput=false}))
                {receipt.Write().Save(writer);writer.Flush();stream.Flush(true);}
                RebirthConsumptionDurableReceipt candidate;
                if(!TryReadFinal(temporary,out candidate)||!candidate.Exact(receipt)||!validateOriginalPacket(candidate)||!currentAuthority())
                    throw new InvalidDataException("Temporary receipt readback or current witness failed.");
                // Existing primitive refuses unsupported replacement instead of deleting final.
                if(!RebirthDurableFileCommit.TryPublish(temporary,path,out error))return false;
                if(!TryReadFinal(path,out candidate)||!candidate.Exact(receipt)||!validateOriginalPacket(candidate)||!currentAuthority())
                    throw new InvalidDataException("Published final receipt verification failed.");
                return true;
            }
            catch(Exception failure){error=failure.GetType().Name+": "+failure.Message;return false;}
        }
    }
    internal static bool TryReadFinal(string path,out RebirthConsumptionDurableReceipt receipt)
    {
        receipt=null;
        try
        {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<=0||stream.Length>RebirthConsumptionDurableReceipt.MaximumXmlCharacters*4L)return false;
                using(var reader=new StreamReader(stream,new UTF8Encoding(false,true),false))
                {
                    var chars=new char[RebirthConsumptionDurableReceipt.MaximumXmlCharacters+1];
                    int used=0,read;
                    while(used<chars.Length&&(read=reader.Read(chars,used,chars.Length-used))>0)used+=read;
                    if(used>RebirthConsumptionDurableReceipt.MaximumXmlCharacters||reader.Peek()!=-1)return false;
                    return RebirthConsumptionDurableReceipt.TryRead(new string(chars,0,used),out receipt);
                }
            }
        }
        catch(Exception){receipt=null;return false;}
    }
}
