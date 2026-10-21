using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // Follow existing positive no-effect links for a viewer's terminal query.
    // This method never creates an attempt or grants an action/owner permit.
    private static bool TryResolveViewedAttempt(World world,XElement request,out Guid attempt)
    {
        attempt=Guid.Empty;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
            !Guid.TryParseExact((string)request.Attribute("operation"),"N",out attempt)||attempt==Guid.Empty)return false;
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");var visited=new System.Collections.Generic.HashSet<Guid>();
        for(int depth=0;depth<32;depth++)
        {
            if(!visited.Add(attempt))return false;
            string path=Path.Combine(root,attempt.ToString("N")+".retry.xml");
            if(ArtifactNodeExists(path+".tmp")||ArtifactNodeExists(path+".bak"))return false;
            if(!ArtifactNodeExists(path))return true;
            long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(24L*length+65536),out var memory))return false;
            using(memory)
            {
                var link=RebirthWorkstationCompletionPublication.ReadPrimary(path);var retry=link?.Element("recyclerUnusedRetry");var before=link?.Element("before")?.Elements().SingleOrDefault();
                if(link?.Name!="workstationCompletionIntent"||(string)link.Attribute("operation")!=attempt.ToString("N")||
                    (string)link.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||before?.Name!="placedRecyclerOriginal"||
                    (string)before.Attribute("operation")!=attempt.ToString("N")||(string)before.Attribute("purpose")!="complete"||
                    (string)before.Attribute("world")!=(string)link.Attribute("world")||(string)before.Attribute("account")!=(string)request.Attribute("recipientOwner")||
                    (string)before.Attribute("creation")!=(string)request.Attribute("recipientCreation")||retry==null||retry.Attributes().Count()!=2||retry.Nodes().Any()||
                    !Guid.TryParseExact((string)retry.Attribute("next"),"N",out var next)||next==Guid.Empty)return false;
                foreach(string name in new[]{"x","y","z"})if((string)before.Attribute(name)!=(string)request.Attribute(name))return false;
                string unusedPath=Path.Combine(root,attempt.ToString("N")+".unused-admission.xml");bool remote=false;
                if(!ArtifactNodeExists(unusedPath))
                {
                    if(ArtifactNodeExists(unusedPath+".tmp")||ArtifactNodeExists(unusedPath+".bak"))return false;
                    unusedPath=Path.Combine(root,attempt.ToString("N")+".remote-unused-host.xml");remote=true;
                }
                long unusedLength=RebirthWorkstationCompletionPublication.PublishedLength(unusedPath);
                if(unusedLength<1||unusedLength>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(12L*(length+unusedLength)+65536)))return false;
                var unused=RebirthWorkstationCompletionPublication.ReadPrimary(unusedPath);var outcome=unused?.Element(remote?"recyclerRemoteUnusedSaved":"recyclerUnusedAdmission");
                if(unused?.Name!="workstationCompletionIntent"||(string)unused.Attribute("operation")!=attempt.ToString("N")||
                    (string)unused.Attribute("world")!=(string)link.Attribute("world")||!XNode.DeepEquals(unused.Element("before"),link.Element("before"))||
                    (string)retry.Attribute("unused")!=RebirthWorkstationCompletionOriginal.Hash(unused.ToString(SaveOptions.DisableFormatting))||outcome==null)return false;
                if(remote)
                {
                    if((bool?)outcome.Attribute("principalAccepted")!=false||(bool?)outcome.Attribute("ownerSlotsAccepted")!=false||
                        (bool?)outcome.Attribute("metadataMarkerEntered") is not bool entered||entered&&outcome.Element("recyclerCleanupSaved")==null)return false;
                }
                else if(outcome.Attributes().Count()!=2||(bool?)outcome.Attribute("nativeStationEntered")!=false||
                    (bool?)outcome.Attribute("nativeOwnerEntered")!=false||outcome.Nodes().Any())return false;
                attempt=next;
            }
        }
        return false;
    }
    // Retry links select a new original identity only. Every new attempt still
    // captures the real current action and both full native preimages. Neither
    // an unused receipt nor a retry link is a setter/publication permission.
    private static bool TrySelectAuthorityAttempt(EntityPlayerLocal actor,TileEntityWorkstation station,Guid requested,string purpose,out Guid attempt)
    {
        attempt=requested;
        if(!ThreadManager.IsMainThread()||actor?.world==null||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
        return TrySelectAuthorityAttemptCore(actor,station,requested,purpose,identity,out attempt);
    }
    private static bool TrySelectServerCompletionAttempt(EntityPlayer actor,TileEntityWorkstation station,Guid requested,out Guid attempt)
    {
        attempt=requested;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;
        return TrySelectAuthorityAttemptCore(actor,station,requested,"complete",identity,out attempt);
    }
    private static bool TrySelectAuthorityAttemptCore(EntityPlayer actor,TileEntityWorkstation station,Guid requested,string purpose,RebirthStablePlayerIdentity identity,out Guid attempt)
    {
        attempt=requested;
        try
        {
        if(!ThreadManager.IsMainThread()||actor?.world==null||station==null||requested==Guid.Empty||!actor.IsSpawned()||actor.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||identity==null||
            !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))return false;
        string root=RebirthWorkstationCompletionOutbox.Root("recyclerPlaced");
        var visited=new System.Collections.Generic.HashSet<Guid>();
        for(int depth=0;depth<32;depth++)
        {
            if(!visited.Add(attempt))return false;
            // A retained original always wins over metadata. A retry cannot
            // sidestep an in-flight or uncertain native action.
            Guid currentAttempt=attempt;
            if(authorityOriginals.Any(value=>value.Source?.Operation==currentAttempt)||recyclerClients.Any(value=>value.Source?.Operation==currentAttempt)||
                remoteRecyclerOriginals.ContainsKey(currentAttempt)||closedAuthorityCompletions.Any(value=>value.Operation==currentAttempt))return true;
            string unusedPath=Path.Combine(root,attempt.ToString("N")+".unused-admission.xml");
            bool remoteUnused=false;
            if(!ArtifactNodeExists(unusedPath)&&!actor.world.IsRemote()&&purpose=="complete")
            {
                if(ArtifactNodeExists(unusedPath+".tmp")||ArtifactNodeExists(unusedPath+".bak"))return false;
                unusedPath=Path.Combine(root,attempt.ToString("N")+".remote-unused-host.xml");remoteUnused=true;
            }
            if(!ArtifactNodeExists(unusedPath))return !ArtifactNodeExists(unusedPath+".tmp")&&!ArtifactNodeExists(unusedPath+".bak");
            long length=RebirthWorkstationCompletionPublication.PublishedLength(unusedPath);
            if(length<1||length>24*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var memory))return false;
            using(memory)
            {
                var record=RebirthWorkstationCompletionPublication.ReadPrimary(unusedPath);
                var source=record?.Element("before")?.Elements().SingleOrDefault();var unused=record?.Element(remoteUnused?"recyclerRemoteUnusedSaved":"recyclerUnusedAdmission");
                if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=attempt.ToString("N")||
                    (string)record.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(actor.world)||
                    source?.Name!="placedRecyclerOriginal"||(string)source.Attribute("operation")!=attempt.ToString("N")||
                    (string)source.Attribute("purpose")!=purpose||(string)source.Attribute("owner")!=identity.StorageKey||
                    (string)source.Attribute("account")!=identity.CanonicalId||(string)source.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                    (int?)source.Attribute("deaths")!=actor.Died||(int?)source.Attribute("x")!=station.ToWorldPos().x||
                    (int?)source.Attribute("y")!=station.ToWorldPos().y||(int?)source.Attribute("z")!=station.ToWorldPos().z||
                    unused==null)return false;
                if(remoteUnused)
                {
                    if((bool?)unused.Attribute("principalAccepted")!=false||(bool?)unused.Attribute("ownerSlotsAccepted")!=false||
                        !Guid.TryParseExact((string)unused.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||
                        (bool?)unused.Attribute("metadataMarkerEntered") is not bool entered||entered&&unused.Element("recyclerCleanupSaved")==null)return false;
                }
                else if(unused.Attributes().Count()!=2||(bool?)unused.Attribute("nativeStationEntered")!=false||
                    (bool?)unused.Attribute("nativeOwnerEntered")!=false||unused.Nodes().Any())return false;
                string linkPath=Path.Combine(root,attempt.ToString("N")+".retry.xml");
                var link=ArtifactNodeExists(linkPath)?RebirthWorkstationCompletionPublication.ReadPrimary(linkPath):null;
                if(link==null)
                {
                    if(ArtifactNodeExists(linkPath)||ArtifactNodeExists(linkPath+".tmp")||ArtifactNodeExists(linkPath+".bak"))return false;
                    var next=Guid.NewGuid();
                    link=new XElement("workstationCompletionIntent",new XAttribute("operation",attempt.ToString("N")),new XAttribute("world",(string)record.Attribute("world")),
                        new XElement("before",new XElement(source)),
                        new XElement("recyclerUnusedRetry",new XAttribute("next",next.ToString("N")),new XAttribute("unused",RebirthWorkstationCompletionOriginal.Hash(record.ToString(SaveOptions.DisableFormatting)))));
                    if(!RebirthWorkstationCompletionPublication.Write(linkPath,link)||!XNode.DeepEquals(link,RebirthWorkstationCompletionPublication.ReadPrimary(linkPath)))return false;
                }
                var retry=link.Element("recyclerUnusedRetry");
                if(link.Name!="workstationCompletionIntent"||(string)link.Attribute("operation")!=attempt.ToString("N")||
                    (string)link.Attribute("world")!=(string)record.Attribute("world")||retry==null||retry.Nodes().Any()||retry.Attributes().Count()!=2||
                    !XNode.DeepEquals(link.Element("before"),record.Element("before"))||
                    (string)retry.Attribute("unused")!=RebirthWorkstationCompletionOriginal.Hash(record.ToString(SaveOptions.DisableFormatting))||
                    !Guid.TryParseExact((string)retry.Attribute("next"),"N",out var selected)||selected==Guid.Empty||
                    !XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(unusedPath)))return false;
                attempt=selected;
            }
        }
        return false;
        }
        catch{return false;}
    }
}
