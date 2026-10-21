using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    private static bool TryReadPendingDefinition(Evidence source)
    {
        if(!source.Terminal||!source.PendingNextWork)return true;
        IDisposable retained=null;
        try
        {
            string suffix=source.ToolsRebased?".tools-load":".terminal-load";
            if(!source.Path.EndsWith(suffix,StringComparison.Ordinal))return false;
            string path=source.Path.Substring(0,source.Path.Length-suffix.Length)+".pending-definition";
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            if(!File.Exists(path))return true; // Legacy REL2 permits only its original frozen definition.
            int maximum=checked(2*RebirthProductiveEarnedRowProof.MaximumFrameBytes+128);
            long length=new FileInfo(path).Length;
            if(length<82||length>maximum||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+1024),out retained))return false;
            byte[] record=Read(path,maximum);
            using(var stream=new MemoryStream(record,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x31445052u||new Guid(reader.ReadBytes(16))!=source.Frame.Birth||
                    new Guid(reader.ReadBytes(16))!=source.Frame.Operation||reader.ReadInt32()!=source.PendingTimerIndex||
                    reader.ReadSingle()!=source.PendingTimerBefore||reader.ReadSingle()!=source.PendingTimerTarget)return false;
                string binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                string extras=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,RebirthProductiveEarnedRowProof.MaximumFrameBytes);
                if(string.IsNullOrEmpty(binding)||string.IsNullOrEmpty(extras)||
                    !reader.ReadBytes(32).SequenceEqual(RebirthProductiveEarnedNativeBody.ComparisonDigest(source.ToolsBefore??source.Native,source.ToolsRebased?source.ToolsBeforeOffset:source.NativeOffset))||
                    stream.Position!=stream.Length||!Matches(path,record)||!source.PrimaryCurrent())return false;
                source.PendingRecipeBinding=binding;source.PendingExtras=extras;
                source.Dependencies=(source.Dependencies??Array.Empty<NativeChildDependency>()).Concat(new[]{NativeChildDependencyFor(path,record)}).ToArray();
                source.DefinitionRetention=retained;retained=null;return true;
            }
        }
        catch{return false;}
        finally{retained?.Dispose();}
    }
}
