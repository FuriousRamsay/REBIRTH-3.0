using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Text;

public static partial class RemoteResourceTransactions
{
    // Retained only after the actual positive native save and full artifact
    // comparison. Metadata retries use this immutable historical material;
    // unrelated later rows in the same region/aggregate cannot replace it.
    internal sealed class ActionSavedMaterial:IDisposable
    {
        internal string Phase,Path;internal byte[] Bytes;internal IDisposable Memory;
        public void Dispose(){Bytes=null;Memory?.Dispose();Memory=null;}
    }
    private static byte[] ReadActionMaterial(string path,int maximum)
    {
        ActionPath(path);if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Action artifact is a reparse point");
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(input.Length<1||input.Length>maximum)throw new InvalidDataException("Action artifact bound exceeded");var bytes=new byte[checked((int)input.Length)];int offset=0;while(offset<bytes.Length){int count=input.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}if(input.ReadByte()!=-1)throw new IOException("Action artifact grew");return bytes;}
    }
    private static ActionSavedMaterial RememberActionMaterial(string phase,string path,int maximum)
    {
        ActionPath(path);long length=new FileInfo(path).Length;if(length<1||length>maximum)throw new InvalidDataException("Saved material exceeds bound");
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(length+2L*path.Length+4096),out var allocation))throw new InvalidDataException("Saved material retention unavailable");
        try
        {
            byte[] bytes=ReadActionMaterial(path,checked((int)length));if(bytes.LongLength!=length)throw new IOException("Saved material changed during capture");
            return new ActionSavedMaterial{Phase=phase,Path=path,Bytes=bytes,Memory=allocation};
        }
        catch{allocation.Dispose();throw;}
    }
    private static bool FinishActionMaterial(ActionPayment payment,ActionSavedMaterial material,string phase,string stage)
    {
        if(material?.Bytes==null||material.Phase!=phase)throw new InvalidOperationException("Saved material phase differs");
        // This full byte comparison is integrity of an already-issued native
        // event, not authority inferred from a newly observed equal endpoint.
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(material.Bytes.LongLength+4096,out var readback))return false;
        using(readback)if(!ReadActionMaterial(material.Path,material.Bytes.Length).SequenceEqual(material.Bytes))return false;
        ActionRecord(payment,stage,new XElement("native",new XAttribute("file",System.IO.Path.GetFileName(material.Path)),new XAttribute("length",material.Bytes.Length),new XAttribute("digest",ActionNativeMaterialDigest(material.Bytes))));return true;
    }
    private static string ActionNativeMaterialDigest(byte[] bytes)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static void VerifyActionMaterialImage(ActionSavedMaterial material,RebirthRemoteResourceNativePublication.SavedTile tile,XElement mobile)
    {
        try
        {
            using(var input=new MemoryStream(material.Bytes,false))
            using(var comparison=new ActionCompareStream(input,48L*1024*1024))
            using(var writer=XmlWriter.Create(comparison,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),CloseOutput=false}))
            {if(tile!=null)tile.WriteActionArtifact(writer);else mobile.WriteTo(writer);writer.Flush();comparison.RequireEnd();}
        }
        catch{material.Dispose();throw;}
    }
    private static bool FinishActionLegMaterial(ActionPayment payment,ActionLeg leg,string phase)
    {
        string stage="source-"+payment.Legs.IndexOf(leg)+"-"+phase+"-saved";
        if(!FinishActionMaterial(payment,leg.SavedMaterial,phase,stage))return false;
        leg.SavedMaterial.Dispose();leg.SavedMaterial=null;leg.Publication?.Dispose();leg.Publication=null;return true;
    }
}
