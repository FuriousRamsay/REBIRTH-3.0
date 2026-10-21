using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RebirthCookingLegacyPull
{
    internal static bool MatchesLocalTerminal(EntityPlayerLocal player,long request,string sourceWorld,string sourceOperation,string sourceBinding,
        string child,string gridHash,string ownerProof,string disposition)
    {
        try
        {
            if(player?.world?.IsRemote()!=false||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)||
                !CurrentPullSession(new Original{Player=player,World=player.world,Owner=identity.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(player)}))return false;
            var record=Requests().Read(Operation(player,identity.CanonicalId,request));
            if(record==null||record.OwnerCreation!=RebirthCamperCharacterAccess.CurrentCreation(player)||!record.Before.SequenceEqual(record.After))return false;
            var root=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),4*1024*1024);
            if((long?)root.Attribute("request")!=request||(string)root.Attribute("world")!=player.world.worldState.Guid||(string)root.Attribute("owner")!=identity.CanonicalId)return false;
            var needs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("needs").Value));
            if(!RebirthRemoteResourceDurableDispatch.TryReadCookingTerminal(player,request,needs,out var terminal)||terminal.Phase!=4||
                terminal.World!=sourceWorld||terminal.Operation.ToString("N")!=sourceOperation||terminal.Binding!=sourceBinding)return false;
            var outcome=terminal.Copy();var action=outcome.Element("ownerAction");
            return action!=null&&(string)action.Attribute("domain")=="cooking-grid"&&(string)action.Attribute("child")==child&&
                RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting))==gridHash&&
                (string)outcome.Attribute("ownerProof")==ownerProof&&(string)outcome.Attribute("disposition")==disposition;
        }
        catch{return false;}
    }
    // The actual host-local primary uses the same saved source coordinator and
    // original result/grid handshake as a remote owner. No local TryConsume
    // receipt is elevated into a saved withdrawal or a replacement grant.
    private static void SendLocalOriginal(Original original)
    {
        if(!CurrentPullSession(original)||original.World.IsRemote())return;
        RebirthRemoteResourceDurableDispatch.Install();
        bool metadata=original.MetadataOnly||!CurrentDeliverySession(original);
        RebirthRemoteResourceDurableJournal.Record outcome;
        bool known=metadata?RebirthRemoteResourceDurableDispatch.TryReadCookingMetadata(original.Player,original.Request,original.Needs,out outcome):
            RebirthRemoteResourceDurableDispatch.TryReadCookingReplay(original.Player,original.Request,original.Needs,out outcome);
        if(!known)
        {
            if(metadata||original.Deaths<0||original.Deaths!=original.Player.Died||
                original.Needs.Any(item=>item.count>10000||!RebirthCookingCatalogue.IsIngredient(item)))return;
            if(!RebirthRemoteResourceDurableDispatch.Resolve(original.Player,1,(ulong)original.Request,RemoteResourceClientOperation.CookingPull,original.Needs,false,out outcome)||outcome.Phase<2)return;
        }
        if(!CurrentPullSession(original))return;
        var result=outcome.Copy();bool terminal=outcome.Phase==4;
        var items=terminal?new List<ItemStack>():RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(result.Element("removed").Value)).ToList();
        string child="",hash="";
        if(terminal&&result.Element("ownerAction") is XElement action)
        {
            if(action.Name!="ownerAction"||(string)action.Attribute("domain")!="cooking-grid"||
                !Guid.TryParseExact((string)action.Attribute("child"),"N",out var nativeChild)||nativeChild==Guid.Empty)return;
            child=nativeChild.ToString("N");hash=RebirthRemoteResourceDurableJournal.Digest(action.ToString(SaveOptions.DisableFormatting));
        }
        Receive(original.Request,items,(int)result.Attribute("outcome")==1?"":result.Element("reason").Value,
            outcome.World,outcome.Operation.ToString("N"),outcome.Binding,terminal,
            (int)result.Attribute("outcome")==2&&(result.Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0)||RebirthRemoteResourceDurableOutcome.IsRestoredCookingRefusal(outcome)),
            child,hash,(string)result.Attribute("ownerProof"),(string)result.Attribute("disposition"));
    }
}
