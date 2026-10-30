using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Original-bound cold read of the exact snapshot captured before native save.
// A descriptor alone is never a saved receipt and cannot authorize another drop.
internal static class RebirthCookingDropSnapshot
{
    private static string Root(string save)=>Path.GetFullPath(save).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
    internal static XElement Describe(string save,string region,RebirthStationSnapshotEvidence.EntityImage image)
    {
        if(image==null)throw new InvalidDataException();string root=Root(save),path=Path.GetFullPath(region);
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
        var node=new XElement("snapshot",new XAttribute("version",1),new XAttribute("region",path.Substring(root.Length).Replace('\\','/')),
            new XAttribute("bytes",image.Bytes),new XAttribute("digest",image.Digest),new XAttribute("x",image.X),new XAttribute("z",image.Z),
            new XAttribute("offset",image.Offset),new XAttribute("length",image.Length),new XAttribute("generation",image.Generation));
        if(!Valid(node,image.Length))throw new InvalidDataException();return node;
    }
    internal static bool Valid(XElement node,int entityBytes)
    {
        try
        {
            if(node==null||node.Name!="snapshot"||node.Attributes().Count()!=9||node.Nodes().Any()||(int?)node.Attribute("version")!=1)return false;
            string path=(string)node.Attribute("region"),digest=(string)node.Attribute("digest");
            int bytes=(int)node.Attribute("bytes"),length=(int)node.Attribute("length");long offset=(long)node.Attribute("offset");
            return !string.IsNullOrEmpty(path)&&path.Length<=512&&!Path.IsPathRooted(path)&&!path.Contains(":")&&
                !path.Split('/','\\').Any(part=>part.Length==0||part=="."||part=="..")&&digest?.Length==64&&digest.All(Uri.IsHexDigit)&&
                bytes>8&&bytes<=RebirthStationSnapshotEvidence.MaximumSnapshotBytes&&length==entityBytes&&length>0&&length<=1024*1024&&
                offset>=8&&offset<=bytes-length&&(long)node.Attribute("generation")>0&&node.Attribute("x")!=null&&node.Attribute("z")!=null;
        }
        catch{return false;}
    }
    internal static bool TryRead(string save,XElement descriptor,byte[] expectedEntity,Func<bool> current,IRebirthStationSnapshotByteBudget budget,
        out RebirthStationSnapshotEvidence.PublishedSnapshot proof)
    {
        proof=null;IDisposable allocation=null;
        try
        {
            if(expectedEntity==null||!Valid(descriptor,expectedEntity.Length)||current==null||budget==null||!current())return false;
            string root=Root(save),path=Path.GetFullPath(Path.Combine(root,(string)descriptor.Attribute("region")));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||Root(GameIO.GetSaveGameDir())!=root)return false;
            int bytes=(int)descriptor.Attribute("bytes"),x=(int)descriptor.Attribute("x"),z=(int)descriptor.Attribute("z");
            long offset=(long)descriptor.Attribute("offset")-8;
            if(bytes>budget.MaximumSnapshotBytes||!budget.TryReserveAllocation(6L*bytes+2L*RebirthStationRegionPayload.MaximumPayloadBytes+256*1024,out allocation))return false;
            byte[] payload;
            using(var stream=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                if(!RebirthStationRegionPayload.TryRead(stream,RebirthStationRegionPayload.NativeLocalIndex(x),RebirthStationRegionPayload.NativeLocalIndex(z),out payload))return false;
            if(!current()||!RebirthStationChunkInflate.TryDecode(payload,bytes-8,out var decoded)||decoded.Length!=bytes-8||decoded.Length<12||
                BitConverter.ToInt32(decoded,0)!=x||BitConverter.ToInt32(decoded,4)!=0||BitConverter.ToInt32(decoded,8)!=z)return false;
            var snapshot=new byte[bytes];snapshot[0]=116;snapshot[1]=116;snapshot[2]=99;snapshot[4]=47;Buffer.BlockCopy(decoded,0,snapshot,8,decoded.Length);
            if(RebirthStationPublicationRecord.Digest(snapshot)!=(string)descriptor.Attribute("digest"))return false;
            for(int i=0;i<expectedEntity.Length;i++)if(decoded[(int)offset+i]!=expectedEntity[i])return false;
            if(!current()||Root(GameIO.GetSaveGameDir())!=root)return false;
            proof=new RebirthStationSnapshotEvidence.PublishedSnapshot(path,RebirthStationPublicationRecord.Digest(payload),x,z,(long)descriptor.Attribute("generation"));
            return true;
        }
        catch{return false;}
        finally{allocation?.Dispose();}
    }
}
