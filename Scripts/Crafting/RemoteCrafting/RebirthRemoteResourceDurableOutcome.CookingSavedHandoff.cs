using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceDurableOutcome
{
    // Retire only a previously admitted handoff whose exact after-effect native
    // receipt is saved. Later movement/death does not authorize actor replay.
    internal static bool TryCompleteSavedCookingHandoff(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original,
        out RebirthRemoteResourceDurableJournal.Record terminal)
    {
        terminal=null;
        try
        {
            if(original==null||original.Phase!=2&&original.Phase!=3||
                !TrySavedCookingHandoff(actor,original,out var child,out var body,out var expected,out var disposition))return false;
            var action=original.Copy().Element("ownerAction").ToString(SaveOptions.DisableFormatting);
            string proof=RebirthRemoteResourceDurableJournal.Digest(child.ToString("N")+"|2|"+expected+"|"+action);
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition",disposition);value.SetAttributeValue("ownerProof",proof);
            },()=>TrySavedCookingHandoff(actor,original,out var currentChild,out var currentBody,out var currentExpected,out var currentDisposition)&&
                currentChild==child&&currentBody==body&&currentExpected==expected&&currentDisposition==disposition,out terminal);
        }
        catch{return false;}
    }
    private static bool TrySavedCookingHandoff(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original,
        out Guid child,out string body,out string expected,out string disposition)
    {
        child=Guid.Empty;body=expected=disposition=null;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            !RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)||
            !RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||world!=original.World)return false;
        var network=RebirthRemoteResourceNetworkScope.Capture(actor);int currentDeaths=actor.Died;
        if(network?.Current!=true)return false;
        var root=original.Copy();int deaths=(int)root.Attribute("deaths");
        if((int)root.Attribute("kind")!=(int)RemoteResourceClientOperation.CookingPull||deaths<0||deaths>actor.Died||
            !RebirthWorldCharacterService.TryGetIdentity(actor,out var identity))return false;
        var action=root.Element("ownerAction");
        if(action==null||action.Attributes().Count()!=2||(string)action.Attribute("domain")!="cooking-grid"||action.Elements().Count()!=1||
            !Guid.TryParseExact((string)action.Attribute("child"),"N",out child)||child==Guid.Empty||
            action.Element("original")==null||action.Element("original").HasAttributes||action.Element("original").HasElements)return false;
        body=action.Element("original").Value;
        var image=RebirthCookingRemoteAdmission.ReadProtocolXml(body,8*1024*1024);
        int? handoffDeaths=(int?)image.Attribute("deaths"),sourceDeaths=(int?)image.Attribute("sourceDeaths");
        if(image.Name!="cookingGridOriginal"||(int?)image.Attribute("version")!=1||
            (long?)image.Attribute("fetchId") is not long request||request<=0||!handoffDeaths.HasValue||handoffDeaths.Value<0||handoffDeaths.Value>actor.Died||
            (sourceDeaths??handoffDeaths)!=deaths||sourceDeaths.HasValue&&((string)image.Attribute("action")!="fetched-return"||sourceDeaths.Value>=handoffDeaths.Value)||
            (string)image.Attribute("world")!=actor.world.worldState.Guid||(string)image.Attribute("owner")!=identity.CanonicalId||
            (string)image.Attribute("creation")!=original.Creation||
            !new[]{"fetched","fetched-return"}.Contains((string)image.Attribute("action")))return false;
        if(RebirthRemoteResourceDurableJournal.Operation(world,original.Owner,original.Creation,1,(ulong)request,RemoteResourceClientOperation.CookingPull)!=original.Operation)return false;
        if(!SameTotals(Totals(ReadSlots(root.Element("removed"))),Totals(ReadSlots(image.Element("fetched")))))return false;
        expected=image.Element("afterActor")?.Value;if(expected==null||expected.Length>6*1024*1024)return false;
        // Written after Apply; absent/older/unreadable receipts prove nothing.
        if(!RebirthCamperEnergySavedOwner.TryRead(identity,out _,out var file)||file.deaths!=actor.Died||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(file,"rbCookingGrid_"+child.ToString("N"),out float phase,out bool present)||!present||phase!=2f)return false;
        if(network.Current!=true||actor.Died!=currentDeaths||actor.IsDead()||
            !RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation))return false;
        disposition=(string)image.Attribute("action")=="fetched"?"grid-owned":"refunded";return true;
    }
}
