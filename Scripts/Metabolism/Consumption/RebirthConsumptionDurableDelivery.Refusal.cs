using System;
using System.Linq;
using System.IO;

internal static partial class RebirthConsumptionDurableDelivery
{
    // A stale selected stack can be refused only before the host has ever
    // issued a source command. Client feedback is never source-payment proof.
    internal static bool TryReceiveInventoryRefusal(NetPackage packet,World world,GameManager callbacks,int actor,string text,byte phase)
    {
        if(TryReceiveInventoryPurposeFragment(packet,world,callbacks,actor,phase,text,true,out string assembled))
        {if(assembled==null)return true;text=assembled;}
        if(text==null||!text.StartsWith("<playerOwnerConsumption",StringComparison.Ordinal))return false;
        if(phase!=0||!RebirthRemoteResourceNetworkScope.Receive(packet,world,callbacks,true,out _))return true;
        var original=inventoryOriginals.Values.SingleOrDefault(value=>value.Actor.entityId==actor&&value.Purpose==text&&value.Current());
        if(original!=null)RefuseUnissuedInventoryOriginal(original);
        return true;
    }
    private static void RefuseUnissuedInventoryOriginal(InventoryOriginal original)
    {
        if(!original.Current()||original.SourceIssued||original.SourceSaved||original.CanonicalEntered||original.DeferredEntered)return;
        original.Record("refused-before-source");
        var failure=new RebirthConsumeResult{Success=false,Failure=RebirthMetabolismConsumeFailure.ItemChanged,Message="The selected item changed before consumption. Try again."};
        if(original.Actor is EntityPlayerLocal)RebirthMetabolismUiFeedback.Receive(failure);
        else SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(original.Actor.entityId)?.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>().Setup(failure));
        original.Terminal=true;inventoryOriginals.Remove(original.Operation);CompleteInventoryNativeLoad(original.Operation);ReleaseInventoryOriginalMemory(original);
    }
    private static bool TryRefuseSavedUnissuedInventoryRequest(EntityPlayer actor,Guid operation,RebirthMetabolismSourceKind kind,int slot,int count,byte[] fingerprint)
    {
        if(inventoryOriginals.ContainsKey(operation)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return false;
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","PlayerConsumption",owner.StorageKey,operation.ToString("N"));
        if(!System.IO.Directory.Exists(directory))return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out var memory))return true;
        using(memory)try
        {
            var scope=RebirthRemoteResourceNetworkScope.Capture(actor);int deaths=actor.Died;string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            bool Current()=>scope?.Current==true&&!actor.IsDead()&&actor.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&!inventoryOriginals.ContainsKey(operation);
            if(!Current())return true;
            var history=ReadInventoryHistory(directory,operation,out string text);
            // Every accepted positive head here precedes the first source
            // permission. This is stronger than an absent attempted marker.
            string[] beforePermission={"prepared","owner-before-intent","owner-before-saved","refused-before-source"};
            if(history.Keys.Any(stage=>!beforePermission.Contains(stage)))return false;
            var image=RebirthCookingRemoteAdmission.ReadProtocolXml(text,16*1024*1024);
            if(image.Name!="playerSourceConsumption"||image.Attributes().Count()!=9||(int?)image.Attribute("version")!=1||image.Elements().Count()!=13||(string)image.Attribute("operation")!=operation.ToString("N")||
                (string)image.Attribute("owner")!=owner.CanonicalId||(string)image.Attribute("creation")!=creation||
                (string)image.Attribute("world")!=actor.world.worldState.Guid||(int?)image.Attribute("deaths") is not int originalDeaths||originalDeaths<0||originalDeaths>deaths||
                checked((byte?)(int?)image.Attribute("sourceKind"))!=(byte)kind||(int?)image.Attribute("sourceSlot")!=slot)return true;
            var source=PurposeStack(image.Element("source"));
            if(source?.count!=count||!RebirthConsumptionSourceIdentity.MatchesFingerprint(source.itemValue,fingerprint)||!Current())return true;
            InventoryOriginal.RecordBoundStage(directory,operation,text,Current,"refused-before-source");
            var failure=new RebirthConsumeResult{Success=false,Failure=RebirthMetabolismConsumeFailure.ItemChanged,Message="The earlier serving was not consumed. Use the item again."};
            if(actor is EntityPlayerLocal)RebirthMetabolismUiFeedback.Receive(failure);
            else SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>().Setup(failure),_attachedToEntityId:actor.entityId);
            return true;
        }
        catch{return true;}
    }
}
