using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static bool BindRemoteOutputPrefixContinuation(RemoteOutputHostSource source)
    {
        var root=source.RootBefore;var active=source.Before;
        var effect=source.Request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
        if(effect==null&&source.Request.Element("deferredNativeContribution")!=null)return BindRemoteDeferredOutputPrefix(source);
        var first=effect?.Element("initialViewerBefore")?.Elements().SingleOrDefault();
        string generation=(string)source.Request.Attribute("generation"),firstGeneration=(string)first?.Attribute("generation");
        if(effect==null||first==null||!RebirthWorkstationCompletionOriginal.ValidKey(firstGeneration)||
            !XNode.DeepEquals(effect.Element("initialBefore")?.Elements().SingleOrDefault(),root))return false;
        source.FinalBeforeGeneration=firstGeneration;
        if(XNode.DeepEquals(active,root))return firstGeneration==generation;
        if(!TryReadRemoteOutputFinalContinuation(source.Path,source.Operation,source.Nonce,source.Hash,root,active,
            out var path,out var record,out var saved,out var image,out var savedImage))return false;
        var dependencies=new Dictionary<string,byte[]>();
        XElement firstSaved=null,activeSaved=null;
        foreach(string current in new[]{firstGeneration,generation}.Distinct())
        {
            string prefix=source.Path+".viewer."+current+".before",inventoryPath=source.Path+".viewer."+current+".inventory-before";
            var request=RemoteOutputXml(prefix+".intent")?.Element("before")?.Element("nativeViewer");
            var receipt=RemoteOutputXml(prefix)?.Element("before")?.Element("nativeViewerSaved");
            var inventory=RemoteOutputXml(inventoryPath)?.Element("before")?.Element("nativeViewerInventoryBeforeAccepted");
            if(request==null||receipt==null||inventory==null||(string)request.Attribute("stage")!="before"||
                (string)request.Attribute("generation")!=current||(string)request.Attribute("nonce")!=source.Nonce.ToString("N")||
                (string)request.Attribute("source")!=source.Hash||(string)receipt.Attribute("generation")!=current||
                (string)receipt.Attribute("nonce")!=source.Nonce.ToString("N")||(string)receipt.Attribute("source")!=source.Hash||
                (string)receipt.Attribute("digest")!=RemoteOutputRequestDigest(request)||(string)receipt.Attribute("retainedNative")!=prefix+".native"||
                !XNode.DeepEquals(request.Element("activeBefore")?.Elements().SingleOrDefault(),current==firstGeneration?root:active)||
                !XNode.DeepEquals(receipt.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame"))||
                !XNode.DeepEquals(inventory.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame")))return false;
            if(current==firstGeneration){if(!XNode.DeepEquals(request,first))return false;firstSaved=receipt;}else activeSaved=receipt;
            foreach(string file in new[]{prefix+".intent",prefix,inventoryPath})
            {var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(file);if(bytes==null)return false;dependencies.Add(file,bytes);}
        }
        if(firstSaved==null||activeSaved==null)return false;
        dependencies.Add(path,image);dependencies.Add(path+".saved",savedImage);
        source.FinalContinuationPath=path;source.FinalContinuationRecord=record;source.FinalContinuationSavedRecord=saved;
        source.FinalContinuationImage=image;source.FinalContinuationSavedImage=savedImage;source.FinalDependencies=dependencies;
        source.FinalBeforeNativePath=(string)firstSaved.Attribute("retainedNative");source.FinalBeforeNativeHash=(string)firstSaved.Attribute("nativeDigest");source.FinalBeforeNativeLength=(int)firstSaved.Attribute("nativeLength");
        source.FinalActiveBeforeNativePath=(string)activeSaved.Attribute("retainedNative");source.FinalActiveBeforeNativeHash=(string)activeSaved.Attribute("nativeDigest");source.FinalActiveBeforeNativeLength=(int)activeSaved.Attribute("nativeLength");
        return true;
    }
    // Registers only a causal full-body candidate for the real native load
    // observer. Files and the returned body alone cannot create a live Add
    // receipt, owner grant, XP effect or physical output permission.
    private static bool BindRemoteDeferredOutputPrefix(RemoteOutputHostSource source,XElement retainedPrefix=null)
    {
        var root=source.RootBefore;var active=source.Before;
        string generation=(string)source.Request.Attribute("generation");
        if(!XNode.DeepEquals(root,active))return BindRemoteDeferredOutputContinuation(source,retainedPrefix??source.Request.Element("deferredNativeContribution"));
        if(generation!=RebirthWorkstationCompletionOriginal.Hash(root.ToString(SaveOptions.DisableFormatting)))return false;
        string beforePath=source.Path+".viewer."+generation+".before",inventoryPath=source.Path+".viewer."+generation+".inventory-before";
        var request=RemoteOutputXml(beforePath+".intent")?.Element("before")?.Element("nativeViewer");
        var receipt=RemoteOutputXml(beforePath)?.Element("before")?.Element("nativeViewerSaved");
        var inventory=RemoteOutputXml(inventoryPath)?.Element("before")?.Element("nativeViewerInventoryBeforeAccepted");
        var retained=RemoteOutputXml(source.Path+".contribution");
        var contribution=retained?.Element("before")?.Element("deferredNativeContribution");
        var prefix=retainedPrefix??source.Request.Element("deferredNativeContribution");
        if(request==null||receipt==null||inventory==null||contribution==null||prefix==null||
            (string)request.Attribute("stage")!="before"||(string)request.Attribute("generation")!=generation||
            (string)request.Attribute("nonce")!=source.Nonce.ToString("N")||(string)request.Attribute("source")!=source.Hash||
            !XNode.DeepEquals(request.Element("activeBefore")?.Elements().SingleOrDefault(),root)||
            (string)receipt.Attribute("generation")!=generation||(string)receipt.Attribute("nonce")!=source.Nonce.ToString("N")||
            (string)receipt.Attribute("source")!=source.Hash||(string)receipt.Attribute("digest")!=RemoteOutputRequestDigest(request)||
            (string)receipt.Attribute("retainedNative")!=beforePath+".native"||
            !XNode.DeepEquals(receipt.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame"))||
            !XNode.DeepEquals(inventory.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame"))||
            (string)retained.Attribute("operation")!=source.Operation.ToString("N")||
            (string)retained.Attribute("world")!=(string)source.Original.Attribute("world")||(int?)contribution.Attribute("phase")!=1||
            (string)contribution.Attribute("operation")!=source.Operation.ToString("N")||
            (string)contribution.Attribute("nonce")!=source.Nonce.ToString("N")||(string)contribution.Attribute("birth")!=source.Birth.ToString("N")||
            (string)contribution.Attribute("original")!=source.Hash||contribution.Element("physicalDelivery")!=null||
            contribution.Element("activeBefore")!=null||contribution.Element("actualCachedSpaceTransfer")!=null)return false;
        var membership=new XElement(contribution);membership.Attribute("phase").Remove();
        if(!XNode.DeepEquals(membership,prefix))return false;
        byte[] before=Convert.FromBase64String(root.Element("nativeBefore").Value);
        int offset=(int)root.Attribute("nativeElapsedOffset"),index=(int)root.Attribute("slot");
        if(!RebirthProductiveEarnedNativeBody.TryRead(before,offset,out var native,out _)||index<0||index>=native.Queue.Length||
            native.Queue[index]?.Recipe==null||native.Queue[index].StartingEntityId!=(int)root.Attribute("startingOwner")||
            !RebirthProductiveEarnedUiDeferredTransport.TryDescribeRemoteNativeAddBody(native.Queue[index].Recipe,
                native.Queue[index].StartingEntityId,before,offset,contribution,out var body,out int elapsed))return false;
        var dependencies=source.FinalDependencies==null?new Dictionary<string,byte[]>():new Dictionary<string,byte[]>(source.FinalDependencies);
        foreach(string path in new[]{beforePath+".intent",beforePath,inventoryPath,source.Path+".contribution",source.Path+".saved"})
        {var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return false;
            if(dependencies.TryGetValue(path,out var existing)&&!existing.SequenceEqual(bytes))return false;dependencies[path]=bytes;}
        source.DeferredAddBody=body;source.DeferredAddOffset=elapsed;source.DeferredMembership=new XElement(membership);source.FinalDependencies=dependencies;source.FinalBeforeGeneration=generation;
        source.FinalBeforeNativePath=source.FinalActiveBeforeNativePath=(string)receipt.Attribute("retainedNative");
        source.FinalBeforeNativeHash=source.FinalActiveBeforeNativeHash=(string)receipt.Attribute("nativeDigest");
        source.FinalBeforeNativeLength=source.FinalActiveBeforeNativeLength=(int)receipt.Attribute("nativeLength");
        return true;
    }
}
