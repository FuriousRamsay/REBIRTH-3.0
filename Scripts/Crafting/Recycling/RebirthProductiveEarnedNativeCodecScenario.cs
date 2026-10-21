#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

// Exercises the actual production codecs against a genuine retained native
// original. No replacement inventory, packet, source identity or effect is issued.
public sealed class ConsoleCmdRebirthEarnedNativeCodec:ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbearnedcodec"};
    public override string getDescription()=>"Read-only native earned plan codec assertions in disposable CodexTest.";
    public override string getHelp()=>"rbearnedcodec output|pending|tools|viewerSource operationGuid";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(sender.RemoteClientInfo!=null||!ThreadManager.IsMainThread()||!RebirthGameBridge.IsRunning||!RebirthProvenanceDetachedReservation.IsDisposableSave()||
            !RebirthProductiveEarnedCoordinator.Enabled||args.Count!=2||!Guid.TryParseExact(args[1],"N",out var operation))
        {SdtdConsole.Instance.Output("*** ERROR "+getHelp());return;}
        try
        {
            JObject result=GameManager.Instance.World.IsRemote()?RebirthProductiveEarnedRemoteClient.NativeCodecScenario(operation,args[0]):RebirthProductiveEarnedRemoteHost.NativeCodecScenario(operation,args[0]);
            if(result==null)throw new InvalidDataException("Actual retained native original is unavailable");
            SdtdConsole.Instance.Output(result.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch(Exception error){SdtdConsole.Instance.Output("*** ERROR "+error.Message);}
    }
}
internal static class RebirthProductiveEarnedNativeCodecScenario
{
    internal static JObject VerifyViewerSource(Guid operation,byte[] record,byte[] original,Func<byte[],byte[],bool> read)
    {
        if(record==null||original==null||record.Length<140||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*record.Length+8L*original.Length+65536),out var memory))throw new InvalidDataException("Actual native viewer source unavailable");
        using(memory)
        {
        if(!read(record,original))throw new InvalidDataException("Actual native viewer source unavailable");
        var assertions=new JArray("canonical-actual-native-viewer-source");
        foreach(var entry in new[]{new{Offset=0,Name="changed-version"},new{Offset=5,Name="foreign-original-digest"}})
        {var changed=(byte[])record.Clone();changed[entry.Offset]^=1;if(read(changed,original))throw new InvalidDataException(entry.Name);assertions.Add(entry.Name);}
        var foreign=(byte[])original.Clone();foreign[foreign.Length-1]^=1;
        if(read(record,foreign))throw new InvalidDataException("Foreign original accepted");assertions.Add("foreign-original-image");
        var truncated=record.Take(record.Length-1).ToArray();var trailing=new byte[record.Length+1];Buffer.BlockCopy(record,0,trailing,0,record.Length);
        if(read(truncated,original)||read(trailing,original))throw new InvalidDataException("Noncanonical native viewer source length");
        assertions.Add("truncated");assertions.Add("trailing-bytes");
        if(read(Array.Empty<byte>(),original))throw new InvalidDataException("Empty native viewer source accepted");assertions.Add("empty-source");
        return new JObject{["schema"]=1,["operation"]=operation.ToString("N"),["codec"]="viewerSource",["readOnly"]=true,["actualRetainedNative"]=true,["passed"]=true,["assertions"]=assertions};
        }
    }
    internal static JObject Verify(Guid operation,string mode,byte[] original,Func<byte[],bool> read)
    {
        if(original==null||original.Length<68||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(3L*original.Length+65536),out var held))throw new InvalidDataException("Actual codec observation bound");
        using(held)
        {
        if(!read(original))throw new InvalidDataException("Actual canonical production plan refused");
        var assertions=new JArray("canonical-actual-native-plan");
        foreach(var entry in new[]{new{Offset=0,Name="changed-version"},new{Offset=4,Name="foreign-operation"},new{Offset=20,Name="foreign-native-birth"},new{Offset=36,Name="foreign-original-nonce"}})
        {
            var changed=(byte[])original.Clone();changed[entry.Offset]^=1;
            if(read(changed))throw new InvalidDataException(entry.Name+" accepted");assertions.Add(entry.Name);
        }
        var truncated=new byte[original.Length-1];Buffer.BlockCopy(original,0,truncated,0,truncated.Length);
        var trailing=new byte[original.Length+1];Buffer.BlockCopy(original,0,trailing,0,original.Length);
        if(read(truncated)||read(trailing))throw new InvalidDataException("Noncanonical length accepted");
        assertions.Add("truncated");assertions.Add("trailing-bytes");
        return new JObject{["schema"]=1,["operation"]=operation.ToString("N"),["codec"]=mode,["readOnly"]=true,["actualRetainedNative"]=true,["passed"]=true,["assertions"]=assertions};
        }
    }
}
internal static partial class RebirthProductiveEarnedRemoteClient
{
    internal static JObject NativeCodecScenario(Guid operation,string mode)
    {
        var matches=originals.Values.Where(value=>value.Frame.Operation==operation).ToArray();if(matches.Length!=1)return null;var source=matches[0];
        using(var callbacks=source.Memory?.TryEnterCallbacks())
        {
            if(callbacks==null||!source.Identity())return null;
            if(mode=="viewerSource"&&source.ViewerToolsOnly)return RebirthProductiveEarnedNativeCodecScenario.VerifyViewerSource(operation,Read(source.Path+".nativeViewerSource",RebirthProductiveEarnedRowProof.MaximumArtifactBytes),source.Image,
                (bytes,image)=>RebirthProductiveEarnedViewerToolSource.TryRead(null,image,bytes,source.Nonce,out _,false));
            if(mode=="output")return RebirthProductiveEarnedNativeCodecScenario.Verify(operation,mode,source.Output?.Record,
                bytes=>RebirthProductiveEarnedNativeOutputPlan.TryRead(bytes,source.Frame,source.Nonce,source.Digest,out _));
            if(mode=="pending")return RebirthProductiveEarnedNativeCodecScenario.Verify(operation,mode,source.Pending?.Record,
                bytes=>RebirthProductiveEarnedNativePendingPlan.TryRead(bytes,source.Frame,source.Nonce,source.Digest,EffectiveNativeOutput(source),source.Station.block,out _));
            if(mode=="tools")return RebirthProductiveEarnedNativeCodecScenario.Verify(operation,mode,source.Tools?.Record,
                bytes=>RebirthProductiveEarnedRemoteToolPlan.TryRead(bytes,operation,source.Frame.Birth,source.Nonce,source.Digest,source.Output.Plan.Native,source.Output.Plan.Offset,out _));
            return null;
        }
    }
}
internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static JObject NativeCodecScenario(Guid operation,string mode)
    {
        if(!pending.TryGetValue(operation,out var source))return null;
        using(var callbacks=source.Memory?.TryEnterCallbacks())
        {
            if(callbacks==null||!source.Current())return null;var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            if(mode=="viewerSource"&&source.ViewerToolsOnly)return RebirthProductiveEarnedNativeCodecScenario.VerifyViewerSource(operation,source.ViewerToolsRecord,source.Image,
                (bytes,image)=>RebirthProductiveEarnedViewerToolSource.TryRead(source.Artifact.OriginalPath,image,bytes,source.Nonce,out _));
            if(mode=="output")return RebirthProductiveEarnedNativeCodecScenario.Verify(operation,mode,source.NativeOutputRecord,
                bytes=>RebirthProductiveEarnedNativeOutputPlan.TryRead(bytes,frame,source.Nonce,Digest(source.Image),out _));
            if(mode=="pending")return RebirthProductiveEarnedNativeCodecScenario.Verify(operation,mode,source.NativePendingRecord,
                bytes=>RebirthProductiveEarnedNativePendingPlan.TryRead(bytes,frame,source.Nonce,Digest(source.Image),EffectiveNativeOutput(source),source.Station.block,out _));
            if(mode=="tools")return RebirthProductiveEarnedNativeCodecScenario.Verify(operation,mode,source.Tools?.Record,
                bytes=>RebirthProductiveEarnedRemoteToolPlan.TryRead(bytes,operation,frame.Birth,source.Nonce,Digest(source.Image),source.NativeOutput.Native,source.NativeOutput.Offset,out _));
            return null;
        }
    }
}
#endif
