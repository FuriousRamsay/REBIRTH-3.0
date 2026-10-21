using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

public static partial class RebirthNpcInventoryPersistenceStore
{
    internal static bool ValidateHistoricalConsumptionSource(string archived,RebirthNpcStableId id,uint revision,Guid operation,System.Collections.Generic.Dictionary<string,int> quantities,System.Collections.Generic.Dictionary<string,int> reservations,RebirthNpcNativeStackSet native)
    {
        try
        {
            if(archived==null||archived.Length>8*1024*1024)return false;
            var document=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(new StringReader(archived),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024}))document.Load(reader);
            if(!ValidateDocument(document))return false;
            var row=document.DocumentElement.SelectNodes("inventory").Cast<XmlElement>().SingleOrDefault(node=>node.GetAttribute("stableId")==id.ToString());if(row==null)return false;
            var saved=Read(row);return saved!=null&&saved.Revision==revision&&(operation==Guid.Empty||saved.ReplayJournal.Contains(operation))&&saved.Quantities.Count==quantities.Count&&saved.Reservations.Count==reservations.Count&&
                saved.Quantities.All(pair=>quantities.TryGetValue(pair.Key,out int value)&&value==pair.Value)&&saved.Reservations.All(pair=>reservations.TryGetValue(pair.Key,out int value)&&value==pair.Value)&&
                XNode.DeepEquals(saved.NativeStacks?.Write(),native?.Write());
        }
        catch{return false;}
    }
    internal static string CaptureColdUnissuedConsumptionSource(RebirthNpcInventorySnapshot expected)
    {
        lock(Sync)try
        {
            if(expected==null||!BoundCurrent||dirty)return null;var file=new FileInfo(boundPath);
            if(!file.Exists||file.Length<1||file.Length>8*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0||!CanRetireBacking)return null;
            using var reader=XmlReader.Create(boundPath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});var document=XDocument.Load(reader);string full=document.ToString(SaveOptions.DisableFormatting);
            return full.Length<=8*1024*1024&&ValidateHistoricalConsumptionSource(full,expected.NpcId,expected.Revision,Guid.Empty,expected.Quantities,expected.Reservations,expected.NativeStacks)?full:null;
        }
        catch{return null;}
    }
    internal static bool QualifyColdConsumptionSource(RebirthNpcStableId id,string archived)
    {
        lock(Sync)try
        {
            if(!BoundCurrent||dirty||archived==null||archived.Length>8*1024*1024)return false;
            var file=new FileInfo(boundPath);if(!file.Exists||file.Length>8*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0||!CanRetireBacking)return false;
            using var oldReader=XmlReader.Create(new StringReader(archived),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            using var currentReader=XmlReader.Create(boundPath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var old=XDocument.Load(oldReader);var current=XDocument.Load(currentReader);
            var expected=old.Root.Elements("inventory").SingleOrDefault(row=>(string)row.Attribute("stableId")==id.ToString());
            var actual=current.Root.Elements("inventory").SingleOrDefault(row=>(string)row.Attribute("stableId")==id.ToString());
            return expected!=null&&actual!=null&&XNode.DeepEquals(expected,actual);
        }
        catch{return false;}
    }
    internal static bool CaptureConsumptionSave(RebirthNpcMetabolismCustody.Original original,out string saved)
    {
        saved=null;if(!RebirthNpcMetabolismCustody.Current(original)||!original.SourceReturned)return false;
        lock(Sync)try
        {
            if(!BoundCurrent||dirty)return false;var info=new FileInfo(boundPath);if(!info.Exists||info.Length<1||info.Length>8*1024*1024||(info.Attributes&FileAttributes.ReparsePoint)!=0||!CanRetireBacking)return false;
            using var reader=XmlReader.Create(boundPath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});var document=XDocument.Load(reader);
            // CanRetireBacking already compared the COMPLETE saved generation
            // with actual runtime. This retained full file is the saved native
            // source witness, not a transaction GUID or a quantity-only receipt.
            var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(original.Id);
            if(snapshot.Revision!=original.Before.Revision+1||(snapshot.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)!=(original.AfterNative?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)||
                snapshot.Quantities.Count!=original.AfterQuantities.Count||snapshot.Quantities.Any(pair=>!original.AfterQuantities.TryGetValue(pair.Key,out int value)||value!=pair.Value))return false;
            saved=document.ToString(SaveOptions.DisableFormatting);return saved.Length<=8*1024*1024;
        }
        catch{return false;}
    }
}
