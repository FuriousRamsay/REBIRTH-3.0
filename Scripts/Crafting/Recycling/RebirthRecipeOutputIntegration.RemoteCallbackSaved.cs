using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class RemoteCallbackOwnerCandidate
    {internal World World;internal XUiC_RecipeStack Card;internal string Path,Creation;internal int Deaths;}
    private sealed class RemoteCallbackOwnerCandidates
    {internal readonly Dictionary<Guid,RemoteCallbackOwnerCandidate> Values=new Dictionary<Guid,RemoteCallbackOwnerCandidate>();}
    private static readonly ConditionalWeakTable<EntityPlayerLocal,RemoteCallbackOwnerCandidates> remoteCallbackOwners=new ConditionalWeakTable<EntityPlayerLocal,RemoteCallbackOwnerCandidates>();
    private static void RegisterRemoteCallbackOwner(UiOutputOriginal original,byte[] returned)
    {
        if(original?.World.IsRemote()!=true||!original.Current()||!original.CallbackOwnerCurrent()||
            !MatchesRemotePrefixFile(original.Path+".native-continuation-returned",returned))return;
        lock(uiOwnerGate)
        {
            var candidates=remoteCallbackOwners.GetValue(original.Actor,_=>new RemoteCallbackOwnerCandidates());
            foreach(var stale in candidates.Values.Where(value=>!ReferenceEquals(value.Value.World,original.World)||value.Value.Creation!=original.Creation||value.Value.Deaths!=original.Deaths).Select(value=>value.Key).ToArray())candidates.Values.Remove(stale);
            candidates.Values[original.Operation]=new RemoteCallbackOwnerCandidate{World=original.World,Card=original.Scope.Card,Path=original.Path,Creation=original.Creation,Deaths=original.Deaths};
        }
    }
    // The intermediate insertion receipt remains immutable. A returned native
    // quest/cosmetic callback is qualified by the distinct actual final PDS save.
    private static void ObserveRemoteOutputCallbackOwnerSaved(RebirthWorkstationCompletionPipeline.AdditionalOutputOwnerSaved saved)
    {
        if(saved?.Current()!=true)return;
        var observed=saved.Observation;var request=saved.Request;var ack=saved.Acknowledgment;
        var ownerFrame=request.Element("nativeOwnerFrame");
        if(observed?.Viewer==null||!observed.World.IsRemote()||ownerFrame==null)return;
        lock(uiOwnerGate)
        {
        var paths=new HashSet<string>();
        if(uiOwnerPrefixes.TryGetValue(observed.Viewer,out var retained))foreach(var value in retained.Values.Where(value=>value.Remote&&ReferenceEquals(value.Actor,observed.Viewer)&&ReferenceEquals(value.Card,observed.Card)))paths.Add(value.SourcePath);
        if(remoteCallbackOwners.TryGetValue(observed.Viewer,out var callbacks))foreach(var value in callbacks.Values.Values.Where(value=>ReferenceEquals(value.World,observed.World)&&ReferenceEquals(value.Card,observed.Card)&&value.Creation==observed.ViewerCreation&&value.Deaths==observed.ViewerDeaths))paths.Add(value.Path);
        foreach(string path in paths)
        {
            if(!ReadPersonalOutputSidecar(path,out var image,out var original)||
                (int?)original.Attribute("callbackFence")!=1||!bool.TryParse((string)original.Attribute("cached"),out _)||
                (string)original.Attribute("owner")!=observed.ViewerOwnerKey||(string)original.Attribute("creation")!=observed.ViewerCreation||
                (int?)original.Attribute("deaths")!=observed.ViewerDeaths||
                !Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation))continue;
            string hash=UiOriginalHash(image);var remote=original.Element("remoteOutputSource");
            if((bool?)remote?.Attribute("cached")!=false||(string)remote.Attribute("operation")!=observed.Operation.ToString("N")||
                (string)remote.Attribute("nonce")!=(string)ack.Attribute("nativeNonce")||(string)remote.Attribute("source")!=(string)ack.Attribute("nativeSource"))continue;
            int count=RebirthCamperInventorySnapshot.Restore(PersonalColdImage(original,"outputs")).Length;
            var localPrefix=request.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix");
            if(count<1||count>65||!ReadPersonalOutputSidecar(path+".native-continuation-returned",out var returnedBytes,out var returned)||
                !PersonalColdIdentity(returned,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",operation,hash)||
                (int?)returned.Attribute("outputPrefix")!=count||
                !ReadPersonalOutputSidecar(path+".output-"+(count-1)+".accepted",out var acceptedBytes,out var accepted)||
                !PersonalColdIdentity(accepted,"recipeOutputAccepted","operation,index,attempt,original","bag,belt,equipment",operation,hash)||
                (int?)accepted.Attribute("index")!=count-1||
                (string)localPrefix?.Attribute("operation")!=operation.ToString("N")||(string)localPrefix?.Attribute("original")!=hash||
                (int?)localPrefix?.Attribute("count")!=count||(string)localPrefix?.Attribute("accepted")!=UiOriginalHash(acceptedBytes)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(original.Element("recipe").Value,out var rows)||rows.Length!=1||rows[0]?.Recipe==null||
                !TryReadScrapCompletedEquipment(path,operation,hash,rows[0].Recipe,count,PersonalColdImage(accepted,"equipment"),out var equipment)||
                (string)ownerFrame.Element("bag")!=Convert.ToBase64String(PersonalColdImage(accepted,"bag"))||
                (string)ownerFrame.Element("belt")!=Convert.ToBase64String(PersonalColdImage(accepted,"belt"))||
                (string)ownerFrame.Element("equipment")!=Convert.ToBase64String(equipment)||!saved.Current())continue;
            var record=new XElement("recipeOutputRemoteCallbackOwnerSaved",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("original",hash),new XAttribute("returned",UiOriginalHash(returnedBytes)),new XAttribute("accepted",UiOriginalHash(acceptedBytes)),
                new XAttribute("outputPrefix",count),new XAttribute("nativeOperation",observed.Operation.ToString("N")),
                new XAttribute("nonce",(string)ack.Attribute("nativeNonce")),new XAttribute("source",(string)ack.Attribute("nativeSource")),
                new XAttribute("generation",(string)request.Attribute("generation")),new XAttribute("digest",(string)ack.Attribute("digest")),
                new XAttribute("nativeDigest",(string)request.Attribute("pdsDigest")),new XAttribute("nativeLength",(int)request.Attribute("pdsLength")),
                new XElement("request",request),new XElement("acknowledgment",ack));
            if(saved.Current()&&PublishPersonalColdBinding(path+".native-callback-owner-saved",UiOriginalBytes(record)))callbacks?.Values.Remove(operation);
        }
        }
    }
}
