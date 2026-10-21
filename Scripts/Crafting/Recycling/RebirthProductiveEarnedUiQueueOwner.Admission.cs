using System;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class AdmissionInsertion
    {internal EntityPlayer Actor;internal TileEntityWorkstation Station;internal RecipeQueueItem Entry;internal string Owner,Creation;internal int Deaths;internal Guid Operation;internal byte[] Before;internal IDisposable Retention;}
    internal static void BeforeAdmission(EntityPlayer actor,TileEntityWorkstation station,RebirthStationGridAdmission admission,RecipeQueueItem entry,out object state)
    {
        state=null;if(!installed||actor?.world?.IsRemote()!=false||entry?.Recipe==null||admission==null||!admission.IsPublicationAttempted||
            entry.StartingEntityId!=actor.entityId||admission.CreationId!=RebirthCamperCharacterAccess.CurrentCreation(actor))return;
        try
        {
            ObserveWorld();string owner;
            if(actor is EntityPlayerLocal&&ReferenceEquals(actor.world.GetPrimaryPlayer(),actor))
            {if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return;owner=identity.StorageKey;}
            else
            {var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);if(peer==null||!peer.loginDone||peer.disconnecting||
                !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity))return;owner=identity.StorageKey;}
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!Capture(station,out var before,out _)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*before.Length),out var retention))return;
                state=new AdmissionInsertion{Actor=actor,Station=station,Entry=entry,Owner=owner,Creation=admission.CreationId,Deaths=actor.Died,Before=before,Retention=retention,Operation=Guid.ParseExact(admission.JobId,"N")};
            }
        }
        catch{}
    }
    internal static void AfterAdmission(object state,bool applied)
    {
        if(state is not AdmissionInsertion value)return;
        try
        {if(applied&&value.Station.Queue!=null&&Array.FindAll(value.Station.Queue,row=>ReferenceEquals(row,value.Entry)).Length==1)
            Issue(value.Actor,value.Station,value.Entry,value.Owner,value.Creation,value.Deaths,value.Before,value.Operation);}
        catch{/* Observation never changes the original native publication result. */}
        finally{value.Before=null;value.Retention?.Dispose();value.Retention=null;}
    }
}
