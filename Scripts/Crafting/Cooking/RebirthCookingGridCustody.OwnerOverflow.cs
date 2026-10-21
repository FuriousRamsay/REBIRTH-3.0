using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthCookingGridCustody
{
    private static ItemStack[] OwnerOverflow(RebirthCamperTransferJournal.Record record)
    {
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));var image=root.Element("overflow");if(image==null)return null;
        var items=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image.Value));
        if((string)root.Attribute("action")!="return"||items.Length<1||items.Length>255||items.Any(item=>item==null||item.IsEmpty())||root.Attribute("overflowEntity")==null)throw new InvalidDataException();return items;
    }
    private static bool OwnerReturnMarker(Intent intent)=>Current(intent)&&RebirthStablePlayerIdentity.TryResolveServerEntity(intent.Player,out var owner)&&
        RebirthCamperEnergySavedOwner.TryRead(owner,out _,out var file)&&file.id==intent.Player.entityId&&file.deaths==intent.Deaths&&
        RebirthCookingRemoteAdmissionBackend.NativePhase(file,Receipt(intent.Operation),out float phase,out bool present)&&present&&phase==2f;
    private static bool ProjectReturnGrid(Intent intent)
    {
        if(!Current(intent))return false;if(intent.Workspace==null)return true;
        var actual=intent.Workspace.ReadCustodyGrid();if(!Compatible(actual,intent.GridBefore,intent.GridAfter))return false;
        for(int slot=0;slot<intent.GridAfter.Length;slot++)if(!Same(actual[slot],intent.GridAfter[slot]))
        {if(!Current(intent))return false;intent.Workspace.WriteCustodyGrid(slot,intent.GridAfter[slot].Clone());}
        return Current(intent)&&Same(intent.Workspace.ReadCustodyGrid(),intent.GridAfter);
    }
    private static bool AdvanceOwnerReturnOverflow(Intent intent)
    {
        var outputs=OwnerOverflow(intent.Record);if(outputs==null||intent.WorldObject.IsRemote()||!Current(intent))return false;
        if(!OwnerReturnMarker(intent))
        {
            // Receipt two means the exact native inventory delta was observed
            // before saving. Source grid cells remain held until every drop.
            Apply(intent,false);intent.Player.Buffs.SetCustomVar(Receipt(intent.Operation),2f,true);GameManager.Instance.SaveLocalPlayerData();
            if(!Saved(intent,2,intent.After))return false;
        }
        var body=XElement.Parse(Encoding.UTF8.GetString(intent.Record.Before));
        bool SourceCurrent()=>Current(intent)&&OwnerReturnMarker(intent)&&
            (intent.Workspace==null||Compatible(intent.Workspace.ReadCustodyGrid(),intent.GridBefore,intent.GridAfter));
        if(!SourceCurrent()||!RebirthCookingOverflowOriginal.Advance(intent.Record,intent.Player,intent.Owner,intent.Creation,outputs,intent.Deaths,(int)body.Attribute("overflowEntity"),SourceCurrent))return false;
        return SourceCurrent()&&ProjectReturnGrid(intent);
    }
    // A positive per-original saved owner receipt plus all original drop facts
    // permits only grid metadata. Neither old owner image is ever installed.
    private static bool TryRecoverSavedOwnerReturn(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,RebirthCamperTransferJournal.Record record,out bool recovered)
    {
        recovered=false;var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));if((string)root.Attribute("action")!="return")return false;
        if(player?.world?.IsRemote()!=false)return false;
        if(!CurrentOwner(player)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||
            (string)root.Attribute("world")!=player.world.worldState.Guid||(string)root.Attribute("owner")!=owner.CanonicalId||
            (string)root.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||(int?)root.Attribute("deaths") is not int deaths||deaths>player.Died)return true;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)return deaths<player.Died;
        if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain&&record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
        var network=RebirthRemoteResourceNetworkScope.Capture(player);int currentDeaths=player.Died;var world=player.world;string save=GameIO.GetSaveGameDir();
        bool Scope()=>network?.Current==true&&CurrentOwner(player)&&ReferenceEquals(player.world,world)&&player.Died==currentDeaths&&save==GameIO.GetSaveGameDir()&&
            RebirthCamperCharacterAccess.CurrentCreation(player)==record.OwnerCreation;
        if(!Scope()||!RebirthCamperEnergySavedOwner.TryRead(owner,out _,out var file)||file.id!=player.entityId||file.deaths!=currentDeaths||
            !RebirthCookingRemoteAdmissionBackend.NativePhase(file,Receipt(record.Operation),out float phase,out bool present)||!present||phase!=2f)return deaths<player.Died;
        var overflow=OwnerOverflow(record);
        if(overflow!=null&&!RebirthCookingOverflowOriginal.Terminal(record,player,owner.CanonicalId,record.OwnerCreation,overflow,deaths,(int)root.Attribute("overflowEntity")))return deaths<player.Died;
        var before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("beforeGrid").Value));
        var after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("afterGrid").Value));
        if(overflow==null?!Conserved(Actor(root.Element("beforeActor").Value),before,Actor(root.Element("afterActor").Value),after,null):
            !Conserved(Actor(root.Element("afterActor").Value),after,Actor(root.Element("beforeActor").Value),before,overflow))return true;
        if(!Scope())return true;
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=Journal().Advance(record.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        if(workspace!=null)
        {
            var live=workspace.ReadCustodyGrid();if(!live.All(item=>item.IsEmpty())&&!Compatible(live,before,after))return true;
            for(int slot=0;slot<after.Length;slot++)if(!Same(live[slot],after[slot]))
            {if(!Scope())return true;workspace.WriteCustodyGrid(slot,after[slot].Clone());}
            if(!Scope()||!Same(workspace.ReadCustodyGrid(),after))return true;
        }
        if(!Scope())return true;Active.RemoveAll(value=>value.Operation==record.Operation);recovered=true;
        workspace?.CustodyStatus("");workspace?.CustodyTransitionFinished("return");return true;
    }
}
