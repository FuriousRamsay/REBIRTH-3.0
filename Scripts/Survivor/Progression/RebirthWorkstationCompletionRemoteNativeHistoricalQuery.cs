using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static NativeHostOriginal preparingNativeRetirement;
    // Resource retirement is called only by the positive saved group successor.
    // This record is a historical answer, never a source/action reconstruction.
    internal static bool TryRetireRemoteNativeOriginal(NativeHostOriginal source)
    {if(!TryPrepareRetiredRemoteNativeOriginal(source,out var prepared))return false;prepared.Retire();return true;}
    internal sealed class NativeRetirement
    {
        private NativeHostOriginal source;
        internal NativeHostOriginal Original=>source;
        private NativeRetirement(NativeHostOriginal value){source=value;}
        internal static NativeRetirement Issue(NativeHostOriginal value)=>ReferenceEquals(preparingNativeRetirement,value)?new NativeRetirement(value):null;
        // Every group member is prepared before parent enters this resource
        // release phase. No I/O or financial qualification can fail midway.
        internal void Retire(){var value=source;source=null;value?.Dispose();}
    }
    internal static bool TryPrepareRetiredRemoteNativeOriginal(NativeHostOriginal source,out NativeRetirement prepared)
        =>TryPrepareRetiredRemoteNativeOriginalCore(source,()=>source?.RemoteOwnerTerminalSaved==true,out prepared);
    internal static bool TryPrepareRetiredRemoteNativeOriginal(NativeHostOriginal source,CachedPhysicalPublication successor,out NativeRetirement prepared)
        =>TryPrepareRetiredRemoteNativeOriginalCore(source,()=>successor?.CurrentSavedSource()==true&&
            successor.Previous?.ContainsSavedNativeIdentity(source)==true&&source.RemoteOwnerTerminalHistoricalSaved,out prepared);
    private static bool TryPrepareRetiredRemoteNativeOriginalCore(NativeHostOriginal source,Func<bool> qualified,out NativeRetirement prepared)
    {
        prepared=null;
        if(source==null||!ThreadManager.IsMainThread()||!qualified())return false;
        try
        {
            long originalLength=RebirthWorkstationCompletionPublication.PublishedLength(source.Path);
            long terminalLength=RebirthWorkstationCompletionPublication.PublishedLength(source.Path+".owner-terminal");
            if(originalLength<1||terminalLength<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(
                checked(16*(originalLength+terminalLength)+262144),out var workspace))return false;
            using(workspace)
            {
                var original=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.Path);
                var terminal=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.Path+".owner-terminal");
                if(original==null||terminal==null||HistoricalHash(original)!=source.SourceHash||!qualified())return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),
                    new XAttribute("world",source.WorldGuid),new XElement("before",new XElement(source.Before)),
                    new XElement("remoteNativeHistoricalTerminal",new XAttribute("version",1),
                        new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("original",source.SourceHash),
                        new XAttribute("terminal",HistoricalHash(terminal)),new XAttribute("viewer",source.ViewerKey),
                        new XAttribute("creation",source.ViewerCreation),new XAttribute("owner",source.EarlierOwner.OriginalOwnerKey),
                        new XAttribute("ownerCreation",source.EarlierOwner.Creation)));
                string path=source.Path+".query-terminal";
                if(!RebirthWorkstationCompletionPublication.Write(path,record))return false;
                var read=RebirthWorkstationCompletionPublication.ReadPrimary(path);
                if(read==null||!XNode.DeepEquals(read,record)||!qualified())return false;
                preparingNativeRetirement=source;
                try{prepared=NativeRetirement.Issue(source);}finally{preparingNativeRetirement=null;}
                return prepared!=null;
            }
        }
        catch{return false;}
    }
    private static string HistoricalHash(byte[] bytes)=>RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(bytes));
    private static void QueryHistoricalRemoteNativeOwner(EntityPlayer viewer,string key,string creation,Guid operation,XElement request)
    {
        // Caller has already authenticated the actual native sender. A new
        // session may retrieve metadata, but cannot recover the old source.
        if(operation==Guid.Empty||viewer==null||string.IsNullOrEmpty(key)||string.IsNullOrEmpty(creation))return;
        try
        {
            string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",operation.ToString("N")+".xml");
            long a=RebirthWorkstationCompletionPublication.PublishedLength(path),b=RebirthWorkstationCompletionPublication.PublishedLength(path+".owner-terminal"),
                c=RebirthWorkstationCompletionPublication.PublishedLength(path+".query-terminal");
            if(a<1||b<1||c<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*(a+b+c)+262144),out var workspace))return;
            using(workspace)
            {
                var original=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                var terminalBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".owner-terminal");
                var primary=RebirthWorkstationCompletionPublication.ReadPrimary(path);
                var terminal=RebirthWorkstationCompletionPublication.ReadPrimary(path+".owner-terminal");
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(path+".query-terminal");
                var historical=record?.Element("remoteNativeHistoricalTerminal");
                var native=primary?.Element("before")?.Element("remoteNativeOriginal");
                var completed=terminal?.Element("remoteNativeOwnerTerminal");
                string purposeText=(string)native?.Attribute("ownerPurpose");
                if(string.IsNullOrEmpty(purposeText)||purposeText.Length>65536)return;
                XElement purpose;
                using(var stream=new System.IO.StringReader(purposeText))
                using(var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{
                    DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536,MaxCharactersFromEntities=1}))
                    purpose=XElement.Load(reader);
                string op=operation.ToString("N"),world=RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world);
                if(original==null||terminalBytes==null||historical==null||native==null||completed==null||
                    (string)historical.Attribute("version")!="1"||(string)record.Attribute("operation")!=op||
                    (string)primary.Attribute("operation")!=op||(string)terminal.Attribute("operation")!=op||
                    (string)record.Attribute("world")!=world||(string)primary.Attribute("world")!=world||(string)terminal.Attribute("world")!=world||
                    (string)historical.Attribute("viewer")!=key||(string)native.Attribute("viewer")!=key||
                    (string)historical.Attribute("creation")!=creation||(string)native.Attribute("creation")!=creation||
                    purpose.Name!="queueOwnerPurpose"||(string)historical.Attribute("owner")!=(string)purpose.Attribute("owner")||
                    (string)historical.Attribute("ownerCreation")!=(string)purpose.Attribute("creation")||
                    (string)historical.Attribute("nonce")!=(string)request.Attribute("nonce")||
                    (string)native.Attribute("nonce")!=(string)request.Attribute("nonce")||
                    (string)completed.Attribute("nonce")!=(string)request.Attribute("nonce")||
                    (string)historical.Attribute("original")!=(string)request.Attribute("source")||
                    (string)completed.Attribute("original")!=(string)request.Attribute("source")||
                    HistoricalHash(original)!=(string)request.Attribute("source")||HistoricalHash(terminalBytes)!=(string)historical.Attribute("terminal")||
                    !XNode.DeepEquals(record.Element("before"),terminal.Element("before"))||
                    !XNode.DeepEquals(terminal.Element("before")?.Element("nativeOwningBefore"),native.Element("nativeOwningBefore")))return;
                Send(false,new XElement("ack",new XAttribute("world",world),new XAttribute("viewerCreation",creation),
                    new XAttribute("stage","nativeOwnerTerminal"),new XAttribute("owner",(string)historical.Attribute("owner")??""),
                    new XAttribute("creation",(string)historical.Attribute("ownerCreation")??""),new XAttribute("digest",(string)request.Attribute("source")),
                    new XAttribute("nativeNonce",(string)request.Attribute("nonce")),new XAttribute("nativeSource",(string)request.Attribute("source")),
                    new XAttribute("challenge",(string)request.Attribute("challenge")??"")),operation,viewer.entityId);
            }
        }
        catch{} // Missing/changed historical evidence is unknown, never permission.
    }
}
