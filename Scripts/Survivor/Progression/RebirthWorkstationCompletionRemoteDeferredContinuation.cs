using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool SameDeferredNativeOutsideOutput(byte[] first,int firstOffset,byte[] last,int lastOffset)
    {
        if(!RebirthProductiveEarnedNativeBody.TryRead(first,firstOffset,out var before,out ulong elapsed)||
            !RebirthProductiveEarnedNativeBody.TryRead(last,lastOffset,out var after,out _))return false;
        before.Output=after.Output;
        var expected=RebirthProductiveEarnedNativeBody.Encode(before,elapsed,out int offset);
        return RebirthProductiveEarnedNativeBody.SameExceptElapsed(expected,offset,last,lastOffset);
    }
    private static bool BindRemoteDeferredOutputContinuation(RemoteOutputHostSource source,XElement membership)
    {
        if(membership==null||source.Request.Element("sameOwnerEffect")!=null)return false;
        var active=source.Before;var root=source.RootBefore;
        string generation=(string)source.Request.Attribute("generation");
        if(generation!=RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting))||
            !TryReadRemoteOutputFinalContinuation(source.Path,source.Operation,source.Nonce,source.Hash,root,active,
                out var phasePath,out var phase,out var phaseSaved,out var phaseBytes,out var phaseSavedBytes))return false;
        // Describe the original Add independently of the later active station.
        // Neither description issues a native load or a callback capability.
        var initialRequest=new XElement(source.Request);
        initialRequest.SetAttributeValue("generation",RebirthWorkstationCompletionOriginal.Hash(root.ToString(SaveOptions.DisableFormatting)));
        var origin=new RemoteOutputHostSource{Path=source.Path,Operation=source.Operation,Nonce=source.Nonce,Birth=source.Birth,
            Hash=source.Hash,Original=source.Original,RootBefore=root,Before=root,Request=initialRequest};
        if(!BindRemoteDeferredOutputPrefix(origin,membership))return false;
        byte[] body=Convert.FromBase64String(active.Element("nativeBefore").Value);int offset=(int)active.Attribute("nativeElapsedOffset");
        var transfer=phase.Element("before")?.Element("remoteNativeCachedBefore")?.Element("actualCachedSpaceTransfer");
        var prior=transfer?.Element("before");var after=transfer?.Element("after");
        if(prior==null||after==null||!SameDeferredNativeOutsideOutput(origin.DeferredAddBody,origin.DeferredAddOffset,body,offset)||
            !SameDeferredNativeOutsideOutput(Convert.FromBase64String(prior.Value),(int)prior.Attribute("offset"),
                Convert.FromBase64String(after.Value),(int)after.Attribute("offset")))return false;
        string beforePath=source.Path+".viewer."+generation+".before",inventoryPath=source.Path+".viewer."+generation+".inventory-before";
        var request=RemoteOutputXml(beforePath+".intent")?.Element("before")?.Element("nativeViewer");
        var receipt=RemoteOutputXml(beforePath)?.Element("before")?.Element("nativeViewerSaved");
        var inventory=RemoteOutputXml(inventoryPath)?.Element("before")?.Element("nativeViewerInventoryBeforeAccepted");
        if(request==null||receipt==null||inventory==null||(string)request.Attribute("stage")!="before"||
            (string)request.Attribute("generation")!=generation||(string)request.Attribute("nonce")!=source.Nonce.ToString("N")||
            (string)request.Attribute("source")!=source.Hash||!XNode.DeepEquals(request.Element("activeBefore")?.Elements().SingleOrDefault(),active)||
            (string)receipt.Attribute("digest")!=RemoteOutputRequestDigest(request)||(string)receipt.Attribute("nonce")!=source.Nonce.ToString("N")||
            (string)receipt.Attribute("source")!=source.Hash||(string)receipt.Attribute("generation")!=generation||
            (string)receipt.Attribute("retainedNative")!=beforePath+".native"||
            !XNode.DeepEquals(receipt.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame"))||
            !XNode.DeepEquals(inventory.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame")))return false;
        var dependencies=new Dictionary<string,byte[]>(origin.FinalDependencies);
        if(source.FinalDependencies!=null)foreach(var pair in source.FinalDependencies)
        {if(dependencies.TryGetValue(pair.Key,out var old)&&!old.SequenceEqual(pair.Value))return false;dependencies[pair.Key]=pair.Value;}
        foreach(string path in new[]{beforePath+".intent",beforePath,inventoryPath})
        {var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return false;dependencies[path]=bytes;}
        dependencies[phasePath]=phaseBytes;dependencies[phasePath+".saved"]=phaseSavedBytes;
        source.DeferredOriginAddBody=origin.DeferredAddBody;source.DeferredOriginAddOffset=origin.DeferredAddOffset;
        source.DeferredAddBody=body;source.DeferredAddOffset=offset;source.DeferredMembership=origin.DeferredMembership;
        source.FinalDependencies=dependencies;source.FinalBeforeGeneration=origin.FinalBeforeGeneration;
        source.FinalBeforeNativePath=origin.FinalBeforeNativePath;source.FinalBeforeNativeHash=origin.FinalBeforeNativeHash;source.FinalBeforeNativeLength=origin.FinalBeforeNativeLength;
        source.FinalActiveBeforeNativePath=(string)receipt.Attribute("retainedNative");source.FinalActiveBeforeNativeHash=(string)receipt.Attribute("nativeDigest");source.FinalActiveBeforeNativeLength=(int)receipt.Attribute("nativeLength");
        source.FinalContinuationPath=phasePath;source.FinalContinuationRecord=phase;source.FinalContinuationSavedRecord=phaseSaved;
        source.FinalContinuationImage=phaseBytes;source.FinalContinuationSavedImage=phaseSavedBytes;
        return true;
    }
}
